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
