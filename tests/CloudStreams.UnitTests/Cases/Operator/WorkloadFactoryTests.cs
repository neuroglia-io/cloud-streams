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
using CloudStreams.Operator.Configuration;
using CloudStreams.Operator.Services;
using k8s;
using k8s.Models;
using System.Text.Json.Nodes;

namespace CloudStreams.UnitTests.Cases.Operator;

/// <summary>
/// Covers the objects the operator deploys a gateway or a broker with (see #279)
/// </summary>
public class WorkloadFactoryTests
{

    static readonly Dictionary<string, byte[]> Connections = new() { ["eventstore"] = "esdb://eventstore:2113"u8.ToArray(), ["redis"] = "redis:6379"u8.ToArray() };

    static WorkloadFactory CreateFactory(OperatorOptions? options = null) => new(options ?? new OperatorOptions() { Instance = "cs" }, new KubernetesResourceStoreOptions() { DefaultNamespace = "cloud-streams" });

    static ManagedResource Gateway(GatewayWorkloadConfiguration? workload = null, string @namespace = "tenant-a") => new()
    {
        Kind = WorkloadKind.Gateway,
        Name = "orders",
        Namespace = @namespace,
        Uid = "4d0c5b1e-0000-0000-0000-000000000001",
        Generation = 3,
        Workload = workload
    };

    [Fact]
    public void Every_Object_Should_Be_Owned_By_The_Resource_In_Its_Namespace()
    {
        var workload = CreateFactory().Create(Gateway(), Connections);
        var objects = new IKubernetesObject<V1ObjectMeta>[] { workload.ServiceAccount, workload.RoleBinding, workload.Connections, workload.Deployment, workload.Service, workload.PodDisruptionBudget! };
        foreach (var item in objects)
        {
            Assert.Equal("tenant-a", item.Metadata.NamespaceProperty);
            var owner = Assert.Single(item.Metadata.OwnerReferences);
            Assert.Equal(("cloud-streams.io/v1", "Gateway", "orders", "4d0c5b1e-0000-0000-0000-000000000001", true), (owner.ApiVersion, owner.Kind, owner.Name, owner.Uid, owner.Controller == true));
            Assert.Equal("cs", item.Metadata.Labels[WorkloadFactory.OperatorLabel]);
            Assert.Equal("orders", item.Metadata.Labels["cloud-streams.io/gateway"]);
        }
        Assert.Equal("orders-gateway", workload.Deployment.Metadata.Name);
        Assert.Equal("orders-gateway-connections", workload.Connections.Metadata.Name);
        Assert.Null(workload.Ingress);
    }

    [Fact]
    public void The_Workload_Should_Serve_Its_Namespace_Only_With_The_Kubernetes_Store()
    {
        var env = CreateFactory().Create(Gateway(), Connections).Deployment.Spec.Template.Spec.Containers.Single().Env.ToDictionary(e => e.Name);
        Assert.Equal("orders", env["CLOUDSTREAMS_GATEWAY_NAME"].Value);
        Assert.Equal("tenant-a", env["CLOUDSTREAMS_GATEWAY_NAMESPACE"].Value);
        Assert.Equal("kubernetes", env["CloudStreams__Resources__Store"].Value);
        Assert.Equal("tenant-a", env["CloudStreams__Resources__Kubernetes__Namespaces__0"].Value);
        Assert.Equal("false", env["CloudStreams__Resources__Kubernetes__CreateNamespaces"].Value);
        Assert.Equal("false", env["ManagementApi__Enabled"].Value);
        Assert.Equal(("orders-gateway-connections", "eventstore"), (env["ConnectionStrings__eventstore"].ValueFrom.SecretKeyRef.Name, env["ConnectionStrings__eventstore"].ValueFrom.SecretKeyRef.Key));
    }

    [Fact]
    public void The_Default_Namespace_Should_Be_Mapped_To_The_Default_Namespace_Of_Cloud_Streams()
    {
        var env = CreateFactory().Create(Gateway(@namespace: "cloud-streams"), Connections).Deployment.Spec.Template.Spec.Containers.Single().Env.ToDictionary(e => e.Name);
        Assert.Equal("default", env["CLOUDSTREAMS_GATEWAY_NAMESPACE"].Value);
        Assert.Equal("default", env["CloudStreams__Resources__Kubernetes__Namespaces__0"].Value);
    }

