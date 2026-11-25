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

namespace CloudStreams.Core.Application.Queries.Subscriptions;

/// <summary>
/// Represents the query used to read the cloud events a <see cref="Subscription"/> failed to deliver, and has stored in its dead-letter stream (see #207)
/// </summary>
/// <param name="name">The name of the <see cref="Subscription"/> to read the dead letters of</param>
/// <param name="direction">The direction in which to read the dead letters</param>
/// <param name="offset">The offset starting from which to read the dead letters, if any. Defaults to the oldest when reading forwards, and to the most recent otherwise</param>
/// <param name="length">The maximum amount of dead letters to read</param>
public class ReadSubscriptionDeadLettersQuery(string name, StreamReadDirection direction = StreamReadDirection.Backwards, long? offset = null, ulong length = StreamReadOptions.MaxLength)
    : Query<IAsyncEnumerable<DeadLetter>>
{

    /// <summary>
    /// Gets the name of the <see cref="Subscription"/> to read the dead letters of
    /// </summary>
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentNullException(nameof(name)) : name;

    /// <summary>
    /// Gets the direction in which to read the dead letters
    /// </summary>
    public StreamReadDirection Direction { get; } = direction;

    /// <summary>
    /// Gets the offset starting from which to read the dead letters, if any
    /// </summary>
    public long? Offset { get; } = offset;

    /// <summary>
    /// Gets the maximum amount of dead letters to read
    /// </summary>
    public ulong Length { get; } = length;

}

/// <summary>
/// Represents the service used to handle <see cref="ReadSubscriptionDeadLettersQuery"/> instances
/// </summary>
/// <param name="resources">The service used to manage resources</param>
/// <param name="deadLetters">The service used to store dead letters</param>
public class ReadSubscriptionDeadLettersQueryHandler(IResourceRepository resources, IDeadLetterStore deadLetters)
    : IQueryHandler<ReadSubscriptionDeadLettersQuery, IAsyncEnumerable<DeadLetter>>
{

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<IAsyncEnumerable<DeadLetter>>> HandleAsync(ReadSubscriptionDeadLettersQuery query, CancellationToken cancellationToken)
    {
        var subscription = await resources.GetAsync<Subscription>(query.Name, null, cancellationToken).ConfigureAwait(false);
        if (subscription == null) return new OperationResult<IAsyncEnumerable<DeadLetter>>((int)HttpStatusCode.NotFound);
        var length = Math.Clamp(query.Length, 1, (ulong)StreamReadOptions.MaxLength);
        var offset = query.Offset ?? (query.Direction == StreamReadDirection.Forwards ? StreamPosition.StartOfStream : StreamPosition.EndOfStream);
        return this.Ok(deadLetters.ReadAsync(subscription.GetQualifiedName(), query.Direction, offset, length, cancellationToken));
    }

}
