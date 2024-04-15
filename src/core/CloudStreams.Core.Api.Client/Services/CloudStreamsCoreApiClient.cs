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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Neuroglia;
using Neuroglia.Serialization;

namespace CloudStreams.Core.Api.Client.Services;

/// <summary>
/// Represents the default implementation of the <see cref="ICloudStreamsCoreApiClient"/> interface
/// </summary>
public partial class CloudStreamsCoreApiClient
    : ICloudStreamsCoreApiClient
{

    internal const string ApiVersionPathPrefix = "api/";

    /// <summary>
    /// Initializes a new <see cref="CloudStreamsCoreApiClient"/>
    /// </summary>
    /// <param name="serviceProvider">The current <see cref="IServiceProvider"/></param>
    /// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
    /// <param name="serializer">The service used to serialize/deserialize objects to/from JSON</param>
    /// <param name="httpClient">The service used to perform http requests</param>
    public CloudStreamsCoreApiClient(IServiceProvider serviceProvider, ILoggerFactory loggerFactory, IJsonSerializer serializer, HttpClient httpClient)
    {
        this.ServiceProvider = serviceProvider;
        this.Logger = loggerFactory.CreateLogger(this.GetType());
        this.Serializer = serializer;
        this.HttpClient = httpClient;
        foreach(var apiProperty in this.GetType().GetProperties().Where(p => p.CanRead && p.PropertyType.GetGenericType(typeof(IResourceManagementApi<>)) != null))
        {
            var apiType = apiProperty.PropertyType.GetGenericType(typeof(IResourceManagementApi<>))!;
            var resourceType = apiType.GetGenericArguments()[0];
            var api = ActivatorUtilities.CreateInstance(this.ServiceProvider, typeof(ResourceManagementApi<>).MakeGenericType(resourceType), this.HttpClient, apiProperty.Name.ToLowerInvariant());
            apiProperty.SetValue(this, api);
        }
    }

    /// <summary>
    /// Gets the current <see cref="IServiceProvider"/>
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the service used to serialize/deserialize objects to/from JSON
    /// </summary>
    protected IJsonSerializer Serializer { get; }

    /// <summary>
    /// Gets the service used to perform http requests
    /// </summary>
    protected HttpClient HttpClient { get; }

    /// <inheritdoc/>
    public ICloudEventsApi CloudEvents => this;

    /// <inheritdoc/>
    public IResourceManagementApiClient Resources => this;

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
