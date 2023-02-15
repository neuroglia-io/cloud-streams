using CloudStreams.Infrastructure.SchemaRegistry.Apicurio.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudStreams.Infrastructure.SchemaRegistry.Apicurio.Services;

/// <summary>
/// Represents the default implementation of the <see cref="IApicurioRegistryApiClient"/> interface
/// </summary>
public partial class ApicurioRegistryApiClient
    : IApicurioRegistryApiClient
{

    /// <summary>
    /// Gets the API's path prefix
    /// </summary>
    protected const string PathPrefix = "/apis/registry/v2";

    /// <summary>
    /// Initializes a new <see cref="ApicurioRegistryApiClient"/>
    /// </summary>
    /// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
    /// <param name="options">The current <see cref="ApiCurioRegistryClientOptions"/></param>
    /// <param name="httpClientFactory">The service used to perform requests</param>
    public ApicurioRegistryApiClient(ILoggerFactory loggerFactory, IOptions<ApiCurioRegistryClientOptions> options, IHttpClientFactory httpClientFactory)
    {
        this.Logger = loggerFactory.CreateLogger(this.GetType());
        this.Options = options.Value;
        this.HttpClient = httpClientFactory.CreateClient(typeof(ApicurioRegistryApiClient).Name);
    }

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the current <see cref="ApiCurioRegistryClientOptions"/>
    /// </summary>
    protected ApiCurioRegistryClientOptions Options { get; }

    /// <summary>
    /// Gets the service used to perform requests
    /// </summary>
    protected HttpClient HttpClient { get; }

    /// <inheritdoc/>
    public virtual IArtifactsApi Artifacts => this;

    /// <inheritdoc/>
    public virtual IVersionsApi Versions => this;

    /// <inheritdoc/>
    public virtual IMetadataApi Metadata => this;

    /// <summary>
    /// Formats the specified string
    /// </summary>
    /// <param name="value">The string to format</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The formatted string</returns>
    protected virtual async Task<string> FormatAsync(string value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(value));
        var formatted = value;
        return await Task.FromResult(this.Options.LineEndingFormatMode switch
        {
            LineEndingFormatMode.ConvertToUnix => formatted.ReplaceLineEndings("\r\n"),
            LineEndingFormatMode.ConvertToWindows => formatted.ReplaceLineEndings("\n"),
            _ => value,
        });
    }

}