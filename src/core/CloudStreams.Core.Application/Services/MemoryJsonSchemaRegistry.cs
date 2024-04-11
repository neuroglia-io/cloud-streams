using Json.Schema;
using Neuroglia.Serialization;

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Represents the in-memory implementation of the <see cref="IJsonSchemaRegistry"/> interface
/// </summary>
/// <param name="httpClient">The service used to perform HTTP requests</param>
/// <param name="serializer">The service used to serialize/deserialize objects to/from JSON</param>
public class MemoryJsonSchemaRegistry(HttpClient httpClient, IJsonSerializer serializer)
    : IJsonSchemaRegistry
{

    /// <summary>
    /// Gets the service used to perform HTTP requests
    /// </summary>
    protected HttpClient HttpClient { get; } = httpClient;

    /// <summary>
    /// Gets the service used to serialize/deserialize objects to/from JSON
    /// </summary>
    protected IJsonSerializer Serializer { get; } = serializer;

    /// <inheritdoc/>
    public virtual async Task<JsonSchema> GetAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        var document = (JsonNodeBaseDocument?)SchemaRegistry.Global.Get(uri);
        if (document != null) return this.Serializer.Deserialize<JsonSchema>(this.Serializer.SerializeToText(document))!;
        var json = await this.HttpClient.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
        var schema = this.Serializer.Deserialize<JsonSchema>(json)!;
        document = new JsonNodeBaseDocument(this.Serializer.SerializeToNode(schema)!, uri);
        SchemaRegistry.Global.Register(uri, document);
        return schema;
    }

    /// <inheritdoc/>
    public virtual async Task RegisterAsync(JsonSchema schema, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var document = new JsonNodeBaseDocument(this.Serializer.SerializeToNode(schema)!, schema.BaseUri);
        SchemaRegistry.Global.Register(schema.BaseUri, document);
        await Task.CompletedTask.ConfigureAwait(false);
    }

}
