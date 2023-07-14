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
/// Represents an object used to configure the ingestion of specific cloud events
/// </summary>
[DataContract]
public class CloudEventIngestionConfiguration
{

    /// <summary>
    /// Initializes a new <see cref="CloudEventIngestionConfiguration"/>
    /// </summary>
    public CloudEventIngestionConfiguration() { }

    /// <summary>
    /// Initializes a new <see cref="CloudEventIngestionConfiguration"/>
    /// </summary>
    /// <param name="source">The source of cloud events to configure the ingestion of. Supports regular expressions</param>
    /// <param name="type">The type of cloud events to configure the ingestion of. Supports regular expressions</param>
    /// <param name="metadata">An object used to configure the way the metadata of ingested cloud events should be resolved</param>
    public CloudEventIngestionConfiguration(string source, string type, CloudEventMetadataResolutionConfiguration? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(type)) throw new ArgumentNullException(nameof(type));
        this.Source = source;
        this.Type = type;
        this.Metadata = metadata;
    }

    /// <summary>
    /// Gets/sets the source of cloud events to configure the ingestion of. Supports regular expressions
    /// </summary>
    [Required, MinLength(1)]
    [DataMember(Order = 1, Name = "source", IsRequired = true), JsonPropertyName("source"), YamlMember(Alias = "source")]
    public virtual string Source { get; set; } = null!;

    /// <summary>
    /// Gets/sets the type of cloud events to configure the ingestion of. Supports regular expressions
    /// </summary>
    [Required, MinLength(1)]
    [DataMember(Order = 2, Name = "type", IsRequired = true), JsonPropertyName("type"), YamlMember(Alias = "type")]
    public virtual string Type { get; set; } = null!;

    /// <summary>
    /// Gets/sets an object used to configure the way the metadata of ingested cloud events should be resolved
    /// </summary>
    [DataMember(Order = 3, Name = "metadata", IsRequired = true), JsonPropertyName("metadata"), YamlMember(Alias = "metadata")]
    public virtual CloudEventMetadataResolutionConfiguration? Metadata { get; set; }

}
