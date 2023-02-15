using CloudStreams.Infrastructure.SchemaRegistry.Apicurio.Models;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace CloudStreams.Infrastructure.SchemaRegistry.Apicurio.Services;

public partial class ApicurioRegistryApiClient
    : IArtifactsApi
{

    async Task<Artifact> IArtifactsApi.CreateArtifactAsync(ArtifactType artifactType, string content, IfArtifactExistsAction ifExists, string? artifactId, string groupId, bool canonical, string? version, string? name, string? description, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(groupId)) throw new ArgumentNullException(nameof(groupId));
        var formattedContent = await this.FormatAsync(content, cancellationToken);
        using var httpContent = new StringContent(formattedContent, Encoding.UTF8);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{PathPrefix}/groups/{groupId}/artifacts?ifExists={EnumHelper.Stringify(ifExists)}&canonical={canonical}") { Content = httpContent };
        request.Headers.TryAddWithoutValidation("X-Registry-ArtifactType", EnumHelper.Stringify(artifactType));
        if (!string.IsNullOrWhiteSpace(artifactId)) request.Headers.TryAddWithoutValidation("X-Registry-ArtifactId", artifactId);
        if (!string.IsNullOrWhiteSpace(version)) request.Headers.TryAddWithoutValidation("X-Registry-Version", version);
        if (!string.IsNullOrWhiteSpace(name)) request.Headers.TryAddWithoutValidation("X-Registry-Name", name);
        if (!string.IsNullOrWhiteSpace(description)) request.Headers.TryAddWithoutValidation("X-Registry-Description", description);
        request.Headers.TryAddWithoutValidation("X-Registry-Content-Hash", HashHelper.SHA256Hash(formattedContent));
        request.Headers.TryAddWithoutValidation("X-Registry-Content-Algorithm", HashAlgorithmName.SHA256.Name);
        using var response = await this.HttpClient.SendAsync(request, cancellationToken);
        var json = await response.Content?.ReadAsStringAsync(cancellationToken)!;
        if (!response.IsSuccessStatusCode)
        {
            this.Logger.LogError("An error occured while creating a new artifact: the remote server responded with a non-success status code '{statusCode}'./r/Response content: {json}", json, response.StatusCode);
            response.EnsureSuccessStatusCode();
        }
        return Serializer.Json.Deserialize<Artifact>(json)!;
    }

    async Task<string?> IArtifactsApi.GetArtifactContentByIdAsync(long contentId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{PathPrefix}/ids/contentIds/{contentId}/");
        using var response = await this.HttpClient.SendAsync(request, cancellationToken);
        var content = await response.Content?.ReadAsStringAsync(cancellationToken)!;
        if (!response.IsSuccessStatusCode)
        {
            this.Logger.LogError("An error occured while retrieving the artifact with the specified content id '{contentId}': the remote server responded with a non-success status code '{statusCode}'./r/Response content: {json}", contentId, response.StatusCode, content);
            response.EnsureSuccessStatusCode();
        }
        return content;
    }

    async Task<string> IArtifactsApi.GetLatestArtifactAsync(string artifactId, string groupId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(artifactId))throw new ArgumentNullException(nameof(artifactId));
        if (string.IsNullOrWhiteSpace(groupId))throw new ArgumentNullException(nameof(groupId));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{PathPrefix}/groups/{groupId}/artifacts/{artifactId}");
        using var response = await this.HttpClient.SendAsync(request, cancellationToken);
        var content = await response.Content?.ReadAsStringAsync(cancellationToken)!;
        if (!response.IsSuccessStatusCode)
        {
            this.Logger.LogError("An error occured while retrieving the latest version of the artifact with the specified id '{artifactId}': the remote server responded with a non-success status code '{statusCode}'./r/Response content: {json}", artifactId, response.StatusCode, content);
            response.EnsureSuccessStatusCode();
        }
        return content;
    }

}