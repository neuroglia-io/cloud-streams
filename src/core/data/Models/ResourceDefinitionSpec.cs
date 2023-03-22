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
/// Represents an object used to configure a resource definition
/// </summary>
[DataContract]
public class ResourceDefinitionSpec
{

    /// <summary>
    /// Gets/sets a list containing object used to describe the resource definition's versions
    /// </summary>
    [Required, JsonRequired, MinLength(1)]
    [DataMember(Order = 1, Name = "versions", IsRequired = true), JsonPropertyName("versions"), YamlMember(Alias = "versions")]
    public virtual List<ResourceDefinitionVersion> Versions { get; set; } = null!;

}
