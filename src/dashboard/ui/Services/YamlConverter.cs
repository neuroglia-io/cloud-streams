using static CloudStreams.Core.Serializer;

namespace CloudStreams.Dashboard.Services;

/// <inheritdoc/>
public class YamlConverter
    : IYamlConverter
{
    /// <inheritdoc/>
    public string YamlToJson(string yaml)
    {
        return Serializer.Json.Serialize(Serializer.Yaml.Deserialize<object>(yaml));
    }

    /// <inheritdoc/>
    public string JsonToYaml(string json)
    {
        return Serializer.Yaml.Serialize(Serializer.Json.Deserialize<object>(json));
    }

}
