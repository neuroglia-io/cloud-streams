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

using CloudStreams.Core.Resources;

namespace CloudStreams.Core;

/// <summary>
/// Represents a request to evaluate a cloud event against the policies of a gateway, which may not be saved yet, without ingesting it (see #262)
/// </summary>
[DataContract]
public record GatewayAdmissionTestRequest
{

    /// <summary>
    /// Gets/sets the spec of the gateway to evaluate the cloud event against
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "spec", IsRequired = true), JsonPropertyName("spec")]
    public virtual GatewaySpec Spec { get; set; } = null!;

    /// <summary>
    /// Gets/sets the cloud event to evaluate
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 2, Name = "event", IsRequired = true), JsonPropertyName("event")]
    public virtual CloudEvent Event { get; set; } = null!;

}

/// <summary>
/// Represents the outcome of a <see cref="GatewayAdmissionTestRequest"/>
/// </summary>
[DataContract]
public record GatewayAdmissionTestResult
{

    /// <summary>
    /// Gets the outcome of a cloud event that would be stored
    /// </summary>
    public const string Accepted = "accepted";

    /// <summary>
    /// Gets the outcome of a cloud event that would be ignored, as a loop, by the split-horizon policy
    /// </summary>
    public const string Ignored = "ignored";

    /// <summary>
    /// Gets the outcome of a cloud event that would be refused by the authorization or split-horizon policies
    /// </summary>
    public const string Rejected = "rejected";

    /// <summary>
    /// Gets the outcome of a cloud event that would be refused as invalid
    /// </summary>
    public const string Invalid = "invalid";

    /// <summary>
    /// Gets/sets the outcome: <see cref="Accepted"/>, <see cref="Ignored"/>, <see cref="Rejected"/> or <see cref="Invalid"/>
    /// </summary>
    [DataMember(Order = 1, Name = "outcome"), JsonPropertyName("outcome")]
    public virtual string Outcome { get; set; } = null!;

    /// <summary>
    /// Gets/sets the status code the publisher would receive
    /// </summary>
    [DataMember(Order = 2, Name = "status"), JsonPropertyName("status")]
    public virtual int Status { get; set; }

    /// <summary>
    /// Gets/sets why the cloud event would not be stored, if it would not
    /// </summary>
    [DataMember(Order = 3, Name = "reason"), JsonPropertyName("reason")]
    public virtual string? Reason { get; set; }

    /// <summary>
    /// Gets/sets the errors that make the cloud event invalid, keyed by attribute or by location in its data, if any
    /// </summary>
    [DataMember(Order = 4, Name = "errors"), JsonPropertyName("errors")]
    public virtual Dictionary<string, string[]>? Errors { get; set; }

    /// <summary>
    /// Gets/sets the context attributes the cloud event would be stored with, which include those the gateway adds, such as its origin, if it would be stored
    /// </summary>
    [DataMember(Order = 5, Name = "attributes"), JsonPropertyName("attributes")]
    public virtual Dictionary<string, object>? Attributes { get; set; }

    /// <summary>
    /// Gets/sets the metadata the gateway would extract from the cloud event, if any
    /// </summary>
    [DataMember(Order = 6, Name = "metadata"), JsonPropertyName("metadata")]
    public virtual Dictionary<string, object>? Metadata { get; set; }

}
