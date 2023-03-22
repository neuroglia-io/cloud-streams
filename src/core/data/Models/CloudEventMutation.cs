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
/// Represents an object used to configure how to mutate consumed cloud events
/// </summary>
[DataContract]
public class CloudEventMutation
{

    /// <summary>
    /// Gets/sets the mutation strategy to use
    /// </summary>
    [DataMember(Order = 1, Name = "type"), JsonPropertyName("type"), YamlMember(Alias = "type")]
    public virtual CloudEventMutationType Type { get; set; }

    /// <summary>
    /// Gets/sets the runtime expression string or object used to mutate consumed cloud events. 
    /// Required if 'strategy' has been set to 'expression'
    /// </summary>
    [DataMember(Order = 2, Name = "expression"), JsonPropertyName("expression"), YamlMember(Alias = "expression")]
    public virtual object? Expression { get; set; }

    /// <summary>
    /// Gets/sets the runtime expression string or object used to mutate consumed cloud events. 
    /// Required if 'strategy' has been set to 'webhook'
    /// </summary>
    [DataMember(Order = 3, Name = "webhook"), JsonPropertyName("webhook"), YamlMember(Alias = "webhook")]
    public virtual Webhook? Webhook { get; set; }

}
