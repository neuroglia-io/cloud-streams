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
/// Represents an object used to configure a cloud event source
/// </summary>
[DataContract]
public class CloudEventSourceSpec
{

    /// <summary>
    /// Gets/sets the uri of the cloud event source to configure
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "uri", IsRequired = true), JsonPropertyName("uri"), YamlMember(Alias = "uri")]
    public virtual Uri Uri { get; set; } = null!;

    /// <summary>
    /// Gets/sets the policy to use to authorize cloud events produced by the source
    /// </summary>
    [DataMember(Order = 2, Name = "authorization"), JsonPropertyName("authorization"), YamlMember(Alias = "authorization")]
    public virtual CloudEventAuthorizationPolicy? Authorization { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [DataMember(Order = 3, Name = "validation"), JsonPropertyName("validation"), YamlMember(Alias = "validation")]
    public virtual CloudEventValidationPolicy? Validation { get; set; }

}
