using CloudStreams.Core.Serialization.Json.Converters;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Models;

/// <summary>
/// Enumerates all supported sort orders
/// </summary>
[TypeConverter(typeof(EnumMemberConverter))]
[JsonConverter(typeof(StringEnumConverterFactory))]
public enum SortOrder
{
    /// <summary>
    /// Indicates an ascending sorting
    /// </summary>
    [EnumMember(Value = "asc")]
    Ascending,
    /// <summary>
    /// Indicates a descending sorting
    /// </summary>
    [EnumMember(Value = "desc")]
    Descending
}
