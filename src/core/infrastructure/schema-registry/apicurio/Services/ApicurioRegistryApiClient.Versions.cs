using Microsoft.Extensions.Logging;

namespace CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Services;

public partial class ApicurioRegistryApiClient
    : IVersionsApi
{

    async Task<string> IVersionsApi.GetArtifactVersionAsync(string artifactId, string groupId, string version, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(artifactId)) throw new ArgumentNullException(nameof(artifactId));
        if (string.IsNullOrWhiteSpace(groupId)) throw new ArgumentNullException(nameof(groupId));
        if (string.IsNullOrWhiteSpace(version)) throw new ArgumentNullException(nameof(version));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{PathPrefix}/groups/{groupId}/artifacts/{artifactId}/versions/{version}");
        using var response = await this.HttpClient.SendAsync(request, cancellationToken);
        var content = await response.Content?.ReadAsStringAsync(cancellationToken)!;
        if (!response.IsSuccessStatusCode)
        {
            this.Logger.LogError("An error occured while retrieving the artifact with the specified id '{artifactId}' and '{version}': the remote server responded with a non-success status code '{statusCode}'./r/Response content: {json}", artifactId, version, response.StatusCode, content);
            response.EnsureSuccessStatusCode();
        }
        return content;
    }

}