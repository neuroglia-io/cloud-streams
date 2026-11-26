// Copyright © 2024-Present The Cloud Streams Authors
//
// Licensed under the Apache License, Version 2.0 (the "License"),
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using CloudStreams.Core.Application.Services;
using Neuroglia.Data.Infrastructure.EventSourcing;

namespace CloudStreams.Core.Application.Queries.Streams;

/// <summary>
/// Represents the <see cref="IQuery{TResult}"/> used to list stored <see cref="CloudEvent"/>s
/// </summary>
/// <remarks>
/// Initializes a new <see cref="ReadEventStreamQuery"/>
/// </remarks>
/// <param name="options">The object used to configure the query to perform</param>
/// <param name="namespace">The namespace the resource belongs to, if any</param>
public class ReadEventStreamQuery(StreamReadOptions options, string? @namespace = null)
        : Query<IAsyncEnumerable<object>>
{

    /// <summary>
    /// Gets the namespace to read the cloud events of, or null to read the whole stream
    /// </summary>
    public virtual string? Namespace { get; } = string.IsNullOrWhiteSpace(@namespace) ? null : @namespace;

    
    /// <summary>
    /// Gets the object used to configure the query to perform
    /// </summary>
    public StreamReadOptions Options { get; } = options;

}

/// <summary>
/// Represents the service used to handle <see cref="ReadEventStreamQuery"/> instances
/// </summary>
public class ReadCloudEventStreamQueryHandler(ICloudEventStore eventStore)
    : IQueryHandler<ReadEventStreamQuery, IAsyncEnumerable<object>>
{

    /// <inheritdoc/>
    public async Task<IOperationResult<IAsyncEnumerable<object>>> HandleAsync(ReadEventStreamQuery query, CancellationToken cancellationToken)
    {
        var length = query.Options.Length > StreamReadOptions.MaxLength ? StreamReadOptions.MaxLength : query.Options.Length;
        if (length < 1) length = 1;
        var offset = query.Options.Offset;
        if (offset.HasValue && query.Options.Time.HasValue)
            return new OperationResult<IAsyncEnumerable<object>>((int)HttpStatusCode.BadRequest, errors: [new Error(new Uri("https://cloud-streams.io/problems/validation-failed"), "Validation Failed", (int)HttpStatusCode.BadRequest, "Either 'offset' or 'time' may be set, but not both")]);
        if (query.Options.Time.HasValue)
        {
            // the first cloud event recorded at or after the specified time, if any (see #211)
            var found = await eventStore.FindOffsetAsync(query.Namespace, query.Options.Time.Value, query.Options.Partition, cancellationToken).ConfigureAwait(false);
            switch (query.Options.Direction)
            {
                case StreamReadDirection.Forwards:
                    if (!found.HasValue) return this.Ok(AsyncEnumerable.Empty<object>());
                    offset = (long)found.Value;
                    break;
                case StreamReadDirection.Backwards:
                    // reading backwards starts at the last cloud event recorded before the specified time: every cloud event has been, if none has been at or after it
                    if (found == 0) return this.Ok(AsyncEnumerable.Empty<object>());
                    offset = found.HasValue ? (long)found.Value - 1 : StreamPosition.EndOfStream;
                    break;
                default:
                    return new OperationResult<IAsyncEnumerable<object>>((int)HttpStatusCode.BadRequest);
            }
        }
        if (!offset.HasValue)
        {
            switch (query.Options.Direction)
            {
                case StreamReadDirection.Forwards:
                    offset = StreamPosition.StartOfStream;
                    break;
                case StreamReadDirection.Backwards:
                    offset = StreamPosition.EndOfStream;
                    break;
                default:
                    return new OperationResult<IAsyncEnumerable<object>>((int)HttpStatusCode.BadRequest);
            }
        }
        var events = query.Options.Partition == null ?
            eventStore.ReadAsync(query.Namespace, query.Options.Direction, offset.Value, length, cancellationToken: cancellationToken)
            : eventStore.ReadPartitionAsync(query.Namespace, query.Options.Partition, query.Options.Direction, offset.Value, length, cancellationToken: cancellationToken);
        var results = query.Options.Format switch
        {
            StreamReadOutputFormat.Event => events.Select(e => e.ToCloudEvent(default)).OfType<object>(),
            _ => events
        };
        return this.Ok(results);
    }

}
