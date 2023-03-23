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
/// Represents an object used to configure the way the metadata of ingested cloud events should be resolved
/// </summary>
[DataContract]
public class CloudEventMetadataResolutionConfiguration
{

    /// <summary>
    /// Initializes a new <see cref="CloudEventMetadataResolutionConfiguration"/>
    /// </summary>
    public CloudEventMetadataResolutionConfiguration() { }

    /// <summary>
    /// Initializes a new <see cref="CloudEventMetadataResolutionConfiguration"/>
    /// </summary>
    /// <param name="properties">A list containing the configuration of the resolution of a cloud event's metadata properties</param>
    public CloudEventMetadataResolutionConfiguration(IEnumerable<CloudEventMetadataPropertyResolver> properties)
    {
        this.Properties = properties?.ToList();
    }

    /// <summary>
    /// Gets/sets a list containing the configuration of the resolution of a cloud event's metadata properties
    /// </summary>
    [DataMember(Order = 1, Name = "properties"), JsonPropertyName("properties"), YamlMember(Alias = "properties")]
    public virtual List<CloudEventMetadataPropertyResolver>? Properties { get; set; }

}
