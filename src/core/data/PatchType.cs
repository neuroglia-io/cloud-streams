using CloudStreams.Core.Serialization.Json.Converters;

namespace CloudStreams.Core;

/// <summary>
/// Enumerates all supported patch types
/// </summary>
[TypeConverter(typeof(EnumMemberConverter))]
[JsonConverter(typeof(StringEnumConverterFactory))]
public enum PatchType
{
    /// <summary>
    /// Indicates a <see href="https://www.rfc-editor.org/rfc/rfc6902">Json Patch</see>
    /// </summary>
    [EnumMember(Value = "patch")]
    JsonPatch,
    /// <summary>
    /// Indicates a <see href="https://www.rfc-editor.org/rfc/rfc7386">Json Merge Patch</see>
    /// </summary>
    [EnumMember(Value = "merge")]
    JsonMergePatch,
    /// <summary>
    /// Indicates a <see href="https://github.com/kubernetes/community/blob/master/contributors/devel/sig-api-machinery/strategic-merge-patch.md">Json Strategic Merge Patch</see>
    /// </summary>
    [EnumMember(Value = "strategic")]
    StrategicMergePatch
}