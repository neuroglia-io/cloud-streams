namespace CloudStreams;

/// <summary>
/// Defines extensions for <see cref="JsonNode"/>s
/// </summary>
public static class JsonNodeExtensions
{

    /// <summary>
    /// Converts the <see cref="JsonNode"/> into a <see cref="JsonElement"/>
    /// </summary>
    /// <param name="jsonNode">The <see cref="JsonNode"/> to convert</param>
    /// <returns>A new <see cref="JsonElement"/></returns>
    public static JsonElement AsJsonElement(this JsonNode jsonNode)
    {
        return jsonNode.Deserialize<JsonElement>();
    }

}
