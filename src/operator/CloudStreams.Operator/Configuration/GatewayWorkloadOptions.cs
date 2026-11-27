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

namespace CloudStreams.Operator.Configuration;

/// <summary>
/// Represents the options of the workloads of gateways
/// </summary>
public class GatewayWorkloadOptions
    : WorkloadOptions
{

    /// <summary>
    /// Gets/sets the default ingress class of the gateways' ingresses, if any
    /// </summary>
    public virtual string? IngressClassName { get; set; }

    /// <summary>
    /// Gets/sets the annotations of the gateways' ingresses, which only the operator's configuration may set, since some ingress controllers read configuration
    /// snippets from them
    /// </summary>
    public virtual Dictionary<string, string> IngressAnnotations { get; set; } = [];

}
