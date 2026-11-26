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

using CloudStreams.Core.Transports.Authentication;
using Neuroglia.Eventing.CloudEvents;

namespace CloudStreams.Core.Transports.Services;

/// <summary>
/// Defines the fundamentals of a transport, other than HTTP, that brokers deliver cloud events over (see #218)
/// </summary>
public interface ICloudEventSink
{

    /// <summary>
    /// Gets the schemes of the addresses the sink delivers to. See <see cref="TransportSchemes"/>
    /// </summary>
    IReadOnlyList<string> Schemes { get; }

    /// <summary>
    /// Delivers the specified cloud events, in order, and waits for the transport to confirm that it has received them
    /// </summary>
    /// <param name="address">The address to deliver the cloud events to</param>
    /// <param name="events">The cloud events to deliver</param>
    /// <param name="headers">The headers to send with each cloud event, such as the name of the subscription and the offset</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>, which bounds how long to wait for the confirmation</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>Throws a <see cref="HttpRequestException"/>, with a status code, if the transport refused the cloud events, or any other exception if it could not be reached</remarks>
    Task SendAsync(TransportAddress address, TransportCredentials? credentials, IReadOnlyList<CloudEvent> events, IReadOnlyList<KeyValuePair<string, string>> headers, CancellationToken cancellationToken = default);

}

/// <summary>
/// Defines the fundamentals of a transport that gateways consume cloud events from, i.e. feeds (see #218)
/// </summary>
public interface ICloudEventFeedConsumer
{

    /// <summary>
    /// Gets the schemes of the addresses the consumer consumes from. See <see cref="TransportSchemes"/>
    /// </summary>
    IReadOnlyList<string> Schemes { get; }

    /// <summary>
    /// Consumes the messages of the specified channel until cancelled
    /// </summary>
    /// <param name="address">The address of the channel to consume</param>
    /// <param name="group">The name of the group of consumers that share the consumption of the feed, i.e. the name of the gateway</param>
    /// <param name="name">A name that uniquely identifies the consumer within its group, e.g. to name a durable NATS JetStream consumer</param>
    /// <param name="handler">A function that handles each message, and returns whether it should be acknowledged. A message that is not is consumed again, later</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that stops the consumption</param>
    /// <returns>A new awaitable <see cref="Task"/>, which completes when the consumption stops, or fails if the channel cannot be consumed</returns>
    Task ConsumeAsync(TransportAddress address, TransportCredentials? credentials, string group, string name, Func<TransportMessage, CancellationToken, Task<bool>> handler, CancellationToken cancellationToken);

}
