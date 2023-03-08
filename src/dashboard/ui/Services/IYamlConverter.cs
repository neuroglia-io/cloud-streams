namespace CloudStreams.Dashboard.Services;

/// <summary>
/// A service used to convert YAML to JSON and JSON to YAML
/// </summary>
public interface IYamlConverter
{
    /// <summary>
    /// Converts the provided JSON string to YAML
    /// </summary>
    /// <param name="json">The JSON string ton convert</param>
    /// <returns>The converted value</returns>
    string JsonToYaml(string json);

    /// <summary>
    /// Converts the provided YAML string to JSON
    /// </summary>
    /// <param name="yaml">The YAML string ton convert</param>
    /// <returns>The converted value</returns>
    string YamlToJson(string yaml);

}
