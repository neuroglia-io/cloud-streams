using CloudStreams.Core.Api.Client.Services;

namespace CloudStreams.ResourceManagement.Api.Client.Configuration;

/// <summary>
/// Represents the options used to configure an <see cref="ICloudStreamsCoreApiClient"/>
/// </summary>
public class CoreApiClientOptions
{

    /// <summary>
    /// Gets/sets the base address of the Cloud Streams API to connect to
    /// </summary>
    public virtual string BaseAddress { get; set; } = null!;

}
