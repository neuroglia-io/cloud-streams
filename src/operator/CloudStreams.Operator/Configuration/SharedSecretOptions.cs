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

using CloudStreams.Core.Transports.Secrets;

namespace CloudStreams.Operator.Configuration;

/// <summary>
/// Represents the options of the shared secrets, those of the operator's namespace, which the workloads of the namespaces they're shared with may read (see #281)
/// </summary>
public class SharedSecretOptions
{

    /// <summary>
    /// Gets/sets a boolean indicating whether or not the workloads read secrets from Kubernetes, in which case the operator lets the workloads of the namespaces
    /// of <see cref="SharedWith"/> read the shared secrets
    /// </summary>
    public virtual bool Enabled { get; set; }

    /// <summary>
    /// Gets/sets the name of the RoleBinding, in the operator's namespace, whose subjects the operator sets to the service accounts of those workloads. It is
    /// installed by the chart, and binds the role that grants the reading of the shared secrets
    /// </summary>
    public virtual string RoleBinding { get; set; } = "cloud-streams-shared-secrets";

    /// <summary>
    /// Gets/sets the namespaces of Cloud Streams the shared secrets are shared with: '*' for every namespace. Defaults to the 'default' namespace, as the secret
    /// providers do
    /// </summary>
    public virtual List<string> SharedWith { get; set; } = [];

    /// <summary>
    /// Determines whether the shared secrets are shared with the specified namespace of Cloud Streams
    /// </summary>
    /// <param name="namespace">The namespace of Cloud Streams to check</param>
    /// <returns>A boolean indicating whether the shared secrets are shared with the namespace</returns>
    public virtual bool IsSharedWith(string @namespace) => this.Enabled && new SecretOptions() { SharedWith = this.SharedWith }.IsSharedWith(@namespace);

}
