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

namespace CloudStreams.Operator.Services;

/// <summary>
/// Computes the conditions of the workload of a gateway or a broker, from its deployment (see #279)
/// </summary>
public static class WorkloadStatusHelper
{

    /// <summary>
    /// Computes the conditions of a workload
    /// </summary>
    /// <param name="resource">The gateway or the broker whose workload to describe</param>
    /// <param name="deployment">The workload's deployment, if it could be applied</param>
    /// <param name="failure">The reason why the workload could not be applied, if it could not</param>
    /// <param name="now">The current date and time</param>
    /// <returns>The conditions of the workload, in a fixed order: Ready, Progressing and Degraded</returns>
    public static List<ResourceCondition> GetConditions(ManagedResource resource, V1Deployment? deployment, string? failure, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var generation = resource.Generation;
        ResourceCondition Condition(string type, bool? holds, string reason, string? message)
        {
            var status = holds switch { true => ResourceConditionStatus.True, false => ResourceConditionStatus.False, _ => ResourceConditionStatus.Unknown };
            var previous = resource.Conditions.FirstOrDefault(c => c.Type == type);
            // the transition time only changes with the status, as Kubernetes does
            var transition = previous != null && previous.Status == status ? previous.LastTransitionTime : now;
            return new(type, status, reason, message, generation, transition);
        }
        if (!string.IsNullOrWhiteSpace(failure) || deployment == null)
        {
            var message = failure ?? "The deployment of the workload could not be read";
            return
            [
                Condition(ResourceConditionType.Ready, false, "ApplyFailed", message),
                Condition(ResourceConditionType.Progressing, false, "ApplyFailed", message),
                Condition(ResourceConditionType.Degraded, true, "ApplyFailed", message)
            ];
        }
        var desired = deployment.Spec?.Replicas ?? 1;
        var status = deployment.Status;
        var available = status?.AvailableReplicas ?? 0;
        var updated = status?.UpdatedReplicas ?? 0;
        var observed = status?.ObservedGeneration is long observedGeneration && observedGeneration >= (deployment.Metadata?.Generation ?? 0);
        var progressing = status?.Conditions?.FirstOrDefault(c => c.Type == "Progressing");
        var replicaFailure = status?.Conditions?.FirstOrDefault(c => c.Type == "ReplicaFailure" && c.Status == "True");
        var deadlineExceeded = progressing?.Status == "False" && progressing.Reason == "ProgressDeadlineExceeded";
        var rolledOut = observed && updated >= desired && available >= desired && (status?.Replicas ?? 0) <= desired;
        var replicas = $"{available}/{desired} replicas available";
        var conditions = new List<ResourceCondition>()
        {
            desired == 0
                ? Condition(ResourceConditionType.Ready, true, "ScaledToZero", "The workload is scaled to 0 replicas")
                : rolledOut
                    ? Condition(ResourceConditionType.Ready, true, "Available", replicas)
                    : Condition(ResourceConditionType.Ready, false, available > 0 ? "PartiallyAvailable" : "Unavailable", replicas),
            rolledOut || deadlineExceeded
                ? Condition(ResourceConditionType.Progressing, false, deadlineExceeded ? "ProgressDeadlineExceeded" : "Complete", deadlineExceeded ? progressing!.Message : "The workload is rolled out")
                : Condition(ResourceConditionType.Progressing, true, "RollingOut", $"{updated}/{desired} replicas updated, {replicas}"),
            deadlineExceeded
                ? Condition(ResourceConditionType.Degraded, true, "ProgressDeadlineExceeded", progressing!.Message)
                : replicaFailure != null
                    ? Condition(ResourceConditionType.Degraded, true, replicaFailure.Reason ?? "ReplicaFailure", replicaFailure.Message)
                    : Condition(ResourceConditionType.Degraded, false, "AsExpected", null)
        };
        return conditions;
    }

    /// <summary>
    /// Determines whether the specified conditions differ from the ones the resource reports, ignoring their transition times
    /// </summary>
    /// <param name="resource">The resource whose reported conditions to compare</param>
    /// <param name="conditions">The conditions to compare</param>
    /// <param name="availableReplicas">The number of available replicas to compare</param>
    /// <returns>A boolean indicating whether the status must be updated</returns>
    public static bool HasChanged(ManagedResource resource, IReadOnlyList<ResourceCondition> conditions, int? availableReplicas)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(conditions);
        if (resource.AvailableReplicas != availableReplicas || resource.Conditions.Count != conditions.Count) return true;
        return conditions.Any(c => resource.Conditions.FirstOrDefault(r => r.Type == c.Type) is not ResourceCondition r
            || r.Status != c.Status || r.Reason != c.Reason || r.Message != c.Message || r.ObservedGeneration != c.ObservedGeneration);
    }

}
