// Copyright © 2023-Present The Cloud Streams Authors
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

using CloudStreams.Core.Data.Models;
using Microsoft.Extensions.Logging;
using System.Net.Mime;
using System.Text;

namespace CloudStreams.ResourceManagement.Api.Client.Services;

/// <summary>
/// Represents the default implementation of the <see cref="IResourceManagementApi{TResource}"/>
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/>s to manage</typeparam>
public class ResourceManagementApi<TResource>
    : IResourceManagementApi<TResource>
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Initializes a new <see cref="ResourceManagementApi{TResource}"/>
    /// </summary>
    /// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
    /// <param name="httpClient">The service used to perform HTTP requests</param>
    /// <param name="path">The path to the API used to manage <see cref="IResource"/>s of the specified type</param>
    public ResourceManagementApi(ILoggerFactory loggerFactory, HttpClient httpClient, string path)
    {
        this.Logger = loggerFactory.CreateLogger(this.GetType());
        this.HttpClient = httpClient;
        this.Path = CloudStreamsResourceManagementApiClient.ApiVersionPathPrefix + path;
    }

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the service used to perform HTTP requests
    /// </summary>
    protected HttpClient HttpClient { get; }

    /// <summary>
    /// Gets the path to the API used to manage <see cref="IResource"/>s of the specified type
    /// </summary>
    protected string Path { get; }

    /// <inheritdoc/>
    public virtual async Task<TResource> CreateAsync(TResource resource, CancellationToken cancellationToken = default)
    {
        if (resource == null) throw new ArgumentNullException(nameof(resource));
        var json = Serializer.Json.Serialize(resource);
        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Post, this.Path) { Content = content }, cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return Serializer.Json.Deserialize<TResource>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task<ResourceDefinition> GetDefinitionAsync(CancellationToken cancellationToken = default)
    {
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Get, $"{this.Path}/definition"), cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return Serializer.Json.Deserialize<ResourceDefinition>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task<TResource> GetAsync(string name, string? @namespace = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        var uri = string.IsNullOrWhiteSpace(@namespace) ? $"{this.Path}/{name}" : $"{this.Path}/{@namespace}/{name}";
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return Serializer.Json.Deserialize<TResource>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task<IAsyncEnumerable<TResource>> ListAsync(string? @namespace = null, IEnumerable<ResourceLabelSelector>? labelSelectors = null, CancellationToken cancellationToken = default)
    {
        var uri = this.Path;
        var queryStringArguments = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(@namespace)) queryStringArguments.Add("namespace", @namespace!);
        if (labelSelectors?.Any() == true) queryStringArguments.Add(nameof(labelSelectors), labelSelectors.Select(s => s.ToString()).Join(','));
        if (queryStringArguments.Any()) uri += $"?{queryStringArguments.Select(kvp => $"{kvp.Key}={kvp.Value}").Join('&')}";
        var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken).ConfigureAwait(false);
        var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return Serializer.Json.DeserializeAsyncEnumerable<TResource>(responseStream, cancellationToken: cancellationToken)!;
    }

    /// <inheritdoc/>
    public virtual async Task<TResource> UpdateAsync(TResource resource, CancellationToken cancellationToken = default)
    {
        if (resource == null) throw new ArgumentNullException(nameof(resource));
        var json = Serializer.Json.Serialize(resource);
        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Put, this.Path) { Content = content }, cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return Serializer.Json.Deserialize<TResource>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task<TResource> PatchAsync(ResourcePatch<TResource> patch, CancellationToken cancellationToken = default)
    {
        if (patch == null) throw new ArgumentNullException(nameof(patch));
        var json = Serializer.Json.Serialize(patch);
        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Patch, this.Path) { Content = content }, cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return Serializer.Json.Deserialize<TResource>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task<TResource> PatchStatusAsync(ResourcePatch<TResource> patch, CancellationToken cancellationToken = default)
    {
        if (patch == null) throw new ArgumentNullException(nameof(patch));
        var json = Serializer.Json.Serialize(patch);
        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Patch, $"{this.Path}/status") { Content = content }, cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return Serializer.Json.Deserialize<TResource>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task DeleteAsync(string name, string? @namespace = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        var uri = string.IsNullOrWhiteSpace(@namespace) ? $"{this.Path}/{name}" : $"{this.Path}/{@namespace}/{name}";
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
        if (request == null) throw new ArgumentNullException(nameof(request));
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
        if (response == null) throw new ArgumentNullException(nameof(response));
        if (response.IsSuccessStatusCode) return response;
        var content = string.Empty;
        if (response.Content != null) content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        this.Logger.LogError("The remote server responded with a non-success status code '{statusCode}': {errorDetails}", response.StatusCode, content);
        if (!response.IsSuccessStatusCode)
        {
            if (string.IsNullOrWhiteSpace(content)) response.EnsureSuccessStatusCode();
            else throw new CloudStreamsException(Serializer.Json.Deserialize<Core.Data.Models.ProblemDetails>(content));
        }
        return response;
    }

}