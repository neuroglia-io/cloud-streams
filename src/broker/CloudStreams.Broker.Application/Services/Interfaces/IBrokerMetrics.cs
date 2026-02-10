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

namespace CloudStreams.Broker.Application.Services;

/// <summary>
/// Defines the fundamentals of a service used to manage Cloud Streams Broker related metrics
/// </summary>
public interface IBrokerMetrics
    : IDisposable
{

    /// <summary>
    /// Increments the total count of cloud events published (dispatched) by the broker
    /// </summary>
    /// <param name="stream">The name of the stream or partition the event was published from, or "All" for unpartitioned subscriptions</param>
    /// <param name="subscriber">The URI of the subscriber the event was dispatched to</param>
    void IncrementTotalPublishedEvents(string? stream, string? subscriber);

    /// <summary>
    /// Increments the total count of delivery failures encountered by the broker
    /// </summary>
    /// <param name="stream">The name of the stream or partition the event was published from, or "All" for unpartitioned subscriptions</param>
    /// <param name="subscriber">The URI of the subscriber the delivery failed for</param>
    void IncrementTotalDeliveryFailures(string? stream, string? subscriber);

}
