using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CloudStreams.Core.Serialization.Yaml;


/// <summary>
/// Represents the <see cref="IYamlTypeConverter"/> used to convert <see cref="JsonNode"/>s
/// </summary>
public class JsonNodeTypeConverter
    : IYamlTypeConverter
{

    /// <inheritdoc/>
    public virtual bool Accepts(Type type) => typeof(JsonElement).IsAssignableFrom(type) || typeof(JsonNode).IsAssignableFrom(type);

    /// <inheritdoc/>
    public virtual object? ReadYaml(IParser parser, Type type) => throw new NotSupportedException();

    /// <inheritdoc/>
    public virtual void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        if (value is JsonElement jsonElement) value = Serializer.Json.SerializeToNode(jsonElement);
        this.WriteJsonNode(emitter, value as JsonNode);
    }

    /// <summary>
    /// Write the specified <see cref="JsonNode"/>
    /// </summary>
    /// <param name="emitter">The <see cref="IEmitter"/> to use</param>
    /// <param name="jsonNode">The <see cref="JsonNode"/> to write</param>
    protected virtual void WriteJsonNode(IEmitter emitter, JsonNode? jsonNode)
    {
        if (jsonNode == null) return;
        switch (jsonNode)
        {
            case JsonArray jsonArray:
                this.WriteJsonArray(emitter, jsonArray);
                break;
            case JsonObject jsonObject:
                this.WriteJsonObject(emitter, jsonObject);
                break;
            case JsonValue jsonValue:
                this.WriteJsonValue(emitter, jsonValue);
                break;
            default:
                throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Write the specified <see cref="JsonArray"/>
    /// </summary>
    /// <param name="emitter">The <see cref="IEmitter"/> to use</param>
    /// <param name="jsonArray">The <see cref="JsonArray"/> to write</param>
    protected virtual void WriteJsonArray(IEmitter emitter, JsonArray? jsonArray)
    {
        if (jsonArray == null) return;
        emitter.Emit(new SequenceStart(null, null, false, SequenceStyle.Block));
        foreach (var jsonNode in jsonArray)
        {
            this.WriteJsonNode(emitter, jsonNode);
        }
        emitter.Emit(new SequenceEnd());
    }

    /// <summary>
    /// Write the specified <see cref="JsonObject"/>
    /// </summary>
    /// <param name="emitter">The <see cref="IEmitter"/> to use</param>
    /// <param name="jsonObject">The <see cref="JsonObject"/> to write</param>
    protected virtual void WriteJsonObject(IEmitter emitter, JsonObject? jsonObject)
    {
        if (jsonObject == null) return;
        emitter.Emit(new MappingStart(null, null, false, MappingStyle.Block));
        foreach (var property in jsonObject)
        {
            this.WriteJsonObjectProperty(emitter, property);
        }
        emitter.Emit(new MappingEnd());
    }

    /// <summary>
    /// Write the specified JSON property
    /// </summary>
    /// <param name="emitter">The <see cref="IEmitter"/> to use</param>
    /// <param name="jsonProperty">The JSON property to write</param>
    protected virtual void WriteJsonObjectProperty(IEmitter emitter, KeyValuePair<string, JsonNode?> jsonProperty)
    {
        if (jsonProperty.Value == null) return;
        emitter.Emit(new Scalar(CamelCaseNamingConvention.Instance.Apply(jsonProperty.Key)));
        this.WriteJsonNode(emitter, jsonProperty.Value);
    }

    /// <summary>
    /// Write the specified <see cref="JsonValue"/>
    /// </summary>
    /// <param name="emitter">The <see cref="IEmitter"/> to use</param>
    /// <param name="jsonValue">The <see cref="JsonValue"/> to write</param>
    protected virtual void WriteJsonValue(IEmitter emitter, JsonValue? jsonValue)
    {
        if (jsonValue == null) return;
        var value = jsonValue.Deserialize<object>()?.ToString();
        if(string.IsNullOrWhiteSpace(value)) return;
        emitter.Emit(new Scalar(value));
    }

}
