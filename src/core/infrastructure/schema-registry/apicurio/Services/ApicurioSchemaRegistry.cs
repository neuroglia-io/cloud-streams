using Json.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Neuroglia.Data.SchemaRegistry;
using Neuroglia.Data.SchemaRegistry.Configuration;
using Neuroglia.Data.SchemaRegistry.Services;

namespace CloudStreams.Infrastructure.Services;

/// <summary>
/// Represents the <see href="https://www.apicur.io/registry/">Apicurio Registry</see> based implementation of the <see cref="ISchemaRegistry"/> interface
/// </summary>
public class ApiCurioSchemaRegistry
    : ISchemaRegistry, IDisposable
{

    /// <summary>
    /// Gets the path prefix for all API Curio requests
    /// </summary>
    protected const string PathPrefix = "apis/registry/v2/groups";

    private bool _Disposed;

    /// <summary>
    /// Initializes a new <see cref="ApiCurioSchemaRegistry"/>
    /// </summary>
    /// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
    /// <param name="apiCurioRegistryOptions">The service used to access the current <see cref="ApiCurioRegistryClientOptions"/></param>
    /// <param name="apiCurioRegistry">The service used to interact with the <see href="https://www.apicur.io/registry/">Apicurio Registry</see></param>
    /// <param name="httpClient">The service used to performhttp requests</param>
    public ApiCurioSchemaRegistry(ILoggerFactory loggerFactory, IOptions<ApiCurioRegistryClientOptions> apiCurioRegistryOptions, IApiCurioRegistryClient apiCurioRegistry, HttpClient httpClient)
    {
        this.Logger = loggerFactory.CreateLogger(this.GetType());
        this.ApiCurioRegistryOptions = apiCurioRegistryOptions.Value;
        this.ApiCurioRegistry = apiCurioRegistry;
        this.HttpClient = httpClient;
        SchemaRegistry.Global.Fetch = uri => this.GetSchemaAsync(uri).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the options used to configure the application's <see cref="IApiCurioRegistryClient"/>
    /// </summary>
    protected ApiCurioRegistryClientOptions ApiCurioRegistryOptions { get; }

    /// <summary>
    /// Gets the service used to interact with the <see href="https://www.apicur.io/registry/">Apicurio Registry</see> API
    /// </summary>
    protected IApiCurioRegistryClient ApiCurioRegistry { get; }

    /// <summary>
    /// Gets the service used to performhttp requests
    /// </summary>
    protected HttpClient HttpClient { get; }

    /// <inheritdoc/>
    public virtual async Task<Uri> RegisterSchemaAsync(JsonSchema schema, CancellationToken cancellationToken = default)
    {
        if (schema == null) throw new ArgumentNullException(nameof(schema));
        var json = Serializer.Json.Serialize(schema);
        var artifactId = "";
        var groupId = "";
        var artifact = await this.ApiCurioRegistry.CreateArtifactAsync(ArtifactType.JSON, json, IfArtifactExistsAction.ReturnOrUpdate, artifactId, groupId, true, null, null, null, cancellationToken);
        var schemaUri = UriHelper.Combine(this.ApiCurioRegistryOptions.ServerUri.OriginalString, "apis", "registry", "v2", "groups", groupId, artifactId, "versions", artifact.Version!);
        SchemaRegistry.Global.Register(schemaUri, schema);
        return schemaUri;
    }

    async Task<JsonSchema?> ISchemaRegistry.GetSchemaAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (uri == null) throw new ArgumentNullException(nameof(uri));
        var schema = SchemaRegistry.Global.Get(uri);
        if (schema != null) return schema;
        return await this.GetSchemaAsync(uri, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the <see cref="JsonSchema"/> with the specified <see cref="Uri"/>, if any
    /// </summary>
    /// <param name="uri">The <see cref="Uri"/> of the <see cref="JsonSchema"/> to get</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The <see cref="JsonSchema"/> with the specified <see cref="Uri"/>, if any</returns>
    protected virtual async Task<JsonSchema?> GetSchemaAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        if (!(uri.Host == this.ApiCurioRegistryOptions.ServerUri.Host && uri.Port == this.ApiCurioRegistryOptions.ServerUri.Port)) return null;
        var components = uri.PathAndQuery.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (components.Length < 7) return null;
        var groupId = components[4];
        var artifactId = components[6];
        var version = string.Empty;
        if (components.Length == 9) version = components[8];
        string? content;
        if (string.IsNullOrWhiteSpace(version)) content = await this.ApiCurioRegistry.GetArtifactContentByIdAsync(artifactId, cancellationToken).ConfigureAwait(false);
        else content = await this.ApiCurioRegistry.GetArtifactVersionAsync(artifactId, groupId, version, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(content)) return null;
        JsonSchema? schema;
        try
        {
            schema = Serializer.Json.Deserialize<JsonSchema>(content);
            SchemaRegistry.Global.Register(uri, schema!);
            return schema;
        }
        catch (Exception ex)
        {
            this.Logger.LogWarning("Failed to deserialize artifact '{groupId}/{contentId}' into a valid JSON Schema: {ex}", groupId, artifactId, ex);
            return null;
        }
    }

    /// <summary>
    /// Disposes of the <see cref="ApiCurioSchemaRegistry"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="ApiCurioSchemaRegistry"/> is being disposed of</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this._Disposed)
        {
            if (disposing)
            {
                this.HttpClient.Dispose();
            }
            this._Disposed = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

}