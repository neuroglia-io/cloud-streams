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
/// Represents the object used to configure a schema to validate defined resources
/// </summary>
[DataContract]
public class ResourceDefinitionValidation
{

    /// <summary>
    /// Gets/sets the JSON schema used to validate defined resources
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "openAPIV3Schema", IsRequired = true), JsonPropertyName("openAPIV3Schema"), YamlMember(Alias = "openAPIV3Schema")]
    public JsonSchema OpenAPIV3Schema { get; set; } = null!;

}