using CloudStreams.Core.Serialization.Json.Converters;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Configuration;

/// <summary>
/// Enumerates all supported line ending formats
/// </summary>
[TypeConverter(typeof(EnumMemberConverter))]
[JsonConverter(typeof(StringEnumConverterFactory))]

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
