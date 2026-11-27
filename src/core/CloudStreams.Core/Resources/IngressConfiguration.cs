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
/// Represents an object used to configure the Kubernetes ingress that exposes the public endpoints of a gateway
/// </summary>
/// <remarks>
/// The ingress' annotations are set by the operator's configuration only, since some ingress controllers read configuration snippets from them
/// </remarks>
[DataContract]
public record IngressConfiguration
{

    /// <summary>
    /// Gets/sets the name of the ingress class to use, if any. Defaults to the operator's configuration, or to the cluster's default class
    /// </summary>
    [DataMember(Order = 1, Name = "className"), JsonPropertyOrder(1), JsonPropertyName("className"), YamlMember(Order = 1, Alias = "className")]
    public virtual string? ClassName { get; set; }

    /// <summary>
    /// Gets/sets the hosts the gateway is exposed on, whose every path routes to it
    /// </summary>
    [Required, MinLength(1)]
    [DataMember(Order = 2, Name = "hosts"), JsonPropertyOrder(2), JsonPropertyName("hosts"), YamlMember(Order = 2, Alias = "hosts")]
    public virtual EquatableList<string> Hosts { get; set; } = [];

    /// <summary>
    /// Gets/sets the name of the Kubernetes secret, in the gateway's namespace, that holds the TLS certificate of the hosts, if any
    /// </summary>
    [DataMember(Order = 3, Name = "tlsSecretName"), JsonPropertyOrder(3), JsonPropertyName("tlsSecretName"), YamlMember(Order = 3, Alias = "tlsSecretName")]
    public virtual string? TlsSecretName { get; set; }

}
