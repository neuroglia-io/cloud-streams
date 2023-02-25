using System.Text.Json.Serialization;

namespace CloudStreams.Core.Infrastructure.Models;

/// <summary>
/// Represents the results of the <see cref="EventStoreProjections.CloudEventPartitionsMetadata"/> projection result
/// </summary>
internal class PartitionsMetadataProjectionResult
{
    /// <summary>
    /// Gets/Sets the <see cref="PartitionTypeMetadata"/> entries
    /// </summary>
    [JsonPropertyName("entries")]
    public Dictionary<string, PartitionTypeMetadata> Entries { get; set; } = null!;

}
