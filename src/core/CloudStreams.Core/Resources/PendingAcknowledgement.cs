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

namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to describe a cloud event whose acknowledgement the subscriber has deferred
/// </summary>
/// <remarks>
/// It is stored in the subscription's status rather than in the broker's memory, because the acknowledgement is received by the gateway, which shares no
/// state with the broker. As a consequence, a pending acknowledgement also survives a broker restart
/// </remarks>
[DataContract]
public record PendingAcknowledgement
{

    /// <summary>
    /// Gets/sets the offset of the cloud event awaiting acknowledgement
    /// </summary>
    [Required]
    [DataMember(Order = 1, Name = "offset", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("offset"), YamlMember(Order = 1, Alias = "offset")]
    public virtual ulong Offset { get; set; }

    /// <summary>
    /// Gets/sets the opaque, single-use token the subscriber must present to acknowledge the cloud event
    /// </summary>
    [Required, MinLength(1)]
    [DataMember(Order = 2, Name = "token", IsRequired = true), JsonPropertyOrder(2), JsonPropertyName("token"), YamlMember(Order = 2, Alias = "token")]
    public virtual string Token { get; set; } = null!;

    /// <summary>
    /// Gets/sets the date and time at which the broker takes the cloud event back if it has not been acknowledged
    /// </summary>
    [Required]
    [DataMember(Order = 3, Name = "expiresAt", IsRequired = true), JsonPropertyOrder(3), JsonPropertyName("expiresAt"), YamlMember(Order = 3, Alias = "expiresAt")]
    public virtual DateTimeOffset ExpiresAt { get; set; }

}
