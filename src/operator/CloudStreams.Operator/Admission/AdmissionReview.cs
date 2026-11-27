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

using System.Text.Json.Serialization;

namespace CloudStreams.Operator.Admission;

/// <summary>
/// Represents an 'admission.k8s.io/v1' AdmissionReview, which the API server sends to admission webhooks, and which they answer with
/// </summary>
public record AdmissionReview
{

    /// <summary>
    /// Gets/sets the API version of the review
    /// </summary>
    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = "admission.k8s.io/v1";

    /// <summary>
    /// Gets/sets the kind of the review
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "AdmissionReview";

    /// <summary>
    /// Gets/sets the request to review, sent by the API server
    /// </summary>
    [JsonPropertyName("request")]
    public AdmissionRequest? Request { get; set; }

    /// <summary>
    /// Gets/sets the webhook's response
    /// </summary>
    [JsonPropertyName("response")]
    public AdmissionResponse? Response { get; set; }

}

/// <summary>
/// Represents the request of an <see cref="AdmissionReview"/>
/// </summary>
public record AdmissionRequest
{

    /// <summary>
    /// Gets/sets the request's unique identifier, which the response must echo
    /// </summary>
    [JsonPropertyName("uid")]
    public string Uid { get; set; } = null!;

    /// <summary>
    /// Gets/sets the resource the request is about
    /// </summary>
    [JsonPropertyName("resource")]
    public AdmissionResource Resource { get; set; } = null!;

    /// <summary>
    /// Gets/sets the sub-resource the request is about, if any, e.g. 'status'
    /// </summary>
    [JsonPropertyName("subResource")]
    public string? SubResource { get; set; }

    /// <summary>
    /// Gets/sets the name of the object the request is about
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets/sets the Kubernetes namespace of the object the request is about, if any
    /// </summary>
    [JsonPropertyName("namespace")]
    public string? Namespace { get; set; }

    /// <summary>
    /// Gets/sets the operation: 'CREATE', 'UPDATE', 'DELETE' or 'CONNECT'
    /// </summary>
    [JsonPropertyName("operation")]
    public string Operation { get; set; } = null!;

    /// <summary>
    /// Gets/sets information about the user that sent the request
    /// </summary>
    [JsonPropertyName("userInfo")]
    public AdmissionUserInfo? UserInfo { get; set; }

    /// <summary>
    /// Gets/sets the object as the operation would leave it, if any
    /// </summary>
    [JsonPropertyName("object")]
    public JsonObject? Object { get; set; }

    /// <summary>
    /// Gets/sets the object as it is, for updates and deletions
    /// </summary>
    [JsonPropertyName("oldObject")]
    public JsonObject? OldObject { get; set; }

    /// <summary>
    /// Gets/sets a boolean indicating whether or not the request is a dry run
    /// </summary>
    [JsonPropertyName("dryRun")]
    public bool? DryRun { get; set; }

}

/// <summary>
/// Represents the resource of an <see cref="AdmissionRequest"/>
/// </summary>
public record AdmissionResource
{

    /// <summary>
    /// Gets/sets the API group of the resource
    /// </summary>
    [JsonPropertyName("group")]
    public string Group { get; set; } = string.Empty;

    /// <summary>
    /// Gets/sets the version of the resource
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = null!;

    /// <summary>
    /// Gets/sets the plural name of the resource
    /// </summary>
    [JsonPropertyName("resource")]
    public string Resource { get; set; } = null!;

}

/// <summary>
/// Represents information about the user that sent an <see cref="AdmissionRequest"/>
/// </summary>
public record AdmissionUserInfo
{

    /// <summary>
    /// Gets/sets the user's name
    /// </summary>
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    /// <summary>
    /// Gets/sets the user's groups
    /// </summary>
    [JsonPropertyName("groups")]
    public List<string>? Groups { get; set; }

}

/// <summary>
/// Represents the response of an <see cref="AdmissionReview"/>
/// </summary>
public record AdmissionResponse
{

    /// <summary>
    /// Gets/sets the unique identifier of the request the response answers
    /// </summary>
    [JsonPropertyName("uid")]
    public string Uid { get; set; } = null!;

    /// <summary>
    /// Gets/sets a boolean indicating whether or not the request is allowed
    /// </summary>
    [JsonPropertyName("allowed")]
    public bool Allowed { get; set; }

    /// <summary>
    /// Gets/sets the reason why the request is refused, if it is, which kubectl prints
    /// </summary>
    [JsonPropertyName("status")]
    public AdmissionStatus? Status { get; set; }

}

/// <summary>
/// Represents the status of a refused <see cref="AdmissionRequest"/>, a subset of a Kubernetes Status
/// </summary>
public record AdmissionStatus
{

    /// <summary>
    /// Gets/sets the HTTP status code the API server answers the request with
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>
    /// Gets/sets a machine-readable reason, e.g. 'Invalid'
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>
    /// Gets/sets a human-readable message
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

}
