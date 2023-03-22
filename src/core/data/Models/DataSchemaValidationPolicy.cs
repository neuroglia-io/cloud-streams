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
/// Represents an object used to configure schema-based validation of incoming cloud events' data
/// </summary>
[DataContract]
public class DataSchemaValidationPolicy
{

    /// <summary>
    /// Gets/sets a boolean indicating whether or not inbound cloud events should define a valid data schema
    /// </summary>
    [DefaultValue(true)]
    [DataMember(Order = 1, Name = "required"), JsonPropertyName("required"), YamlMember(Alias = "required")]
    public virtual bool Required { get; set; } = true;

    /// <summary>
    /// Gets/sets a boolean indicating whether or not schemas for unknown inbound cloud events for be automatically generated and registered in the application's schema registry
    /// </summary>
    [DataMember(Order = 2, Name = "autoGenerate"), JsonPropertyName("autoGenerate"), YamlMember(Alias = "autoGenerate")]
    public virtual bool AutoGenerate { get; set; }

}
