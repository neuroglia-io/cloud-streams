using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Neuroglia;
using Neuroglia.Serialization.Json.Converters;

namespace CloudStreams.Core.Api.Models;

/// <summary>
/// Enumerates all supported read output formats
/// </summary>
[TypeConverter(typeof(EnumMemberTypeConverter))]
[JsonConverter(typeof(StringEnumConverter))]
public enum StreamReadOutputFormat
{
    /// <summary>
    /// Specifies that the stream should output cloud events
    /// </summary>
    [EnumMember(Value = "event")]
    Event = 1,
    /// <summary>
    /// Specifies that the stream should output cloud event records
    /// </summary>
    [EnumMember(Value = "record")]
    Record = 2
}