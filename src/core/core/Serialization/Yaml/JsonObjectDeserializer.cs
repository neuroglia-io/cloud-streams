using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace CloudStreams.Serialization.Yaml;

/// <summary>
/// Represents the <see cref="INodeDeserializer"/> used to deserialize <see cref="JsonObject"/>s
/// </summary>
public class JsonObjectDeserializer
    : INodeDeserializer
{

    /// <summary>
    /// Initializes a new <see cref="JsonObjectDeserializer"/>
    /// </summary>
    /// <param name="inner">The inner <see cref="INodeDeserializer"/></param>
    public JsonObjectDeserializer(INodeDeserializer inner)
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
        if (!typeof(JsonObject).IsAssignableFrom(expectedType)) return this.Inner.Deserialize(reader, expectedType, nestedObjectDeserializer, out value);
        if (!this.Inner.Deserialize(reader, typeof(Dictionary<object, object>), nestedObjectDeserializer, out value)) return false;
        value = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(value));
        return true;
    }

}
