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
/// Represents the compute resources a replica of a workload requests and is limited to, as Kubernetes quantities, e.g. '500m' or '256Mi'
/// </summary>
[DataContract]
public record WorkloadResourceRequirements
{

    /// <summary>
    /// Gets/sets the resources each replica requests, keyed by name, e.g. 'cpu' or 'memory'
    /// </summary>
    [DataMember(Order = 1, Name = "requests"), JsonPropertyOrder(1), JsonPropertyName("requests"), YamlMember(Order = 1, Alias = "requests")]
    public virtual IDictionary<string, string>? Requests { get; set; }

    /// <summary>
    /// Gets/sets the resources each replica is limited to, keyed by name, e.g. 'cpu' or 'memory'
    /// </summary>
    [DataMember(Order = 2, Name = "limits"), JsonPropertyOrder(2), JsonPropertyName("limits"), YamlMember(Order = 2, Alias = "limits")]
    public virtual IDictionary<string, string>? Limits { get; set; }

}
