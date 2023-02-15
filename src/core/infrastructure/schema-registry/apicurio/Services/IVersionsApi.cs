using CloudStreams.Infrastructure.SchemaRegistry.Apicurio.Models;

namespace CloudStreams.Infrastructure.SchemaRegistry.Apicurio.Services;

/// <summary>
/// Defines the fundamentals of the Apicurio Registry versioning API
/// </summary>
public interface IVersionsApi
{

    /// <summary>
    /// Gets the specified <see cref="Artifact"/> version
    /// </summary>
    /// <param name="artifactId">The id of the <see cref="Artifact"/> to get</param>
    /// <param name="groupId">The id of the group the <see cref="Artifact"/> to get belongs to</param>
    /// <param name="version">The version of the <see cref="Artifact"/> to get</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The content of the <see cref="Artifact"/> with the specified id and version</returns>
    Task<string> GetArtifactVersionAsync(string artifactId, string groupId, string version, CancellationToken cancellationToken = default);

}