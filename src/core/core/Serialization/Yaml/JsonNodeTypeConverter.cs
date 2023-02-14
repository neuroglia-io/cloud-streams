using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CloudStreams.Serialization.Yaml;


/// <summary>
/// Represents the <see cref="IYamlTypeConverter"/> used to convert <see cref="JsonNode"/>s
/// </summary>
public class JsonNodeTypeConverter
    : IYamlTypeConverter
{

    /// <inheritdoc/>
    public virtual bool Accepts(Type type) => typeof(JsonNode).IsAssignableFrom(type);

    /// <inheritdoc/>
    public virtual object? ReadYaml(IParser parser, Type type) => throw new NotSupportedException();

    /// <inheritdoc/>
    public virtual void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        this.WriteJsonNode(emitter, value as JsonNode);
    }

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

    protected virtual void WriteJsonObjectProperty(IEmitter emitter, KeyValuePair<string, JsonNode?> jsonProperty)
    {
        if (jsonProperty.Value == null) return;
        emitter.Emit(new Scalar(CamelCaseNamingConvention.Instance.Apply(jsonProperty.Key)));
        this.WriteJsonNode(emitter, jsonProperty.Value);
    }

    protected virtual void WriteJsonValue(IEmitter emitter, JsonValue? jsonValue)
    {
        if (jsonValue == null) return;
        var value = jsonValue.Deserialize<object>()?.ToString();
        if(string.IsNullOrWhiteSpace(value)) return;
        emitter.Emit(new Scalar(value));
    }

}