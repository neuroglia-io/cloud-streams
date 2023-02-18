namespace CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Services;

/// <summary>
/// Defines the fundamentals of an Apicurio Registry client
/// </summary>
public interface IApicurioRegistryApiClient
{

    /// <summary>
    /// Gets the artifacts API
    /// </summary>
    IArtifactsApi Artifacts { get; }

    /// <summary>
    /// Gets the metadata API
    /// </summary>
    IMetadataApi Metadata { get; }

    /// <summary>
    /// Gets the versions API
    /// </summary>
    IVersionsApi Versions { get; }

}
