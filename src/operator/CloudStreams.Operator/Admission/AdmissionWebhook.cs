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
using k8s.Autorest;
using Neuroglia;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Data.Infrastructure.ResourceOriented.Services;
using Neuroglia.Security;
using Neuroglia.Serialization;

namespace CloudStreams.Operator.Admission;

/// <summary>
/// Validates the resources of Cloud Streams that are written to Kubernetes directly, e.g. with kubectl, with the validators the API uses (see #278)
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="validators">The validators of resources</param>
/// <param name="serializer">The service used to serialize and deserialize JSON</param>
/// <param name="kubernetes">The service used to interact with the Kubernetes API</param>
/// <param name="store">The options of the Kubernetes store of resources</param>
public class AdmissionWebhook(ILogger<AdmissionWebhook> logger, IEnumerable<IResourceValidator> validators, IJsonSerializer serializer, IKubernetes kubernetes, KubernetesResourceStoreOptions store)
{

    /// <summary>
    /// Reviews the specified request
    /// </summary>
    /// <param name="review">The review to answer</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The answered review</returns>
    public virtual async Task<AdmissionReview> ReviewAsync(AdmissionReview review, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(review);
        var request = review.Request ?? throw new ArgumentException("The review has no request", nameof(review));
        AdmissionResponse response;
        try { response = await this.ReviewAsync(request, cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // a failure is a refusal, as the webhook's 'Fail' policy would make it: an invalid resource is never admitted because of an error
            logger.LogError(ex, "Failed to review the {operation} of the {resource} '{name}' of the namespace '{namespace}'", request.Operation, request.Resource?.Resource, request.Name, request.Namespace);
            response = Refuse(request.Uid, (int)HttpStatusCode.InternalServerError, "InternalError", $"The resource could not be reviewed: {ex.Message}");
        }
        return new() { ApiVersion = review.ApiVersion, Kind = review.Kind, Response = response };
    }

    /// <summary>
    /// Reviews the specified request
    /// </summary>
    /// <param name="request">The request to review</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The response to the request</returns>
    protected virtual async Task<AdmissionResponse> ReviewAsync(AdmissionRequest request, CancellationToken cancellationToken)
    {
        var allowed = new AdmissionResponse() { Uid = request.Uid, Allowed = true };
        if (!string.IsNullOrWhiteSpace(request.SubResource) || request.Resource.Group != CloudStreamsDefaults.ResourceGroup) return allowed;
        Operation operation;
        switch (request.Operation)
        {
            case "CREATE": operation = Operation.Create; break;
            case "UPDATE": operation = Operation.Replace; break;
            default: return allowed;
        }
        var @namespace = this.FromKubernetesNamespace(request.Namespace);
        if (operation == Operation.Create && !string.IsNullOrWhiteSpace(request.Namespace))
        {
            // as the API does, resources may only be created in a namespace of Cloud Streams (see #277)
            var refusal = await this.CheckNamespaceAsync(request, cancellationToken).ConfigureAwait(false);
            if (refusal != null) return refusal;
        }
        var updated = this.ToResource(request.Object, @namespace);
        var original = this.ToResource(request.OldObject, @namespace);
        var name = request.Name ?? updated?.GetName() ?? string.Empty;
        var reference = new ResourceReference(new ResourceDefinitionReference(request.Resource.Group, request.Resource.Version, request.Resource.Resource), name, @namespace);
        var user = request.UserInfo?.Username == null ? null : new UserInfo(request.UserInfo.Username, "kubernetes", [.. (request.UserInfo.Groups ?? []).Select(g => new ClaimInfo() { Type = "groups", Value = g })]);
        var context = new AdmissionReviewRequest(request.Uid, operation, reference, null, null, updated, original, user, request.DryRun == true);
        foreach (var validator in validators.Where(v => v.AppliesTo(operation, request.Resource.Group, request.Resource.Version, request.Resource.Resource, @namespace)))
        {
            var result = await validator.ValidateAsync(context, cancellationToken).ConfigureAwait(false);
            if (result.Allowed) continue;
            var problem = result.Problem;
            var message = problem?.Detail ?? problem?.Title ?? $"The {request.Resource.Resource.TrimEnd('s')} '{name}' has been refused";
            return Refuse(request.Uid, problem?.Status is int status and >= 400 ? status : (int)HttpStatusCode.BadRequest, "Invalid", message);
        }
        return allowed;
    }

    /// <summary>
    /// Checks that the Kubernetes namespace of the specified request is a namespace of Cloud Streams
    /// </summary>
    /// <param name="request">The request to check</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The refusal of the request, if its namespace is not a namespace of Cloud Streams</returns>
    protected virtual async Task<AdmissionResponse?> CheckNamespaceAsync(AdmissionRequest request, CancellationToken cancellationToken)
    {
        V1Namespace kubernetesNamespace;
        try { kubernetesNamespace = await kubernetes.CoreV1.ReadNamespaceAsync(request.Namespace, cancellationToken: cancellationToken).ConfigureAwait(false); }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound) { return null; /* the API server refuses it anyway */ }
        if (kubernetesNamespace.Metadata.Labels?.TryGetValue(store.NamespaceLabel, out var value) == true && value == "true") return null;
        return Refuse(request.Uid, (int)HttpStatusCode.BadRequest, "Invalid",
            $"The namespace '{request.Namespace}' is not a namespace of Cloud Streams: create it with the API or the dashboard, or label it '{store.NamespaceLabel}=true'");
    }

    /// <summary>
    /// Gets the namespace of Cloud Streams the specified Kubernetes namespace is
    /// </summary>
    /// <param name="kubernetesNamespace">The Kubernetes namespace, if any</param>
    /// <returns>The namespace of Cloud Streams, if any</returns>
    protected virtual string? FromKubernetesNamespace(string? kubernetesNamespace) => string.IsNullOrWhiteSpace(kubernetesNamespace) ? null : kubernetesNamespace == store.DefaultNamespace ? Namespace.DefaultNamespaceName : kubernetesNamespace;

    /// <summary>
    /// Converts the specified custom object into a resource of Cloud Streams, in the namespace of Cloud Streams of its Kubernetes namespace
    /// </summary>
    /// <param name="node">The custom object to convert, if any</param>
    /// <param name="namespace">The namespace of Cloud Streams of the custom object, if any</param>
    /// <returns>The converted resource, if any</returns>
    protected virtual Resource? ToResource(JsonObject? node, string? @namespace)
    {
        if (node == null) return null;
        var copy = node.DeepClone().AsObject();
        if (copy["metadata"] is JsonObject metadata)
        {
            if (@namespace == null) metadata.Remove("namespace");
            else metadata["namespace"] = @namespace;
            // the fields Kubernetes manages are of no use to the validators, and some of them aren't part of the metadata of Cloud Streams
            metadata.Remove("managedFields");
        }
        return serializer.Deserialize<Resource>(copy.ToJsonString());
    }

    /// <summary>
    /// Creates a response that refuses a request
    /// </summary>
    /// <param name="uid">The unique identifier of the request</param>
    /// <param name="code">The HTTP status code to refuse the request with</param>
    /// <param name="reason">A machine-readable reason</param>
    /// <param name="message">A human-readable message, which kubectl prints</param>
    /// <returns>A new <see cref="AdmissionResponse"/></returns>
    protected static AdmissionResponse Refuse(string uid, int code, string reason, string message) => new()
    {
        Uid = uid,
        Allowed = false,
        Status = new() { Code = code, Reason = reason, Message = message }
    };

}