    [Fact]
    public void The_Workload_Configuration_Should_Override_The_Defaults()
    {
        // arrange
        var workload = new GatewayWorkloadConfiguration()
        {
            Replicas = 5,
            Resources = new() { Requests = new Dictionary<string, string>() { ["cpu"] = "250m" } },
            NodeSelector = new Dictionary<string, string>() { ["pool"] = "events" },
            Tolerations = [JsonNode.Parse("""{ "key": "dedicated", "operator": "Equal", "value": "events", "effect": "NoSchedule" }""")!.AsObject()],
            Affinity = JsonNode.Parse("""{ "podAntiAffinity": { "preferredDuringSchedulingIgnoredDuringExecution": [] } }""")!.AsObject(),
            PodLabels = new Dictionary<string, string>() { ["team"] = "orders", ["cloud-streams.io/gateway"] = "hijacked" },
            ManagementApi = true,
            Ingress = new() { Hosts = ["orders.example.com"], TlsSecretName = "orders-tls" }
        };

        // act
        var result = CreateFactory(new OperatorOptions() { Instance = "cs", Gateway = new() { Image = "gateway:1", IngressClassName = "nginx", IngressAnnotations = new() { ["a"] = "b" } } }).Create(Gateway(workload), Connections);

        // assert
        var pod = result.Deployment.Spec.Template;
        Assert.Equal(5, result.Deployment.Spec.Replicas);
        Assert.Equal("250m", pod.Spec.Containers.Single().Resources.Requests["cpu"].ToString());
        Assert.Equal("events", pod.Spec.NodeSelector["pool"]);
        Assert.Equal("dedicated", Assert.Single(pod.Spec.Tolerations).Key);
        Assert.NotNull(pod.Spec.Affinity.PodAntiAffinity);
        Assert.Equal("orders", pod.Metadata.Labels["team"]);
        // the labels the replicas are selected by can't be overridden
        Assert.Equal("orders", pod.Metadata.Labels["cloud-streams.io/gateway"]);
        Assert.Equal("true", pod.Spec.Containers.Single().Env.Single(e => e.Name == "ManagementApi__Enabled").Value);
        Assert.Equal("gateway:1", pod.Spec.Containers.Single().Image);
        var ingress = Assert.IsType<V1Ingress>(result.Ingress);
        Assert.Equal("nginx", ingress.Spec.IngressClassName);
        Assert.Equal("b", ingress.Metadata.Annotations["a"]);
        Assert.Equal("orders.example.com", Assert.Single(ingress.Spec.Rules).Host);
        Assert.Equal("orders-tls", Assert.Single(ingress.Spec.Tls).SecretName);
    }

    [Fact]
    public void Only_The_Operator_Should_Set_The_Environment()
    {
        var options = new OperatorOptions() { Instance = "cs", Broker = new() { Image = "broker:1", Env = [new("OTEL_EXPORTER_OTLP_ENDPOINT", "http://collector:4317"), new("CloudStreams__Resources__Store", "redis")] } };
        var broker = new ManagedResource() { Kind = WorkloadKind.Broker, Name = "orders", Namespace = "tenant-a", Uid = "uid" };
        var env = CreateFactory(options).Create(broker, Connections).Deployment.Spec.Template.Spec.Containers.Single().Env;
        Assert.Equal("http://collector:4317", env.Single(e => e.Name == "OTEL_EXPORTER_OTLP_ENDPOINT").Value);
        // the variables the operator sets can't be overridden by its configuration
        Assert.Equal("kubernetes", env.Single(e => e.Name == "CloudStreams__Resources__Store").Value);
        Assert.DoesNotContain(env, e => e.Name == "ManagementApi__Enabled");
    }

    [Fact]
    public void The_Replicas_Should_Restart_When_The_Connection_Strings_Change()
    {
        var factory = CreateFactory();
        var first = factory.Create(Gateway(), Connections).Deployment.Spec.Template.Metadata.Annotations["checksum/connections"];
        var second = factory.Create(Gateway(), new Dictionary<string, byte[]>(Connections) { ["redis"] = "redis-2:6379"u8.ToArray() }).Deployment.Spec.Template.Metadata.Annotations["checksum/connections"];
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Long_Names_Should_Be_Shortened_To_Valid_Names()
    {
        var name = WorkloadFactory.GetObjectName(WorkloadKind.Gateway, new string('a', 63));
        Assert.True(name.Length <= 63);
        Assert.NotEqual(name, WorkloadFactory.GetObjectName(WorkloadKind.Broker, new string('a', 63)));
    }

}
