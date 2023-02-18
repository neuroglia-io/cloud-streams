using System.Text.Json.Serialization;

namespace CloudStreams.Core.Infrastructure.Models;

internal class ListCloudEventTypesQueryResult
{

    [JsonPropertyName("types")]
    public List<string> Types { get; set; } = null!;

}
