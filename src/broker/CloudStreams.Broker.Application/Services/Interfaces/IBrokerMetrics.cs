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
/// Defines the fundamentals of a service used to manage the broker's metrics
/// </summary>
/// <remarks>
/// Every metric is labelled with the qualified name of the subscription and with the name of the stream it reads from, both of which are bounded by the amount of
/// subscriptions. The subscriber's URI is deliberately not used as a label: its cardinality is unbounded, and it may carry credentials in its query string (see #202)
/// </remarks>
public interface IBrokerMetrics
    : IDisposable
{

    /// <summary>
    /// Increments the total count of cloud events published (dispatched) by the broker
    /// </summary>
    /// <param name="subscription">The qualified name of the subscription the event was dispatched for</param>
    /// <param name="stream">The name of the stream or partition the event was published from, or "All" for unpartitioned subscriptions</param>
    void IncrementTotalPublishedEvents(string subscription, string stream);

    /// <summary>
    /// Increments the total count of delivery failures encountered by the broker
    /// </summary>
    /// <param name="subscription">The qualified name of the subscription the delivery failed for</param>
    /// <param name="stream">The name of the stream or partition the event was published from, or "All" for unpartitioned subscriptions</param>
    void IncrementTotalDeliveryFailures(string subscription, string stream);

    /// <summary>
    /// Records the duration of a delivery attempt, from the request to the subscriber's response, mutation included
    /// </summary>
    /// <param name="subscription">The qualified name of the subscription the event was dispatched for</param>
    /// <param name="stream">The name of the stream or partition the event was published from, or "All" for unpartitioned subscriptions</param>
    /// <param name="outcome">The outcome of the delivery attempt. See <see cref="DeliveryOutcome"/></param>
    /// <param name="duration">The duration of the delivery attempt</param>
    void RecordDeliveryDuration(string subscription, string stream, string outcome, TimeSpan duration);

    /// <summary>
    /// Adds the specified delta to the amount of cloud events whose acknowledgement is pending
    /// </summary>
    /// <param name="subscription">The qualified name of the subscription whose subscriber deferred the acknowledgement</param>
    /// <param name="stream">The name of the stream or partition the event was published from, or "All" for unpartitioned subscriptions</param>
    /// <param name="delta">The delta to add: 1 when a subscriber defers an acknowledgement, -1 when it is acknowledged, negatively acknowledged or expires</param>
    void AddPendingAcknowledgements(string subscription, string stream, int delta);

    /// <summary>
    /// Records how long a deferred acknowledgement has been pending before it was resolved
    /// </summary>
    /// <param name="subscription">The qualified name of the subscription whose subscriber deferred the acknowledgement</param>
    /// <param name="stream">The name of the stream or partition the event was published from, or "All" for unpartitioned subscriptions</param>
    /// <param name="outcome">How the acknowledgement was resolved. See <see cref="AcknowledgementOutcome"/></param>
    /// <param name="duration">How long the acknowledgement has been pending</param>
    void RecordAcknowledgementDuration(string subscription, string stream, string outcome, TimeSpan duration);

    /// <summary>
    /// Increments the total count of deferred acknowledgements that have expired
    /// </summary>
    /// <param name="subscription">The qualified name of the subscription whose subscriber failed to acknowledge in time</param>
    /// <param name="stream">The name of the stream or partition the event was published from, or "All" for unpartitioned subscriptions</param>
    void IncrementTotalAcknowledgementTimeouts(string subscription, string stream);

    /// <summary>
    /// Increments the total count of cloud events that could not be delivered, and have been dead-lettered
    /// </summary>
    /// <param name="subscription">The qualified name of the subscription that failed to deliver the cloud event</param>
    /// <param name="stream">The name of the stream or partition the event was published from, or "All" for unpartitioned subscriptions</param>
    void IncrementTotalDeadLetters(string subscription, string stream);

    /// <summary>
    /// Registers a subscription whose lag, which is to say the amount of records of its stream it has yet to handle, is reported by the lag gauge
    /// </summary>
    /// <param name="subscription">The qualified name of the subscription</param>
    /// <param name="observe">A function that returns the name of the stream the subscription reads from and its current lag, or null if it is unknown</param>
    /// <returns>A new <see cref="IDisposable"/> that stops reporting the subscription's lag once disposed of</returns>
    IDisposable ObserveLag(string subscription, Func<(string Stream, ulong Lag)?> observe);

}
