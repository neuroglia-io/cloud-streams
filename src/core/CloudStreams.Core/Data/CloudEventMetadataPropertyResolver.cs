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
/// Represents an object used to configure how to resolve the value of a cloud event metadata property
/// </summary>
[DataContract]
public record CloudEventMetadataPropertyResolver
{

    /// <summary>
    /// Initializes a new <see cref="CloudEventMetadataPropertyResolver"/>
    /// </summary>
    public CloudEventMetadataPropertyResolver() { }

    /// <summary>
    /// Initializes a new <see cref="CloudEventMetadataPropertyResolver"/>
    /// </summary>
    /// <param name="name">The  name of the cloud event metadata property to resolve</param>
    /// <param name="attribute">An object used to configure the cloud event context attribute to extract the metadata property from</param>
    public CloudEventMetadataPropertyResolver(string name, CloudEventAttributeFilter attribute)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        this.Name = name;
        this.Strategy = CloudEventMetadataPropertyResolutionStrategy.Attribute;
        this.Attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
    }

    /// <summary>
    /// Initializes a new <see cref="CloudEventMetadataPropertyResolver"/>
    /// </summary>
    /// <param name="name">The  name of the cloud event metadata property to resolve</param>
    /// <param name="expression">A runtime expression used to resolve the cloud event metadata property</param>
    public CloudEventMetadataPropertyResolver(string name, string expression)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentNullException(nameof(expression));
        this.Name = name;
        this.Strategy = CloudEventMetadataPropertyResolutionStrategy.Expression;
        this.Expression = expression;
    }

    /// <summary>
    /// Gets/sets the name of the cloud event metadata property to resolve
    /// </summary>
    /// <remarks>See <see cref="CloudEventMetadataProperties"/></remarks>
    [Required, MinLength(3)]
    [DataMember(Order = 1, Name = "name"), JsonPropertyName("name"), YamlMember(Alias = "name")]
    public virtual string Name { get; set; } = null!;

    /// <summary>
    /// Gets/sets the strategy to use to resolve the cloud event's metadata property
    /// </summary>
    [Required]
    [DataMember(Order = 2, Name = "strategy"), JsonPropertyName("strategy"), YamlMember(Alias = "strategy")]
    public virtual CloudEventMetadataPropertyResolutionStrategy Strategy { get; set; }

    /// <summary>
    /// Gets/sets an object used to configure the cloud event context attribute to extract the metadata property from<para></para>
    /// Required if strategy has been set to <see cref="CloudEventMetadataPropertyResolutionStrategy.Attribute"/>
    /// </summary>
    [DataMember(Order = 3, Name = "attribute"), JsonPropertyName("attribute"), YamlMember(Alias = "attribute")]
    public virtual CloudEventAttributeFilter? Attribute { get; set; }

    /// <summary>
    /// Gets/sets a runtime expression used to resolve the cloud event metadata property<para></para>
    /// Required if strategy has been set to <see cref="CloudEventMetadataPropertyResolutionStrategy.Expression"/>
    /// </summary>
    [DataMember(Order = 4, Name = "expression"), JsonPropertyName("expression"), YamlMember(Alias = "expression")]
    public virtual string? Expression { get; set; }

}
