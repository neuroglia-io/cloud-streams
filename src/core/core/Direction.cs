namespace CloudStreams;

/// <summary>
/// Enumerates all supported directions
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StreamReadDirection
{
    [EnumMember(Value = "forwards")]
    Forwards,
    [EnumMember(Value = "backwards")]
    Backwards
}