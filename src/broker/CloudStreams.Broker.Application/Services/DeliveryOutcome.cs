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
/// Enumerates the outcomes of a delivery attempt, as reported by the broker's metrics
/// </summary>
public static class DeliveryOutcome
{

    /// <summary>
    /// Indicates that the subscriber accepted the event
    /// </summary>
    public const string Delivered = "delivered";
    /// <summary>
    /// Indicates that the subscriber deferred the acknowledgement of the event, by responding '202 Accepted'
    /// </summary>
    public const string Deferred = "deferred";
    /// <summary>
    /// Indicates that the delivery failed: the subscriber responded with a non-success status code, could not be reached or did not respond in time
    /// </summary>
    public const string Failed = "failed";

}

/// <summary>
/// Enumerates the ways a deferred acknowledgement can be resolved, as reported by the broker's metrics
/// </summary>
public static class AcknowledgementOutcome
{

    /// <summary>
    /// Indicates that the subscriber acknowledged the event
    /// </summary>
    public const string Acknowledged = "ack";
    /// <summary>
    /// Indicates that the subscriber negatively acknowledged the event, which is redelivered
    /// </summary>
    public const string NegativelyAcknowledged = "nack";
    /// <summary>
    /// Indicates that the subscriber did not call back before the acknowledgement expired
    /// </summary>
    public const string Expired = "expired";

}
