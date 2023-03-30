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
/// Represents an object used to configure a cloud event broker
/// </summary>
[DataContract]
public class BrokerSpec
{

    /// <summary>
    /// Initializes a new <see cref="BrokerSpec"/>
    /// </summary>
    public BrokerSpec() { }

    /// <summary>
    /// Initializes a new <see cref="BrokerSpec"/>
    /// </summary>
    /// <param name="network">The name of the network the broker belongs to</param>
    /// <param name="dispatch">An object used to configure the way the broker should dispatch cloud events</param>
    public BrokerSpec(string network, BrokerDispatchConfiguration dispatch)
    {
        if (string.IsNullOrWhiteSpace(network)) throw new ArgumentNullException(nameof(network));
        this.Network = network;
        this.Dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
    }

    /// <summary>
    /// Gets/sets the name of the network the broker belongs to
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "network", IsRequired = true), JsonPropertyName("network"), YamlMember(Alias = "network")]
    public virtual string Network { get; set; } = null!;

    /// <summary>
    /// Gets/sets an object used to configure the way the broker should dispatch cloud events
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 2, Name = "dispatch", IsRequired = true), JsonPropertyName("dispatch"), YamlMember(Alias = "dispatch")]
    public virtual BrokerDispatchConfiguration Dispatch { get; set; } = null!;

}
