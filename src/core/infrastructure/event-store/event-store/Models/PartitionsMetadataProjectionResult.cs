using CloudStreams.Core.Data.Models;
using System.Text.Json.Serialization;

namespace CloudStreams.Core.Infrastructure.Models;

/// <summary>
/// Represents the results of the partition of a <see cref="CloudEventPartitionType"/> metadata projection
/// </summary>
internal class PartitionsMetadataProjectionResult
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
    public Dictionary<string, PartitionMetadata> Values { get; set; } = null!;

}
