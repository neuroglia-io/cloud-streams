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
/// Enumerates the outcomes of the ingestion of a cloud event, as reported by the gateway's metrics
/// </summary>
public static class IngestionOutcome
{

    /// <summary>
    /// Indicates that the cloud event has been admitted and stored
    /// </summary>
    public const string Stored = "stored";
    /// <summary>
    /// Indicates that the cloud event has been ignored by the split-horizon policy
    /// </summary>
    public const string Ignored = "ignored";
    /// <summary>
    /// Indicates that the cloud event has been rejected by the authorization or split-horizon policies
    /// </summary>
    public const string Rejected = "rejected";
    /// <summary>
    /// Indicates that the cloud event is invalid
    /// </summary>
    public const string Invalid = "invalid";
    /// <summary>
    /// Indicates that the gateway could not ingest the cloud event: its configuration has not been loaded yet, or the event store failed
    /// </summary>
    public const string Failed = "failed";

}

/// <summary>
/// Enumerates the reasons why the gateway rejects a cloud event, as reported by its metrics
/// </summary>
public static class RejectionReason
{

    /// <summary>
    /// Indicates that the gateway's split-horizon policy refused the cloud event's origin, or detected a loop
    /// </summary>
    public const string SplitHorizon = "split_horizon";
    /// <summary>
    /// Indicates that the gateway's authorization policy refused the cloud event
    /// </summary>
    public const string Authorization = "authorization";

}

/// <summary>
/// Enumerates the reasons why the gateway deems a cloud event invalid, as reported by its metrics
/// </summary>
public static class InvalidityReason
{

    /// <summary>
    /// Indicates that the cloud event's context attributes are invalid
    /// </summary>
    public const string Attributes = "attributes";
    /// <summary>
    /// Indicates that the cloud event's data does not match its schema, or that its schema could not be resolved
    /// </summary>
    public const string Schema = "schema";

}
