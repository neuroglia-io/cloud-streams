using CloudStreams.Core.Data.Models;
using System.Text.Json.Serialization;

namespace CloudStreams.Core.Infrastructure.Models;

/// <summary>
/// Represent metadata for a specific <see cref="CloudEventPartitionType"/>
/// </summary>
internal class PartitionTypeMetadata
{
    /// <summary>
    /// Gets/Sets existing ids of a <see cref="CloudEventPartitionType"/>
    /// </summary>
    [JsonPropertyName("keys")]
    public List<string> Keys { get; set; } = null!;

    /// <summary>
    /// Gets/Sets the metadata entries of a <see cref="CloudEventPartitionType"/>
    /// </summary>
    [JsonPropertyName("values")]
    public Dictionary<string, CloudEventPartitionMetadata> Values { get; set; } = null!;

}
