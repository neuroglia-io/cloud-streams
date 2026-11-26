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

using Neuroglia.Data.Infrastructure.EventSourcing;
using Neuroglia.Data.Infrastructure.EventSourcing.Services;
using Neuroglia.Serialization;
using System.Runtime.CompilerServices;

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Defines the fundamentals of a service used to store the cloud events subscriptions failed to deliver (see #207)
/// </summary>
public interface IDeadLetterStore
{

    /// <summary>
    /// Appends the specified dead letter to the dead-letter stream of the specified subscription
    /// </summary>
    /// <param name="subscription">The qualified name of the subscription that failed to deliver the cloud event</param>
    /// <param name="deadLetter">The dead letter to append</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The offset of the dead letter in the subscription's dead-letter stream</returns>
    Task<ulong> AppendAsync(string subscription, DeadLetter deadLetter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the dead-letter stream of the specified subscription
    /// </summary>
    /// <param name="subscription">The qualified name of the subscription</param>
    /// <param name="readDirection">The direction in which to read the stream</param>
    /// <param name="offset">The offset starting from which to read</param>
    /// <param name="length">The maximum amount of dead letters to read</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing the dead letters read, none if the subscription has none</returns>
    IAsyncEnumerable<DeadLetter> ReadAsync(string subscription, StreamReadDirection readDirection, long offset, ulong length, CancellationToken cancellationToken = default);

}

/// <summary>
/// Represents an <see cref="IDeadLetterStore"/> that stores the dead letters of each subscription in a stream of the event store
/// </summary>
/// <param name="eventStore">The service used to store events</param>
/// <param name="serializer">The service used to serialize and deserialize dead letters</param>
public class EventStoreDeadLetterStore(IEventStore eventStore, IJsonSerializer serializer)
    : IDeadLetterStore
{

    /// <summary>
    /// Gets the type of the events that record dead letters
    /// </summary>
    public const string EventType = "io.cloud-streams.dead-letter.v1";

    /// <inheritdoc/>
    public virtual async Task<ulong> AppendAsync(string subscription, DeadLetter deadLetter, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscription);
        ArgumentNullException.ThrowIfNull(deadLetter);
        return await eventStore.AppendAsync(StreamNameHelper.DeadLetters(subscription), [new EventDescriptor(EventType, deadLetter)], cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<DeadLetter> ReadAsync(string subscription, StreamReadDirection readDirection, long offset, ulong length, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscription);
        // the underlying store reports a missing stream while the records are being enumerated: a subscription that never dead-lettered anything has no stream
        var enumerator = eventStore.ReadAsync(StreamNameHelper.DeadLetters(subscription), readDirection, offset, length, cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false)) yield break;
                }
                catch (StreamNotFoundException)
                {
                    yield break;
                }
                var record = enumerator.Current;
                var deadLetter = serializer.Deserialize<DeadLetter>(serializer.SerializeToNode(record.Data)!)!;
                deadLetter.Offset = record.Offset;
                yield return deadLetter;
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

}
