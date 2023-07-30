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
    public virtual BrokerDispatchConfiguration? Dispatch { get; set; } = null!;

    /// <summary>
    /// Gets/sets an object used to configure the broker service, if any
    /// </summary>
    [DataMember(Order = 2, Name = "service"), JsonPropertyOrder(2), JsonPropertyName("service"), YamlMember(Order = 2, Alias = "service")]
    public virtual ServiceConfiguration? Service { get; set; }

}
