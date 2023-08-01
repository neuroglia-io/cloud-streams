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
/// Represents an object used to configure the way a broker should sequence dispatched cloud events
/// </summary>
[DataContract]
public record CloudEventSequencingConfiguration
{

    /// <summary>
    /// Gets the default <see cref="CloudEventSequencingConfiguration"/>
    /// </summary>
    public static readonly CloudEventSequencingConfiguration Default = new()
    {
        Strategy = CloudEventSequencingStrategy.Attribute,
        AttributeName = CloudEventExtensionAttributes.Sequence,
        AttributeConflictResolution = CloudEventAttributeConflictResolution.Overwrite
    };

    /// <summary>
    /// Gets/sets the sequencing strategy to use
    /// </summary>
    /// <remarks>See <see cref="CloudEventSequencingStrategy"/></remarks>
    [Required, MinLength(3)]
    [DataMember(Order = 1, Name = "strategy", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("strategy"), YamlMember(Order = 1, Alias = "strategy")]
    public virtual string Strategy { get; set; } = null!;

    /// <summary>
    /// Gets/sets the name of the context attribute to store the CloudStreams sequence into
    /// </summary>
    [DataMember(Order = 2, Name = "attributeName"), JsonPropertyOrder(2), JsonPropertyName("attributeName"), YamlMember(Order = 2, Alias = "attributeName")]
    public virtual string? AttributeName { get; set; }

    /// <summary>
    /// Gets/sets the way to handle conflicts with existing attributes
    /// </summary>
    /// <remarks>See <see cref="CloudEventAttributeConflictResolution"/></remarks>
    [DataMember(Order = 3, Name = "attributeConflictResolution"), JsonPropertyOrder(3), JsonPropertyName("attributeConflictResolution"), YamlMember(Order = 3, Alias = "attributeConflictResolution")]
    public virtual string? AttributeConflictResolution { get; set; }

    /// <summary>
    /// Gets/sets the name of the context attribute to fallback to when the attribute specified by `attributeName` already exist
    /// </summary>
    [DataMember(Order = 4, Name = "fallbackAttributeName"), JsonPropertyOrder(4), JsonPropertyName("fallbackAttributeName"), YamlMember(Order = 4, Alias = "fallbackAttributeName")]
    public virtual string? FallbackAttributeName { get; set; }

}
