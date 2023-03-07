using CloudStreams.Core.Serialization.Json.Converters;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Models;

/// <summary>
/// Enumerates all supported artifact states
/// </summary>
[TypeConverter(typeof(EnumMemberConverter))]
[JsonConverter(typeof(StringEnumConverterFactory))]

public enum ArtifactState
{
    /// <summary>
    /// Indicates the the artifact is enabled
    /// </summary>
    [EnumMember(Value = "ENABLED")]
    Enabled,
    /// <summary>
    /// Indicates the the artifact is disabled
    /// </summary>
    [EnumMember(Value = "DISABLED")]
    Disabled,
    /// <summary>
    /// Indicates the the artifact has been deprecated
    /// </summary>
    [EnumMember(Value = "DEPRECATED")]
    Deprecated
}
