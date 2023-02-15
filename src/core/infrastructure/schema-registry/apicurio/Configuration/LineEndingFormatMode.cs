using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace CloudStreams.Infrastructure.SchemaRegistry.Apicurio.Configuration;

/// <summary>
/// Enumerates all supported line ending formats
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]

public enum LineEndingFormatMode
{
    /// <summary>
    /// Indicates that original line endings should be preserved 
    /// </summary>
    [EnumMember(Value = "preserve")]
    Preserve,
    /// <summary>
    /// Indicates that original line endings should be converted to Unix line endings ('\n' character)
    /// </summary>
    [EnumMember(Value = "unix")]
    ConvertToUnix,
    /// <summary>
    /// Indicates that original line endings should be converted to Windows line endings ('\r\n' character)
    /// </summary>
    [EnumMember(Value = "win")]
    ConvertToWindows
}
