using CloudStreams.Core.Serialization.Json.Utilities;

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="JsonSchema"/>s
/// </summary>
public static class JsonSchemaExtensions
{

    /// <summary>
    /// Gets the <see cref="JsonSchema"/> that defines the property with the specified name
    /// </summary>
    /// <param name="schema">The <see cref="JsonSchema"/> to check for the specified property</param>
    /// <param name="propertyName">The name of the property to get the <see cref="JsonSchema"/> of</param>
    /// <returns>The <see cref="JsonSchema"/> that defines the property with the specified name, if any</returns>
    public static JsonSchema? GetProperty(this JsonSchema schema, string propertyName)
    {
        var properties = schema.Keywords?.OfType<PropertiesKeyword>().SingleOrDefault()?.Properties;
        properties ??= schema.Keywords?.OfType<ItemsKeyword>().SingleOrDefault()?.SingleSchema?.Keywords?.OfType<PropertiesKeyword>().SingleOrDefault()?.Properties;
        if (properties == null || !properties.TryGetValue(propertyName, out var propertySchema)) return null;
        var items = propertySchema?.Keywords?.OfType<ItemsKeyword>().SingleOrDefault();
        if (items?.SingleSchema != null) propertySchema = items.SingleSchema;
        return propertySchema;
    }

    /// <summary>
    /// Gets the <see cref="JsonStrategicMergePatch"/> merge key defined by the <see cref="JsonSchema"/>, if any
    /// </summary>
    /// <param name="schema">The <see cref="JsonSchema"/> to check</param>
    /// <returns>The <see cref="JsonStrategicMergePatch"/> merge key defined by the <see cref="JsonSchema"/>, if any</returns>
    public static string? GetPatchMergeKey(this JsonSchema schema)
    {
        return schema.Keywords?.OfType<UnrecognizedKeyword>().SingleOrDefault(k => k.Name == JsonStrategicMergePatch.JsonSchemaProperties.MergeKey)?.Value?.GetValue<string>();
    }

}