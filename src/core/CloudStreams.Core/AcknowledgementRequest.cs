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

namespace CloudStreams.Core;

/// <summary>
/// Represents a request to acknowledge a cloud event whose acknowledgement has been deferred
/// </summary>
[DataContract]
public record AcknowledgementRequest
{

    /// <summary>
    /// Gets/sets the acknowledgement token the cloud event has been delivered with, in the <c>cloudstreams-ack-token</c> header
    /// </summary>
    [Required, MinLength(1)]
    [DataMember(Order = 1, Name = "token", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("token"), YamlMember(Order = 1, Alias = "token")]
    public virtual string Token { get; set; } = null!;

}

/// <summary>
/// Represents a request to negatively acknowledge a cloud event whose acknowledgement has been deferred, so that it is redelivered
/// </summary>
[DataContract]
public record NegativeAcknowledgementRequest
    : AcknowledgementRequest
{

    /// <summary>
    /// Gets/sets the reason why the cloud event is negatively acknowledged, if any. It is reported on the subscription's status
    /// </summary>
    [DataMember(Order = 2, Name = "reason"), JsonPropertyOrder(2), JsonPropertyName("reason"), YamlMember(Order = 2, Alias = "reason")]
    public virtual string? Reason { get; set; }

}
