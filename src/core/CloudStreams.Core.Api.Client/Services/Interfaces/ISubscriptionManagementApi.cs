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

namespace CloudStreams.Core.Api.Client.Services;

/// <summary>
/// Defines the fundamentals of the API used to manage <see cref="Subscription"/>s
/// </summary>
public interface ISubscriptionManagementApi
    : IResourceManagementApi<Subscription>
{

    /// <summary>
    /// Suspends the specified <see cref="Subscription"/>: nothing is delivered, and its offset is kept, until it is resumed
    /// </summary>
    /// <param name="name">The name of the <see cref="Subscription"/> to suspend</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The suspended <see cref="Subscription"/></returns>
    Task<Subscription> SuspendAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes the specified suspended or faulted <see cref="Subscription"/>. A faulted subscription has its fault cleared, so that the cloud event that caused it is retried
    /// </summary>
    /// <param name="name">The name of the <see cref="Subscription"/> to resume</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The resumed <see cref="Subscription"/></returns>
    Task<Subscription> ResumeAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replays the specified <see cref="Subscription"/>, by delivering every cloud event matching it again, from the beginning of the stream or from the specified time
    /// </summary>
    /// <param name="name">The name of the <see cref="Subscription"/> to replay</param>
    /// <param name="from">The time from which to replay the <see cref="Subscription"/>, if any: it is replayed from the first cloud event recorded at or after that time</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The replayed <see cref="Subscription"/></returns>
    Task<Subscription> ReplayAsync(string name, DateTimeOffset? from = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the cloud events the specified <see cref="Subscription"/> failed to deliver, and has stored in its dead-letter stream, most recent first
    /// </summary>
    /// <param name="name">The name of the <see cref="Subscription"/> to get the dead letters of</param>
    /// <param name="offset">The offset starting from which to read the dead letters, backwards, if any. Defaults to the most recent</param>
    /// <param name="length">The maximum amount of dead letters to get</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The dead letters read</returns>
    Task<IReadOnlyList<DeadLetter>> GetDeadLettersAsync(string name, long? offset = null, ulong length = StreamReadOptions.MaxLength, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delivers the specified dead letter to the subscriber of the specified <see cref="Subscription"/> again, once
    /// </summary>
    /// <param name="name">The name of the <see cref="Subscription"/> the dead letter belongs to</param>
    /// <param name="offset">The offset of the dead letter in the subscription's dead-letter stream</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>An object that describes the redelivery</returns>
    Task<DeadLetterRedelivery> RedeliverDeadLetterAsync(string name, ulong offset, CancellationToken cancellationToken = default);

}
