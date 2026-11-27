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
using CloudStreams.Core.Application.Validation;
using CloudStreams.Operator.Admission;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Neuroglia.Data.PatchModel.Services;
using System.Text.Json.Nodes;
using JsonSerializer = Neuroglia.Serialization.Json.JsonSerializer;
using AdmissionReview = CloudStreams.Operator.Admission.AdmissionReview;

namespace CloudStreams.UnitTests.Cases.Operator;

/// <summary>
/// Covers the admission webhook, which validates the resources written to Kubernetes directly with the validators of the API (see #278)
/// </summary>
public class AdmissionWebhookTests
{

    readonly ICoreV1Operations _core = Substitute.For<ICoreV1Operations>();

    AdmissionWebhook CreateWebhook(params (string name, bool labelled)[] namespaces)
    {
        foreach (var (name, labelled) in namespaces)
        {
            var labels = labelled ? new Dictionary<string, string>() { ["cloud-streams.io/enabled"] = "true" } : null;
            this._core.ReadNamespaceWithHttpMessagesAsync(name, Arg.Any<bool?>(), Arg.Any<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), Arg.Any<CancellationToken>())
                .Returns(new HttpOperationResponse<V1Namespace>() { Body = new V1Namespace() { Metadata = new() { Name = name, Labels = labels } } });
        }
        var kubernetes = Substitute.For<IKubernetes>();
        kubernetes.CoreV1.Returns(this._core);
        var serializer = JsonSerializer.Default;
        var validator = new CloudStreamsResourceValidator(serializer, [new JsonMergePatchHandler(), new JsonPatchHandler()]);
        return new(NullLogger<AdmissionWebhook>.Instance, [validator], serializer, kubernetes, new KubernetesResourceStoreOptions() { DefaultNamespace = "cloud-streams" });
    }

    static AdmissionReview Review(string operation, string @namespace, string json, string? oldJson = null, string? subResource = null) => new()
    {
        Request = new()
        {
            Uid = "6f1b",
            Operation = operation,
            Resource = new() { Group = "cloud-streams.io", Version = "v1", Resource = "subscriptions" },
            SubResource = subResource,
            Name = "orders",
            Namespace = @namespace,
            UserInfo = new() { Username = "jane", Groups = ["system:authenticated"] },
            Object = JsonNode.Parse(json)!.AsObject(),
            OldObject = oldJson == null ? null : JsonNode.Parse(oldJson)!.AsObject()
        }
    };

    const string Valid = """
    {
      "apiVersion": "cloud-streams.io/v1", "kind": "Subscription",
      "metadata": { "name": "orders", "namespace": "tenant-a", "uid": "1", "managedFields": [ { "manager": "kubectl" } ] },
      "spec": { "subscriber": { "endpoint": "https://orders.example.com/events" } }
    }
    """;

    const string Invalid = """
    {
      "apiVersion": "cloud-streams.io/v1", "kind": "Subscription",
      "metadata": { "name": "orders", "namespace": "tenant-a" },
      "spec": { "subscriber": { "endpoint": "https://orders.example.com/events", "rateLimit": -1 } }
    }
    """;

    [Fact]
    public async Task A_Valid_Resource_Should_Be_Admitted()
    {
        var review = await this.CreateWebhook(("tenant-a", true)).ReviewAsync(Review("CREATE", "tenant-a", Valid));
        Assert.Equal("6f1b", review.Response!.Uid);
        Assert.True(review.Response.Allowed, review.Response.Status?.Message);
        Assert.Null(review.Request);
    }

    [Fact]
    public async Task An_Invalid_Resource_Should_Be_Refused_With_The_Errors_Of_The_Api()
    {
        var review = await this.CreateWebhook(("tenant-a", true)).ReviewAsync(Review("CREATE", "tenant-a", Invalid));
        Assert.False(review.Response!.Allowed);
        Assert.Equal(400, review.Response.Status!.Code);
        Assert.Contains("rateLimit", review.Response.Status.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_Invalid_Update_Should_Be_Refused()
    {
        var review = await this.CreateWebhook().ReviewAsync(Review("UPDATE", "tenant-a", Invalid, Valid));
        Assert.False(review.Response!.Allowed);
    }

    [Fact]
    public async Task A_Resource_Should_Only_Be_Created_In_A_Namespace_Of_Cloud_Streams()
    {
        var review = await this.CreateWebhook(("tenant-b", false)).ReviewAsync(Review("CREATE", "tenant-b", Valid.Replace("tenant-a", "tenant-b")));
        Assert.False(review.Response!.Allowed);
        Assert.Contains("cloud-streams.io/enabled=true", review.Response.Status!.Message);
    }

    [Fact]
    public async Task Status_Changes_And_Deletions_Should_Not_Be_Reviewed()
    {
        var webhook = this.CreateWebhook();
        Assert.True((await webhook.ReviewAsync(Review("UPDATE", "tenant-a", Invalid, Valid, "status"))).Response!.Allowed);
        Assert.True((await webhook.ReviewAsync(Review("DELETE", "tenant-a", Invalid))).Response!.Allowed);
    }

    [Fact]
    public async Task A_Failure_Should_Refuse_The_Resource()
    {
        // arrange: the namespace can't be read
        this._core.ReadNamespaceWithHttpMessagesAsync(Arg.Any<string>(), Arg.Any<bool?>(), Arg.Any<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), Arg.Any<CancellationToken>())
            .Returns<HttpOperationResponse<V1Namespace>>(_ => throw new HttpRequestException("connection refused"));
        var webhook = this.CreateWebhook();

        // act
        var review = await webhook.ReviewAsync(Review("CREATE", "tenant-a", Valid));

        // assert
        Assert.False(review.Response!.Allowed);
        Assert.Equal(500, review.Response.Status!.Code);
    }

}
