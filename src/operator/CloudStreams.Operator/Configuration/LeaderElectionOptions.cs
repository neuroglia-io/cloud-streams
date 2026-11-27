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
/// Represents the options of the election of the replica of the operator that reconciles workloads and issues certificates, with a Kubernetes lease
/// </summary>
public class LeaderElectionOptions
{

    /// <summary>
    /// Gets/sets the name of the lease, in the operator's namespace
    /// </summary>
    public virtual string LeaseName { get; set; } = "cloud-streams-operator";

    /// <summary>
    /// Gets/sets how long a leader holds the lease without renewing it, after which another replica may take it
    /// </summary>
    public virtual TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets/sets how long the leader tries to renew the lease before giving it up
    /// </summary>
    public virtual TimeSpan RenewDeadline { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets/sets the interval at which replicas try to acquire or renew the lease
    /// </summary>
    public virtual TimeSpan RetryPeriod { get; set; } = TimeSpan.FromSeconds(2);

}
