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
/// Represents an object used to configure a cloud event filter
/// </summary>
[DataContract]
public class CloudEventFilter
{

    /// <summary>
    /// Gets/sets the filte's type
    /// </summary>
    [DataMember(Order = 1, Name = "type"), JsonPropertyName("type"), YamlMember(Alias = "type")]
    public virtual CloudEventFilterType Type { get; set; }

    /// <summary>
    /// Gets/sets a key/value mapping of the context attributes by which to filter consumed cloud events.
    /// Required if 'strategy' has been set to 'attributes'
    /// Values support regular and runtime expressions. 
    /// If no value has been supplied for a given key, it will match cloud events that define said attribute, no matter its value
    /// </summary>
    [DataMember(Order = 2, Name = "attributes"), JsonPropertyName("attributes"), YamlMember(Alias = "attributes")]
    public virtual IDictionary<string, string>? Attributes { get; set; } = null!;

    /// <summary>
    /// Gets/sets the runtime expression based condition to evaluate consumed cloud events against
    /// Required if 'strategy' has been set to 'expression'
    /// </summary>
    [DataMember(Order = 2, Name = "expression"), JsonPropertyName("expression"), YamlMember(Alias = "expression")]
    public virtual string? Expression { get; set; }

}
