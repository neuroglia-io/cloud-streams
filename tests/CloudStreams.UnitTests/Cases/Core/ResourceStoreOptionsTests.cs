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
using Microsoft.Extensions.Logging.Abstractions;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the configuration of the store of resources, Redis or Kubernetes, and how the Kubernetes store maps namespaces (see #275, #277)
/// </summary>
public class ResourceStoreOptionsTests
{

    [Theory]
    [InlineData(null, false)]
    [InlineData("redis", false)]
    [InlineData("Kubernetes", true)]
    [InlineData(" kubernetes ", true)]
    public void The_Store_Should_Be_Chosen_By_Configuration(string? store, bool usesKubernetes)
    {
        var options = store == null ? new ResourceStoreOptions() : new ResourceStoreOptions() { Store = store };
        options.Validate();
        Assert.Equal(usesKubernetes, options.UsesKubernetes);
    }

    [Fact]
    public void An_Unknown_Store_Should_Fail_Fast()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new ResourceStoreOptions() { Store = "etcd" }.Validate());
        Assert.Contains("'redis' or 'kubernetes'", exception.Message);
    }

    [Fact]
    public void An_Invalid_Default_Namespace_Should_Fail_Fast()
    {
        var options = new ResourceStoreOptions() { Store = ResourceStoreType.Kubernetes, Kubernetes = new() { DefaultNamespace = "Not_A_Namespace" } };
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Invalid_Served_Namespaces_Should_Fail_Fast()
    {
        var options = new ResourceStoreOptions() { Store = ResourceStoreType.Kubernetes, Kubernetes = new() { Namespaces = ["tenant-a", "Not_A_Namespace"] } };
        var exception = Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Contains("'Not_A_Namespace'", exception.Message);
        new ResourceStoreOptions() { Store = ResourceStoreType.Kubernetes, Kubernetes = new() { Namespaces = ["tenant-a", "default"] } }.Validate();
    }

    static KubernetesResourceDatabase NewRestrictedDatabase(k8s.IKubernetes kubernetes) => new(NullLogger<Neuroglia.Data.Infrastructure.ResourceOriented.Services.KubernetesDatabase>.Instance, kubernetes,
        Neuroglia.Serialization.Json.JsonSerializer.Default, [], new KubernetesResourceStoreOptions() { DefaultNamespace = "cloud-streams", Namespaces = ["tenant-a", "cloud-streams"] });

    [Fact]
    public void The_Served_Namespaces_Should_Be_Those_Configured()
    {
        // arrange - the default namespace may be listed by the name of its Kubernetes namespace
        var database = NewRestrictedDatabase(Substitute.For<k8s.IKubernetes>());

        // assert
        Assert.Equal(["tenant-a", "default"], database.ServedNamespaces);
        Assert.True(database.IsServed("default"));
        Assert.False(database.IsServed("tenant-b"));
        Assert.Null(new KubernetesResourceDatabase(NullLogger<Neuroglia.Data.Infrastructure.ResourceOriented.Services.KubernetesDatabase>.Instance, Substitute.For<k8s.IKubernetes>(),
            Neuroglia.Serialization.Json.JsonSerializer.Default, [], new KubernetesResourceStoreOptions()).ServedNamespaces);
    }

    [Fact]
    public async Task Resources_Of_Unserved_Namespaces_Should_Be_Refused_Without_Calling_The_API_Server()
    {
        // arrange
        var kubernetes = Substitute.For<k8s.IKubernetes>();
        var database = NewRestrictedDatabase(kubernetes);
        var group = CloudStreamsDefaults.Resources.Definitions.Subscription.Spec.Group;
        var plural = CloudStreamsDefaults.Resources.Definitions.Subscription.Spec.Names.Plural;

        // act
        var read = await Assert.ThrowsAsync<ProblemDetailsException>(() => database.GetResourceAsync(group, "v1", plural, "orders", "tenant-b"));
        var deleted = await Assert.ThrowsAsync<ProblemDetailsException>(() => database.DeleteResourceAsync(group, "v1", plural, "orders", "tenant-b"));
        var watched = await Assert.ThrowsAsync<ProblemDetailsException>(() => database.WatchResourcesAsync(group, "v1", plural, "tenant-b"));

        // assert - the problem names the option that restricts the process
        Assert.All([read, deleted, watched], ex => Assert.Equal((int)HttpStatusCode.Forbidden, ex.Problem.Status));
        Assert.Contains("CloudStreams:Resources:Kubernetes:Namespaces", read.Problem.Detail);
        Assert.Empty(kubernetes.ReceivedCalls());
    }

    [Theory]
    [InlineData("default", "cloud-streams")]
    [InlineData("tenant-a", "tenant-a")]
    [InlineData(null, null)]
    public void The_Default_Namespace_Should_Be_The_Configured_Kubernetes_Namespace(string? @namespace, string? kubernetesNamespace)
    {
        // arrange
        var database = new KubernetesResourceDatabase(NullLogger<Neuroglia.Data.Infrastructure.ResourceOriented.Services.KubernetesDatabase>.Instance, Substitute.For<k8s.IKubernetes>(),
            Neuroglia.Serialization.Json.JsonSerializer.Default, [], new KubernetesResourceStoreOptions() { DefaultNamespace = "cloud-streams" });

        // act & assert - both ways
        Assert.Equal(kubernetesNamespace, database.ToKubernetesNamespace(@namespace));
        Assert.Equal(@namespace, database.FromKubernetesNamespace(kubernetesNamespace));
    }

}
