using CloudStreams.Infrastructure.SchemaRegistry.Apicurio.Services;

namespace CloudStreams.Infrastructure.SchemaRegistry.Apicurio.Configuration;

/// <summary>
/// Represents the options used to configure an <see cref="IApicurioRegistryApiClient"/>
/// </summary>
public class ApiCurioRegistryClientOptions
{

    /// <summary>
    /// Gets/sets the Api Curio Registry server uri
    /// </summary>
    public virtual Uri ServerUri { get; set; } = null!;

    /// <summary>
    /// Gets/sets the id of the artifact group to use by default. Defaults to 'cloud-events'
    /// </summary>
    public virtual string DefaultGroupId { get; set; } = "cloud-events";

    /// <summary>
    /// Gets/sets the line ending format
    /// </summary>
    public virtual LineEndingFormatMode LineEndingFormatMode { get; set; } = LineEndingFormatMode.Preserve;

}
