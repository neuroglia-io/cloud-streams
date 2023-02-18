using Json.Schema.Generation;
using System.Text.Json.Nodes;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents the default implementation of the <see cref="ISchemaGenerator"/> interface
/// </summary>
public class SchemaGenerator
    : ISchemaGenerator
{

    /// <inheritdoc/>
    public virtual async Task<JsonSchema?> GenerateAsync(object? graph, JsonSchemaGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (graph == null) return new JsonSchemaBuilder().Type(SchemaValueType.Null).Build();
        var schemaGeneratorConfiguration = SchemaGeneratorConfiguration.Current;
        if(graph is not JsonNode graphNode) graphNode = Serializer.Json.SerializeToNode(graph)!;
        return graphNode switch
        {
            JsonArray jsonArray => await this.GenerateForJsonArrayAsync(jsonArray, options, cancellationToken),
            JsonObject jsonObject => await this.GenerateForJsonObjectAsync(jsonObject, options, cancellationToken),
            JsonValue jsonValue => await this.GenerateForJsonValueAsync(jsonValue, options, cancellationToken),
            _ => throw new NotSupportedException($"The specified JsonNode type '{graphNode.GetType()}' is not supported"),
        };
    }

    /// <summary>
    /// Generates a new <see cref="JsonSchema"/> for the specified <see cref="JsonArray"/>
    /// </summary>
    /// <param name="array">The <see cref="JsonArray"/> to generate a new <see cref="JsonSchema"/> for</param>
    /// <param name="options">The <see cref="JsonSchemaGenerationOptions"/> to use</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="JsonSchema"/></returns>
    protected virtual async Task<JsonSchema?> GenerateForJsonArrayAsync(JsonArray array, JsonSchemaGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var items = array.OfType<object>();
        var schemaBuilder = new JsonSchemaBuilder().Type(SchemaValueType.Array);
        if (!string.IsNullOrWhiteSpace(options?.Id)) schemaBuilder = schemaBuilder.Id(options.Id);
        if (!string.IsNullOrWhiteSpace(options?.Title)) schemaBuilder = schemaBuilder.Title(options.Title);
        var schema = await this.GenerateAsync(items.First(), null, cancellationToken);
        if (schema == null) return null;
        if (items.Any()) schemaBuilder = schemaBuilder.Items(schema);
        return schemaBuilder.Build();
    }

    /// <summary>
    /// Generates a new <see cref="JsonSchema"/> for the specified <see cref="JsonObject"/>
    /// </summary>
    /// <param name="obj">The <see cref="JsonObject"/> to generate a new <see cref="JsonSchema"/> for</param>
    /// <param name="options">The <see cref="JsonSchemaGenerationOptions"/> to use</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="JsonSchema"/></returns>
    protected virtual async Task<JsonSchema?> GenerateForJsonObjectAsync(JsonObject obj, JsonSchemaGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var schemaBuilder = new JsonSchemaBuilder().Type(SchemaValueType.Object);
        if (!string.IsNullOrWhiteSpace(options?.Id)) schemaBuilder = schemaBuilder.Id(options.Id);
        if (!string.IsNullOrWhiteSpace(options?.Title)) schemaBuilder = schemaBuilder.Title(options.Title);
        var properties = new Dictionary<string, JsonSchema>();
        foreach(var jsonProperty in obj)
        {
            var schema = await this.GenerateAsync(jsonProperty.Value!, null, cancellationToken);
            if (schema == null) continue;
            properties.Add(jsonProperty.Key, schema);
        }
        schemaBuilder.Properties(properties);
        return schemaBuilder.Build();
    }

    /// <summary>
    /// Generates a new <see cref="JsonSchema"/> for the specified <see cref="JsonValue"/>
    /// </summary>
    /// <param name="value">The <see cref="JsonValue"/> to generate a new <see cref="JsonSchema"/> for</param>
    /// <param name="options">The <see cref="JsonSchemaGenerationOptions"/> to use</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="JsonSchema"/></returns>
    protected virtual Task<JsonSchema?> GenerateForJsonValueAsync(JsonValue value, JsonSchemaGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var schemaBuilder = new JsonSchemaBuilder().Type(value.GetSchemaValueType());
        if (!string.IsNullOrWhiteSpace(options?.Id)) schemaBuilder = schemaBuilder.Id(options.Id);
        if (!string.IsNullOrWhiteSpace(options?.Title)) schemaBuilder = schemaBuilder.Title(options.Title);
        return Task.FromResult((JsonSchema?)schemaBuilder.Build());
    }

}