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

using Microsoft.Extensions.Logging;
using Neuroglia;
using Neuroglia.Data;
using Neuroglia.Serialization;
using System.Net.Mime;
using System.Text;

namespace CloudStreams.Core.Api.Client.Services;

/// <summary>
/// Represents the default implementation of the <see cref="IResourceManagementApi{TResource}"/>
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/>s to manage</typeparam>
/// <param name="logger">The service used to perform logging</param>
/// <param name="serializer">The service used to serialize/deserialize objects from/to JSON</param>
/// <param name="httpClient">The service used to perform HTTP requests</param>
/// <param name="path">The path to the API used to manage <see cref="IResource"/>s of the specified type</param>
public class ResourceManagementApi<TResource>(ILogger<ResourceManagementApi<TResource>> logger, IJsonSerializer serializer, HttpClient httpClient, string path)
    : IResourceManagementApi<TResource>
    where TResource : IResource, new()
{

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; } = logger;

    /// <summary>
    /// Gets the service used to serialize/deserialize objects from/to JSON
    /// </summary>
    protected IJsonSerializer Serializer { get; } = serializer;

    /// <summary>
    /// Gets the service used to perform HTTP requests
    /// </summary>
    protected HttpClient HttpClient { get; } = httpClient;

    /// <summary>
    /// Gets the path to the API used to manage <see cref="IResource"/>s of the specified type
    /// </summary>
    protected string Path { get; } = CloudStreamsCoreApiClient.ResourceManagementApiPath + $"{new TResource().GetVersion()}/{path}";

    /// <summary>
    /// Gets the uri of the specified resource
    /// </summary>
    /// <param name="name">The name of the resource to get the uri of</param>
    /// <returns>The uri of the specified resource</returns>
    /// <remarks>Cloud Streams resources are cluster-scoped, so their uri never includes a namespace</remarks>
    protected virtual string GetResourceUri(string name) => $"{this.Path}/{Uri.EscapeDataString(name)}";

    /// <inheritdoc/>
    public virtual async Task<TResource> CreateAsync(TResource resource, CancellationToken cancellationToken = default)
    {
        if (resource == null) throw new ArgumentNullException(nameof(resource));
        var json = this.Serializer.SerializeToText(resource);
        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Post, this.Path) { Content = content }, cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<TResource>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task<ResourceDefinition> GetDefinitionAsync(CancellationToken cancellationToken = default)
    {
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Get, $"{this.Path}/definition"), cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<ResourceDefinition>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task<TResource> GetAsync(string name, string? @namespace = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        var uri = this.GetResourceUri(name);
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<TResource>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task<IAsyncEnumerable<TResource>> ListAsync(string? @namespace = null, IEnumerable<LabelSelector>? labelSelectors = null, CancellationToken cancellationToken = default)
    {
        var uri = this.Path;
        var queryStringArguments = new Dictionary<string, string>();
        // resources are cluster-scoped: the server accepts no namespace (see #178)
        if (labelSelectors?.Any() == true) queryStringArguments.Add("labelSelector", labelSelectors.Select(s => s.ToString()).Join(','));
        if (queryStringArguments.Count != 0) uri += $"?{queryStringArguments.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}").Join('&')}";
        var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken).ConfigureAwait(false);
        var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.DeserializeAsyncEnumerable<TResource>(responseStream, cancellationToken: cancellationToken)!;
    }

    /// <inheritdoc/>
    public virtual async Task<TResource> UpdateAsync(TResource resource, CancellationToken cancellationToken = default)
    {
        if (resource == null) throw new ArgumentNullException(nameof(resource));
        var json = this.Serializer.SerializeToText(resource);
        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Put, this.Path) { Content = content }, cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<TResource>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task<TResource> PatchAsync(Patch patch, string name, string? @namespace = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var uri = this.GetResourceUri(name);
        var json = this.Serializer.SerializeToText(patch);
        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Patch, uri) { Content = content }, cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<TResource>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task<TResource> PatchStatusAsync(Patch patch, string name, string? @namespace = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var uri = $"{this.GetResourceUri(name)}/status";
        var json = this.Serializer.SerializeToText(patch);
        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Patch, uri) { Content = content }, cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<TResource>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task DeleteAsync(string name, string? @namespace = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        var uri = this.GetResourceUri(name);
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Delete, uri), cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Processes the specified <see cref="HttpRequestMessage"/> before sending it
    /// </summary>
    /// <param name="request">the <see cref="HttpRequestMessage"/> to process</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The processed <see cref="HttpRequestMessage"/></returns>
    protected virtual Task<HttpRequestMessage> ProcessRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(request);
    }

    /// <summary>
    /// Processes the specified <see cref="HttpResponseMessage"/>
    /// </summary>
    /// <param name="response">the <see cref="HttpResponseMessage"/> to process</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The processed <see cref="HttpResponseMessage"/></returns>
    protected virtual async Task<HttpResponseMessage> ProcessResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.IsSuccessStatusCode) return response;
        var content = string.Empty;
        if (response.Content != null) content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        this.Logger.LogError("The remote server responded with a non-success status code '{statusCode}': {errorDetails}", response.StatusCode, content);
        if (!response.IsSuccessStatusCode)
        {
            if (string.IsNullOrWhiteSpace(content)) response.EnsureSuccessStatusCode();
            else throw new ProblemDetailsException(this.Serializer.Deserialize<ProblemDetails>(content)!);
        }
        return response;
    }

}