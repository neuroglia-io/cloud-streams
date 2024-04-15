// Copyright © 2024-Present The Cloud Streams Authors
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

namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure a cloud event context attribute filter.
/// </summary>
[DataContract]
public record CloudEventAttributeFilter
{

    /// <summary>
    /// Initializes a new <see cref="CloudEventAttributeFilter"/>.
    /// </summary>
    public CloudEventAttributeFilter() { }

    /// <summary>
    /// Initializes a new <see cref="CloudEventAttributeFilter"/>.
    /// </summary>
    /// <param name="name">The name of the cloud event context attribute to filter.</param>
    /// <param name="value">The value of the cloud event context attribute to filter. Not setting any value configures the filter to only check if cloud events defined the attribute, no matter its value.</param>
    public CloudEventAttributeFilter(string name, string? value = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        this.Name = name;
        this.Value = value;
    }

    /// <summary>
    /// Gets/sets the name of the cloud event context attribute to filter.
    /// </summary>
    [Required, MinLength(1)]
    [DataMember(Order = 1, Name = "name", IsRequired = true), JsonPropertyName("name"), YamlMember(Alias = "name")]
    public virtual string Name { get; set; } = null!;

    /// <summary>
    /// Gets/sets the value of the cloud event context attribute to filter. Not setting any value configures the filter to only check if cloud events defined the attribute, no matter its value.
    /// </summary>
    [DataMember(Order = 2, Name = "value", IsRequired = true), JsonPropertyName("value"), YamlMember(Alias = "value")]
    public virtual string? Value { get; set; }

    /// <inheritdoc/>
    public override string ToString() => string.IsNullOrWhiteSpace(this.Value) ? this.Name : $"{this.Name}={this.Value}";

}