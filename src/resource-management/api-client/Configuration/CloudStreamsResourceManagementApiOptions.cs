using CloudStreams.ResourceManagement.Api.Client.Services;

namespace CloudStreams.ResourceManagement.Api.Client.Configuration;

/// <summary>
/// Represents the options used to configure an <see cref="ICloudStreamsResourceManagementApiClient"/>
/// </summary>
public class CloudStreamResourceManagementApiClientOptions
{

    /// <summary>
    /// Gets/sets the base address of the Cloud Streams API to connect to
    /// </summary>
    public virtual string BaseAddress { get; set; } = null!;

}
