using System.Text.Json.Nodes;
using System.Text.Json;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using Json.Schema;

namespace CloudStreams.Serialization.Yaml;

/// <summary>
/// Represents the <see cref="INodeDeserializer"/> used to deserialize <see cref="JsonSchema"/>s
/// </summary>
public class JsonSchemaDeserializer
    : INodeDeserializer
{

    /// <summary>
    /// Initializes a new <see cref="JsonSchemaDeserializer"/>
    /// </summary>
    /// <param name="inner">The inner <see cref="INodeDeserializer"/></param>
    public JsonSchemaDeserializer(INodeDeserializer inner)
    {
        this.Inner = inner;
    }

    /// <summary>
    /// Gets the inner <see cref="INodeDeserializer"/>
    /// </summary>
    protected INodeDeserializer Inner { get; }

    /// <inheritdoc/>
    public virtual bool Deserialize(IParser reader, Type expectedType, Func<IParser, Type, object?> nestedObjectDeserializer, out object? value)
    {
        if (!typeof(JsonSchema).IsAssignableFrom(expectedType)) return this.Inner.Deserialize(reader, expectedType, nestedObjectDeserializer, out value!);
        if (!this.Inner.Deserialize(reader, typeof(JsonObject), nestedObjectDeserializer, out value!)) return false;
        var jsonObject = (JsonObject)value;
        var jschema = jsonObject.Deserialize<JsonSchema>()!;
        value = jschema;
        return true;
    }

}
