using CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Models;

namespace CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Services;

/// <summary>
/// Defines the fundamentals of the Apicurio Registry metadata API
/// </summary>
public interface IMetadataApi
{

    /// <summary>
    /// Gets the metadata of the <see cref="Artifact"/> with the specified id
    /// </summary>
    /// <param name="artifactId">The id of the <see cref="Artifact"/> to get the metadata of</param>
    /// <param name="groupId">The id of the group the <see cref="Artifact"/> to get the metadata of belongs to</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The metadata of the <see cref="Artifact"/> with the specified id</returns>
    Task<Artifact> GetArtifactMetadataAsync(string artifactId, string groupId, CancellationToken cancellationToken = default);

}
