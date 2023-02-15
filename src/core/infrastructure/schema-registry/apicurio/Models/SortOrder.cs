using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace CloudStreams.Infrastructure.SchemaRegistry.Apicurio.Models;

/// <summary>
/// Enumerates all supported sort orders
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
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
