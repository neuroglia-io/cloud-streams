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

using CloudStreams.Operator.Services;
using System.Text.Json.Nodes;
using JsonSerializer = Neuroglia.Serialization.Json.JsonSerializer;

namespace CloudStreams.UnitTests.Cases.Operator;

/// <summary>
/// Covers how the operator reads the gateways and brokers it deploys (see #279)
/// </summary>
public class ManagedResourceTests
{

    [Fact]
    public void A_Gateway_Should_Be_Read_With_Its_Workload_And_Status()
    {
        var node = JsonNode.Parse("""
        {
          "apiVersion": "cloud-streams.io/v1", "kind": "Gateway",
          "metadata": { "name": "orders", "namespace": "tenant-a", "uid": "u1", "generation": 7 },
          "spec": { "workload": { "replicas": 3, "managementApi": true, "ingress": { "hosts": [ "orders.example.com" ] }, "tolerations": [ { "key": "k" } ] } },
          "status": { "availableReplicas": 2, "conditions": [ { "type": "Ready", "status": "True", "reason": "Available", "observedGeneration": 7, "lastTransitionTime": "2026-11-27T10:00:00Z" } ] }
        }
        """)!.AsObject();
        var resource = ManagedResource.FromCustomObject(WorkloadKind.Gateway, node, JsonSerializer.Default);
        Assert.Equal(("orders", "tenant-a", "u1", 7ul, false, false), (resource.Name, resource.Namespace, resource.Uid, resource.Generation, resource.Deleting, resource.IsExternal));
        var workload = Assert.IsType<GatewayWorkloadConfiguration>(resource.Workload);
        Assert.Equal(3, workload.Replicas);
        Assert.True(workload.ManagementApi);
        Assert.Equal("orders.example.com", Assert.Single(workload.Ingress!.Hosts));
        Assert.Equal("k", workload.Tolerations![0]["key"]!.GetValue<string>());
        Assert.Equal(2, resource.AvailableReplicas);
        Assert.Equal("Available", Assert.Single(resource.Conditions).Reason);
    }

    [Fact]
    public void A_Resource_Whose_Workload_Is_External_Should_Be_Recognized()
    {
        var node = JsonNode.Parse("""
        { "metadata": { "name": "api", "namespace": "cloud-streams", "uid": "u2", "annotations": { "cloud-streams.io/workload": "external" }, "deletionTimestamp": "2026-11-27T10:00:00Z" }, "spec": {} }
        """)!.AsObject();
        var resource = ManagedResource.FromCustomObject(WorkloadKind.Broker, node, JsonSerializer.Default);
        Assert.True(resource.IsExternal);
        Assert.True(resource.Deleting);
        Assert.Null(resource.Workload);
    }

}
