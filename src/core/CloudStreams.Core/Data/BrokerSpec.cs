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
/// Represents an object used to configure a cloud event broker
/// </summary>
[DataContract]
public record BrokerSpec
{

    /// <summary>
    /// Initializes a new <see cref="BrokerSpec"/>
    /// </summary>
    public BrokerSpec() { }

    /// <summary>
    /// Initializes a new <see cref="BrokerSpec"/>
    /// </summary>
    /// <param name="dispatch">An object used to configure the way the broker should dispatch cloud events</param>
    public BrokerSpec(BrokerDispatchConfiguration dispatch)
    {
        this.Dispatch = dispatch;
    }

    /// <summary>
    /// Gets/sets an object used to configure the way the broker should dispatch cloud events
    /// </summary>
    [DataMember(Order = 1, Name = "dispatch"), JsonPropertyOrder(1), JsonPropertyName("dispatch"), YamlMember(Order = 1, Alias = "dispatch")]
    public virtual BrokerDispatchConfiguration? Dispatch { get; set; }

    /// <summary>
    /// Gets/sets a key/value mapping of the labels to select subscriptions by.<para></para>
    /// If not set, the broker will attempt to pick up all inactive subscriptions
    /// </summary>
    [DataMember(Order = 2, Name = "selector"), JsonPropertyOrder(2), JsonPropertyName("selector"), YamlMember(Order = 2, Alias = "selector")]
    public virtual IDictionary<string, string>? Selector { get; set; }

    /// <summary>
    /// Gets/sets an object used to configure the broker service, if any
    /// </summary>
    [DataMember(Order = 3, Name = "service"), JsonPropertyOrder(3), JsonPropertyName("service"), YamlMember(Order = 3, Alias = "service")]
    public virtual ServiceConfiguration? Service { get; set; }

}
