using Microsoft.Extensions.Logging;

namespace CloudStreams.Gateway.Api.Client.Services;

/// <summary>
/// Represents the default implementation of the <see cref="ICloudStreamsGatewayApiClient"/> interface
/// </summary>
public partial class CloudStreamsGatewayApiClient
    : ICloudStreamsGatewayApiClient
{

    /// <summary>
    /// Initializes a new <see cref="CloudStreamsGatewayApiClient"/>
    /// </summary>
    /// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
    /// <param name="httpClient">The service used to perform http requests</param>
    public CloudStreamsGatewayApiClient(ILoggerFactory loggerFactory, HttpClient httpClient)
    {
        this.Logger = loggerFactory.CreateLogger(this.GetType());
        this.HttpClient = httpClient;
    }

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the service used to perform http requests
    /// </summary>
    protected HttpClient HttpClient { get; }

    ICloudEventsApi ICloudStreamsGatewayApiClient.CloudEvents => this;

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
