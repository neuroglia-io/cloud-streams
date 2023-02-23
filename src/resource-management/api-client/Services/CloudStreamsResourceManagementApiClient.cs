using CloudStreams.Core.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CloudStreams.ResourceManagement.Api.Client.Services;

/// <summary>
/// Represents the default implementation of the <see cref="ICloudStreamsResourceManagementApiClient"/> interface
/// </summary>
public partial class CloudStreamsResourceManagementApiClient
    : ICloudStreamsResourceManagementApiClient
{

    internal const string ApiVersionPathPrefix = "api/resource-management/v1/";

    /// <summary>
    /// Initializes a new <see cref="CloudStreamsResourceManagementApiClient"/>
    /// </summary>
    /// <param name="serviceProvider">The current <see cref="IServiceProvider"/></param>
    /// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
    /// <param name="httpClient">The service used to perform http requests</param>
    public CloudStreamsResourceManagementApiClient(IServiceProvider serviceProvider, ILoggerFactory loggerFactory, HttpClient httpClient)
    {
        this.ServiceProvider = serviceProvider;
        this.Logger = loggerFactory.CreateLogger(this.GetType());
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
    /// Gets the service used to perform http requests
    /// </summary>
    protected HttpClient HttpClient { get; }

    /// <inheritdoc/>
    public IResourceManagementApi<Gateway> Gateways { get; private set; } = null!;

    /// <inheritdoc/>
    public IResourceManagementApi<Broker> Brokers { get; private set; } = null!;

    /// <inheritdoc/>
    public IResourceManagementApi<Channel> Channels { get; private set; } = null!;

    /// <inheritdoc/>
    public IResourceManagementApi<Network> Networks { get; private set; } = null!;

    /// <summary>
    /// Processes the specified <see cref="HttpRequestMessage"/> before sending it
    /// </summary>
    /// <param name="request">the <see cref="HttpRequestMessage"/> to process</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The processed <see cref="HttpRequestMessage"/></returns>
    protected virtual Task<HttpRequestMessage> ProcessRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        if(request == null) throw new ArgumentNullException(nameof(request));
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
        response.EnsureSuccessStatusCode();
        return response;
    }

}
