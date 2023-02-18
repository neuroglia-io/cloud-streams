using System.Text.Json.Serialization;

namespace CloudStreams.Core.Infrastructure.Models;

internal class ListCloudEventSubjectsQueryResult
{

    [JsonPropertyName("subjects")]
    public List<string> Subjects { get; set; } = null!;

}
