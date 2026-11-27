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

using CloudStreams.Core.Api.Configuration;
using CloudStreams.Core.Api.Services;
using CloudStreams.Operator.Configuration;
using CloudStreams.Operator.Services;
using k8s.Models;

namespace CloudStreams.UnitTests.Cases.Operator;

/// <summary>
/// Covers how coordination and secrets work in Kubernetes mode: the names of the leases, the shared secrets, and what the operator tells the workloads (see #280, #281)
/// </summary>
public class CoordinationTests
{

    [Fact]
    public void Lease_Names_Should_Be_Valid_Names_Of_Kubernetes_Objects()
    {
        Assert.Equal("cloud-streams-health-gateway-orders.tenant-a", KubernetesHealthMonitorLeaseManager.GetLeaseName("gateway:orders.tenant-a"));
        var longName = KubernetesHealthMonitorLeaseManager.GetLeaseName("broker:" + new string('x', 300));
        Assert.True(longName.Length <= 253);
        Assert.NotEqual(longName, KubernetesHealthMonitorLeaseManager.GetLeaseName("broker:" + new string('x', 301)));
    }

    [Theory]
    [InlineData(null, "default", true)]
    [InlineData(null, "tenant-a", false)]
    [InlineData("tenant-a", "tenant-a", true)]
    [InlineData("*", "tenant-b", true)]
    public void Shared_Secrets_Should_Only_Be_Shared_With_The_Listed_Namespaces(string? sharedWith, string @namespace, bool shared)
    {
        var options = new SharedSecretOptions() { Enabled = true, SharedWith = sharedWith == null ? [] : [sharedWith] };
        Assert.Equal(shared, options.IsSharedWith(@namespace));
        Assert.False(new SharedSecretOptions() { SharedWith = ["*"] }.IsSharedWith(@namespace));
    }

    [Fact]
    public void The_Subjects_Of_A_Binding_Should_Only_Change_When_They_Differ()
    {
        Rbacv1Subject Subject(string name) => new("ServiceAccount", name, namespaceProperty: "tenant-a");
        Assert.Null(WorkloadReconciler.MergeSubjects([Subject("a")], [Subject("a")], false));
        Assert.Equal(["a", "b"], WorkloadReconciler.MergeSubjects([Subject("a")], [Subject("b")], false)!.Select(s => s.Name));
        Assert.Equal(["b"], WorkloadReconciler.MergeSubjects([Subject("a")], [Subject("b")], true)!.Select(s => s.Name));
        Assert.Null(WorkloadReconciler.MergeSubjects(null, [], true));
    }

    [Fact]
    public void Workloads_Should_Read_The_Secrets_Of_Their_Namespace_And_The_Shared_Ones_Of_The_Operator()
    {
        var factory = new WorkloadFactory(new OperatorOptions() { Instance = "cs", Namespace = "cloud-streams" }, new KubernetesResourceStoreOptions() { DefaultNamespace = "cloud-streams" });
        var resource = new ManagedResource() { Kind = WorkloadKind.Broker, Name = "orders", Namespace = "tenant-a", Uid = "uid" };
        var env = factory.Create(resource, new Dictionary<string, byte[]>() { ["eventstore"] = [], ["redis"] = [] }).Deployment.Spec.Template.Spec.Containers.Single().Env.ToDictionary(e => e.Name);
        Assert.Equal("true", env["CloudStreams__Secrets__Kubernetes__NamespacedSecrets"].Value);
        Assert.Equal("cloud-streams", env["CloudStreams__Secrets__Kubernetes__Namespace"].Value);
        Assert.Equal("cloud-streams", env["CloudStreams__Secrets__Kubernetes__DefaultNamespace"].Value);
        Assert.Equal("metadata.namespace", env["POD_NAMESPACE"].ValueFrom.FieldRef.FieldPath);
        Assert.Equal("metadata.name", env["POD_NAME"].ValueFrom.FieldRef.FieldPath);
    }

    [Fact]
    public void The_Namespace_Of_Coordination_Should_Be_That_Of_The_Monitored_Resource()
    {
        var options = new KubernetesResourceStoreOptions() { DefaultNamespace = "cloud-streams" };
        Assert.Equal("tenant-a", options.GetKubernetesNamespace("tenant-a"));
        Assert.Equal("cloud-streams", options.GetKubernetesNamespace("default"));
    }

}
