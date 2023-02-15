using CloudStreams.Infrastructure.SchemaRegistry.Apicurio.Models;
using Microsoft.Extensions.Logging;

namespace CloudStreams.Infrastructure.SchemaRegistry.Apicurio.Services;

public partial class ApicurioRegistryApiClient
    : IMetadataApi
{

    async Task<Artifact> IMetadataApi.GetArtifactMetadataAsync(string artifactId, string groupId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(artifactId)) throw new ArgumentNullException(nameof(artifactId));
        if (string.IsNullOrWhiteSpace(groupId)) throw new ArgumentNullException(nameof(groupId));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{PathPrefix}/groups/{groupId}/artifacts/{artifactId}/meta");
        using var response = await this.HttpClient.SendAsync(request, cancellationToken);
        var json = await response.Content?.ReadAsStringAsync(cancellationToken)!;
        if (!response.IsSuccessStatusCode)
        {
            this.Logger.LogError("An error occured while retrieving the metadata of the artifact with the specified id '{artifactId}': the remote server responded with a non-success status code '{statusCode}'./r/Response content: {json}", artifactId, response.StatusCode, json);
            response.EnsureSuccessStatusCode();
        }
        return Serializer.Json.Deserialize<Artifact>(json)!;
    }
}