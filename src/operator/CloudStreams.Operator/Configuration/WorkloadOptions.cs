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
/// Represents the options of the workloads of a kind of resource, gateways or brokers, which the operator deploys. The settings of a resource's
/// <see cref="WorkloadConfiguration"/> override the defaults set here, except those only the operator may set: the image, the environment and the security contexts
/// </summary>
public class WorkloadOptions
{

    /// <summary>
    /// Gets/sets the image the workloads run
    /// </summary>
    public virtual string Image { get; set; } = null!;

    /// <summary>
    /// Gets/sets the pull policy of the image
    /// </summary>
    public virtual string ImagePullPolicy { get; set; } = "IfNotPresent";

    /// <summary>
    /// Gets/sets the names of the secrets the image is pulled with, which must exist in the namespace of each workload
    /// </summary>
    public virtual List<string> ImagePullSecrets { get; set; } = [];

    /// <summary>
    /// Gets/sets the default number of replicas of each workload
    /// </summary>
    public virtual int Replicas { get; set; } = 2;

    /// <summary>
    /// Gets/sets the port the workloads serve HTTP on
    /// </summary>
    public virtual int Port { get; set; } = 8080;

    /// <summary>
    /// Gets/sets the environment variables of the workloads, e.g. the telemetry endpoint, the retention policy or the secret providers, which only the
    /// operator's configuration may set
    /// </summary>
    public virtual List<V1EnvVar> Env { get; set; } = [];

    /// <summary>
    /// Gets/sets the default compute resources of each replica, if any
    /// </summary>
    public virtual WorkloadResourceRequirements? Resources { get; set; }

    /// <summary>
    /// Gets/sets the security context of the pods
    /// </summary>
    public virtual V1PodSecurityContext PodSecurityContext { get; set; } = new()
    {
        RunAsNonRoot = true,
        RunAsUser = 1654,
        RunAsGroup = 1654,
        FsGroup = 1654,
        SeccompProfile = new() { Type = "RuntimeDefault" }
    };

    /// <summary>
    /// Gets/sets the security context of the containers
    /// </summary>
    public virtual V1SecurityContext SecurityContext { get; set; } = new()
    {
        AllowPrivilegeEscalation = false,
        Capabilities = new() { Drop = ["ALL"] }
    };

    /// <summary>
    /// Gets/sets a boolean indicating whether or not each workload gets a pod disruption budget
    /// </summary>
    public virtual bool PodDisruptionBudget { get; set; } = true;

    /// <summary>
    /// Gets/sets the maximum number of unavailable replicas of the pod disruption budget of each workload
    /// </summary>
    public virtual int MaxUnavailable { get; set; } = 1;

}
