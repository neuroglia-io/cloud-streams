using CloudStreams.Core.Serialization.Json.Converters;
using System.ComponentModel;

namespace CloudStreams.Core;

/// <summary>
/// Enumerates all supported read directions for streams
/// </summary>
[TypeConverter(typeof(EnumMemberConverter))]
[JsonConverter(typeof(StringEnumConverterFactory))]
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