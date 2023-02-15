using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace CloudStreams.Infrastructure.SchemaRegistry.Apicurio.Models;

/// <summary>
/// Enumerates all supported artifact states
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]

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
