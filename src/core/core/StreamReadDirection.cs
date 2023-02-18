namespace CloudStreams.Core;

/// <summary>
/// Enumerates all supported read directions for streams
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StreamReadDirection
{
    /// <summary>
    /// Specifies a forward direction
    /// </summary>
    [EnumMember(Value = "forwards")]
    Forwards,
    /// <summary>
    /// Specifies a backward direction
    /// </summary>
    [EnumMember(Value = "backwards")]
    Backwards
}