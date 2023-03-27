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

namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to configure a cloud event gateway
/// </summary>
[DataContract]
public class GatewaySpec
{

    /// <summary>
    /// Gets/sets the authorization policy that applies to cloud events of any source
    /// </summary>
    [DataMember(Order = 1, Name = "authorization"), JsonPropertyName("authorization"), YamlMember(Alias = "authorization")]
    public virtual CloudEventAuthorizationPolicy? Authorization { get; set; } = null!;

    /// <summary>
    /// Gets/sets the validation policy that applies to cloud events of any source
    /// </summary>
    [DataMember(Order = 2, Name = "validation"), JsonPropertyName("validation"), YamlMember(Alias = "validation")]
    public virtual CloudEventValidationPolicy? Validation { get; set; } = null!;

    /// <summary>
    /// Gets/sets the configuration that applies to specific cloud event sources
    /// </summary>
    [DataMember(Order = 3, Name = "sources"), JsonPropertyName("sources"), YamlMember(Alias = "sources")]
    public virtual List<CloudEventSourceDefinition>? Sources { get; set; }

    /// <summary>
    /// Gets/sets a list containing event-specific ingestion configurations
    /// </summary>
    [DataMember(Order = 4, Name = "events"), JsonPropertyName("events"), YamlMember(Alias = "events")]
    public virtual List<CloudEventIngestionConfiguration>? Events { get; set; }

}
