// Copyright © 2023-Present The Cloud Streams Authors
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

namespace CloudStreams.Core.Data;

/// <summary>
/// Represents an object used to configure a cloud event gateway
/// </summary>
[DataContract]
public record GatewaySpec
{

    /// <summary>
    /// Gets/sets the authorization policy that applies to cloud events of any source
    /// </summary>
    [DataMember(Order = 1, Name = "authorization"), JsonPropertyOrder(1), JsonPropertyName("authorization"), YamlMember(Order = 1, Alias = "authorization")]
    public virtual CloudEventAuthorizationPolicy? Authorization { get; set; } = null!;

    /// <summary>
    /// Gets/sets the validation policy that applies to cloud events of any source
    /// </summary>
    [DataMember(Order = 2, Name = "validation"), JsonPropertyOrder(2), JsonPropertyName("validation"), YamlMember(Order = 2, Alias = "validation")]
    public virtual CloudEventValidationPolicy? Validation { get; set; } = null!;

    /// <summary>
    /// Gets/sets the configuration that applies to specific cloud event sources
    /// </summary>
    [DataMember(Order = 3, Name = "sources"), JsonPropertyOrder(3), JsonPropertyName("sources"), YamlMember(Order = 3, Alias = "sources")]
    public virtual List<CloudEventSourceDefinition>? Sources { get; set; }

    /// <summary>
    /// Gets/sets a list containing event-specific ingestion configurations
    /// </summary>
    [DataMember(Order = 4, Name = "events"), JsonPropertyOrder(4), JsonPropertyName("events"), YamlMember(Order = 4, Alias = "events")]
    public virtual List<CloudEventIngestionConfiguration>? Events { get; set; }

    /// <summary>
    /// Gets/sets an object used to configure the gateway service, if any
    /// </summary>
    [DataMember(Order = 5, Name = "service"), JsonPropertyOrder(5), JsonPropertyName("service"), YamlMember(Order = 5, Alias = "service")]
    public virtual ServiceConfiguration? Service { get; set; }

}
