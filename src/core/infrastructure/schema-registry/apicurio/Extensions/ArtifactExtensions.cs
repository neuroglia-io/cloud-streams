using CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Models;

namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="Artifact"/>s
/// </summary>
public static class ArtifactExtensions
{

    /// <summary>
    /// Gets the path to the <see cref="Artifact"/>
    /// </summary>
    /// <param name="artifact">The <see cref="Artifact"/>'s path</param>
    /// <returns>The <see cref="Artifact"/>'s path</returns>
    public static string GetPath(this Artifact artifact) => $"apis/registry/v2/groups/{artifact.GroupId}/artifacts/{artifact.ContentId}/versions/{artifact.Version}";

}
