// Copyright © 2023-Present The Cloud Streams Authors
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

using CloudStreams.Core.Data;
using CloudStreams.Core.Infrastructure.Services;
using Hylo.Api.Application;
using System.Net;

namespace CloudStreams.Core.Application.Queries.Streams;

/// <summary>
/// Represents the <see cref="IQuery{TResult}"/> used to list stored <see cref="CloudEvent"/>s
/// </summary>
public class ReadEventStreamQuery
    : IQuery<IAsyncEnumerable<object>>
{

    /// <summary>
    /// Initializes a new <see cref="ReadEventStreamQuery"/>
    /// </summary>
    /// <param name="options">The object used to configure the query to perform</param>
    public ReadEventStreamQuery(StreamReadOptions options)
    {
        this.Options = options;
    }

    /// <summary>
    /// Gets the object used to configure the query to perform
    /// </summary>
    public StreamReadOptions Options { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="ReadEventStreamQuery"/> instances
/// </summary>
public class ReadCloudEventStreamQueryHandler
    : IQueryHandler<ReadEventStreamQuery, IAsyncEnumerable<object>>
{

    readonly IEventStoreProvider _eventStoreProvider;

    /// <inheritdoc/>
    public ReadCloudEventStreamQueryHandler(IEventStoreProvider eventStoreProvider)
    {
        this._eventStoreProvider = eventStoreProvider;
    }

    /// <inheritdoc/>
    public Task<ApiResponse<IAsyncEnumerable<object>>> Handle(ReadEventStreamQuery query, CancellationToken cancellationToken)
    {
        var length = query.Options.Length > StreamReadOptions.MaxLength ? StreamReadOptions.MaxLength : query.Options.Length;
        if (length < 1) length = 1;
        var offset = query.Options.Offset;
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
                    return Task.FromResult(new ApiResponse<IAsyncEnumerable<object>>((int)HttpStatusCode.BadRequest) { Errors = new(new KeyValuePair<string, string[]>[] { new(nameof(query.Options.Direction).ToLowerInvariant(), new string[] { $"The specified {nameof(StreamReadDirection)} '{query.Options.Direction}' is not supported" }) }) });
            }
        }
        var eventStore = _eventStoreProvider.GetEventStore();
        var events = query.Options.Partition == null ?
            eventStore.ReadAsync(query.Options.Direction, offset.Value, length, cancellationToken: cancellationToken)
            : eventStore.ReadPartitionAsync(query.Options.Partition, query.Options.Direction, offset.Value, length, cancellationToken: cancellationToken);
        var results = query.Options.Format switch
        {
            StreamReadOutputFormat.Event => events.Select(e => e.ToCloudEvent(default)).OfType<object>(),
            _ => events
        };
        return Task.FromResult(this.Ok(results));
    }

}
