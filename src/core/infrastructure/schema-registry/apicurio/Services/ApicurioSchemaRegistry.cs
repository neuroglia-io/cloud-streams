using CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Configuration;
using CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Models;
using CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Services;
using Json.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudStreams.Core.Infrastructure.Services;

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
    /// <param name="apicurioRegistryOptions">The service used to access the current <see cref="ApiCurioRegistryClientOptions"/></param>
    /// <param name="apicurioRegistry">The service used to interact with the <see href="https://www.apicur.io/registry/">Apicurio Registry</see></param>
    /// <param name="httpClient">The service used to performhttp requests</param>
    public ApiCurioSchemaRegistry(ILoggerFactory loggerFactory, IOptions<ApiCurioRegistryClientOptions> apicurioRegistryOptions, IApicurioRegistryApiClient apicurioRegistry, HttpClient httpClient)
    {
        this.Logger = loggerFactory.CreateLogger(this.GetType());
        this.ApicurioRegistryOptions = apicurioRegistryOptions.Value;
        this.ApicurioRegistry = apicurioRegistry;
        this.HttpClient = httpClient;
        Json.Schema.SchemaRegistry.Global.Fetch = uri => this.GetSchemaAsync(uri).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the options used to configure the application's <see cref="IApicurioRegistryApiClient"/>
    /// </summary>
    protected ApiCurioRegistryClientOptions ApicurioRegistryOptions { get; }

    /// <summary>
    /// Gets the service used to interact with the <see href="https://www.apicur.io/registry/">Apicurio Registry</see> API
    /// </summary>
    protected IApicurioRegistryApiClient ApicurioRegistry { get; }

    /// <summary>
    /// Gets the service used to performhttp requests
    /// </summary>
    protected HttpClient HttpClient { get; }

    /// <inheritdoc/>
    public virtual async Task<Uri> RegisterSchemaAsync(JsonSchema schema, CancellationToken cancellationToken = default)
    {
        if (schema == null) throw new ArgumentNullException(nameof(schema));
        var json = Serializer.Json.Serialize(schema);
        var artifactId = schema.Keywords?.OfType<IdKeyword>().FirstOrDefault()?.Id.OriginalString!;
        var groupId = "cloud-events";
        var artifact = await this.ApicurioRegistry.Artifacts.CreateArtifactAsync(ArtifactType.JSON, json, IfArtifactExistsAction.ReturnOrUpdate, artifactId, groupId, true, null, null, null, cancellationToken);
        var schemaUri = BuildArtifactUri(artifactId);
        Json.Schema.SchemaRegistry.Global.Register(schemaUri, schema);
        return schemaUri;
    }

    /// <inheritdoc/>
    public virtual async Task<Uri?> GetSchemaUriByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
        Artifact? artifact = null;
        try
        {
            artifact = await this.ApicurioRegistry.Metadata.GetArtifactMetadataAsync(id, this.ApicurioRegistryOptions.DefaultGroupId, cancellationToken).ConfigureAwait(false);
        }
        catch { }
        return artifact == null ? null : BuildArtifactUri(id);
    }

    async Task<JsonSchema?> ISchemaRegistry.GetSchemaAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (uri == null) throw new ArgumentNullException(nameof(uri));
        var schema = Json.Schema.SchemaRegistry.Global.Get(uri);
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
        if (!(uri.Host == this.ApicurioRegistryOptions.ServerUri.Host && uri.Port == this.ApicurioRegistryOptions.ServerUri.Port)) return null;
        var components = uri.PathAndQuery.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (components.Length < 7) return null;
        var groupId = components[4];
        var artifactId = components[6];
        var version = string.Empty;
        if (components.Length == 9) version = components[8];
        string? content;
        if (string.IsNullOrWhiteSpace(version) || version == "latest") content = await this.ApicurioRegistry.Artifacts.GetLatestArtifactAsync(artifactId, this.ApicurioRegistryOptions.DefaultGroupId, cancellationToken).ConfigureAwait(false);
        else content = await this.ApicurioRegistry.Versions.GetArtifactVersionAsync(artifactId, groupId, version, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(content)) return null;
        JsonSchema? schema;
        try
        {
            schema = Serializer.Json.Deserialize<JsonSchema>(content);
            Json.Schema.SchemaRegistry.Global.Register(uri, schema!);
            return schema;
        }
        catch (Exception ex)
        {
            this.Logger.LogWarning("Failed to deserialize artifact '{groupId}/{contentId}' into a valid JSON Schema: {ex}", groupId, artifactId, ex);
            return null;
        }
    }

    /// <summary>
    /// Builds a new <see cref="Uri"/> for the specified <see cref="Artifact"/>
    /// </summary>
    /// <param name="id">The id of the <see cref="Artifact"/> to build a new <see cref="Uri"/> for</param>
    /// <returns>A new <see cref="Uri"/></returns>
    protected virtual Uri BuildArtifactUri(string id) => UriHelper.Combine(this.ApicurioRegistryOptions.ServerUri.OriginalString, PathPrefix, this.ApicurioRegistryOptions.DefaultGroupId, "artifacts", id);

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
