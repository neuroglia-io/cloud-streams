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

namespace CloudStreams.Gateway.Application.Services;

/// <summary>
/// Defines the fundamentals of a service used to manage the gateway's metrics
/// </summary>
/// <remarks>
/// Every label has a bounded cardinality. Events refused by the gateway are labelled with the reason they were refused for, but not with their source, which is chosen
/// by the publisher and therefore unbounded. Admitted events are labelled with their source, up to a maximum amount of distinct sources, past which they are labelled
/// 'other'. Their type is not used as a label (see #202)
/// </remarks>
public interface IGatewayMetrics
    : IDisposable
{

    /// <summary>
    /// Increments the total count of cloud events ingested by the gateway, which is to say admitted and appended to the event store
    /// </summary>
    /// <param name="source">The source of the ingested cloud event</param>
    void IncrementTotalIngestedEvents(Uri source);

    /// <summary>
    /// Increments the total count of cloud events rejected by the gateway's authorization or split-horizon policies
    /// </summary>
    /// <param name="reason">The reason why the cloud event has been rejected. See <see cref="RejectionReason"/></param>
    void IncrementTotalRejectedEvents(string reason);

    /// <summary>
    /// Increments the total count of invalid cloud events received by the gateway
    /// </summary>
    /// <param name="reason">The reason why the cloud event is invalid. See <see cref="InvalidityReason"/></param>
    void IncrementTotalInvalidEvents(string reason);

    /// <summary>
    /// Increments the total count of cloud events ignored by the gateway's split-horizon policy
    /// </summary>
    /// <param name="source">The source of the ignored cloud event</param>
    void IncrementTotalIgnoredEvents(Uri source);

    /// <summary>
    /// Increments the total count of cloud events published again within the deduplication window, which have not been stored again
    /// </summary>
    /// <param name="source">The source of the duplicate cloud event</param>
    void IncrementTotalDuplicateEvents(Uri source);

    /// <summary>
    /// Increments the total count of admitted cloud events that could not be appended to the event store
    /// </summary>
    /// <param name="source">The source of the cloud event that could not be appended</param>
    void IncrementTotalAppendFailures(Uri source);

    /// <summary>
    /// Records the duration of the ingestion of a cloud event, from its admission to its storage
    /// </summary>
    /// <param name="outcome">The outcome of the ingestion. See <see cref="IngestionOutcome"/></param>
    /// <param name="duration">The duration of the ingestion</param>
    void RecordIngestionDuration(string outcome, TimeSpan duration);

}
