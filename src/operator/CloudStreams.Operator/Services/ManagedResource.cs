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

using Neuroglia.Serialization;

namespace CloudStreams.Operator.Services;

/// <summary>
/// Represents a gateway or a broker, as the operator reads it from Kubernetes: what it needs to deploy and describe its workload
/// </summary>
public record ManagedResource
{

    /// <summary>
    /// Gets the kind of the resource
    /// </summary>
    public required WorkloadKind Kind { get; init; }

    /// <summary>
    /// Gets the name of the resource
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the Kubernetes namespace of the resource, which its workload is deployed to
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// Gets the unique identifier of the resource, which its workload's objects are owned by
    /// </summary>
    public required string Uid { get; init; }

    /// <summary>
    /// Gets the generation of the resource's spec
    /// </summary>
    public ulong Generation { get; init; }

    /// <summary>
    /// Gets a boolean indicating whether or not the resource is being deleted
    /// </summary>
    public bool Deleting { get; init; }

    /// <summary>
    /// Gets the annotations of the resource
    /// </summary>
    public IReadOnlyDictionary<string, string> Annotations { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets the configuration of the resource's workload, if any. A <see cref="GatewayWorkloadConfiguration"/> for gateways
    /// </summary>
    public WorkloadConfiguration? Workload { get; init; }

    /// <summary>
    /// Gets the conditions the operator last reported, if any
    /// </summary>
    public IReadOnlyList<ResourceCondition> Conditions { get; init; } = [];

    /// <summary>
    /// Gets the number of available replicas the operator last reported, if any
    /// </summary>
    public int? AvailableReplicas { get; init; }

    /// <summary>
    /// Gets a boolean indicating whether or not the workload of the resource is deployed by something else than the operator, e.g. the Helm chart (see
    /// <see cref="CloudStreamsDefaults.Resources.WorkloadAnnotation"/>)
    /// </summary>
    public bool IsExternal => this.Annotations.TryGetValue(CloudStreamsDefaults.Resources.WorkloadAnnotation, out var value) && value == CloudStreamsDefaults.Resources.ExternalWorkload;

    /// <summary>
    /// Reads a <see cref="ManagedResource"/> from the specified custom object
    /// </summary>
    /// <param name="kind">The kind of the resource</param>
    /// <param name="node">The custom object to read</param>
    /// <param name="serializer">The service used to deserialize the workload's configuration and the status</param>
    /// <returns>A new <see cref="ManagedResource"/></returns>
    public static ManagedResource FromCustomObject(WorkloadKind kind, JsonObject node, IJsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(serializer);
        var metadata = node["metadata"] as JsonObject ?? throw new InvalidOperationException("The custom object has no metadata");
        var workload = node["spec"]?["workload"];
        var status = node["status"];
        var conditions = status?["conditions"];
        return new()
        {
            Kind = kind,
            Name = metadata["name"]!.GetValue<string>(),
            Namespace = metadata["namespace"]!.GetValue<string>(),
            Uid = metadata["uid"]?.GetValue<string>() ?? string.Empty,
            Generation = metadata["generation"]?.GetValue<ulong>() ?? 0,
            Deleting = metadata["deletionTimestamp"] != null,
            Annotations = (metadata["annotations"] as JsonObject)?.ToDictionary(a => a.Key, a => a.Value?.GetValue<string>() ?? string.Empty) ?? [],
            Workload = workload == null ? null : kind == WorkloadKind.Gateway
                ? serializer.Deserialize<GatewayWorkloadConfiguration>(workload.ToJsonString())
                : serializer.Deserialize<WorkloadConfiguration>(workload.ToJsonString()),
            Conditions = conditions == null ? [] : serializer.Deserialize<List<ResourceCondition>>(conditions.ToJsonString()) ?? [],
            AvailableReplicas = status?["availableReplicas"]?.GetValue<int>()
        };
    }

}
