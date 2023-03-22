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
/// Represents an object used to describe a version of a resource definition
/// </summary>
[DataContract]
public class ResourceDefinitionVersion
{

    /// <summary>
    /// Gets/sets the name of the described resource definition version
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "name", IsRequired = true), JsonPropertyName("name"), YamlMember(Alias = "name")]
    public virtual string Name { get; set; } = null!;

    /// <summary>
    /// Gets/sets an object used to configure a schema to validate defined resources
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 2, Name = "schema", IsRequired = true), JsonPropertyName("schema"), YamlMember(Alias = "schema")]
    public virtual ResourceDefinitionValidation Schema { get; set; } = null!;

}
