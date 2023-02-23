using CloudStreams.Gateway.Api.Client.Services;

namespace CloudStreams.Gateway.Api.Client.Configuration;

/// <summary>
/// Represents the options used to configure an <see cref="ICloudStreamsGatewayApiClient"/>
/// </summary>
public class CloudStreamGatewayApiClientOptions
{

    /// <summary>
    /// Gets/sets the base address of the Cloud Streams API to connect to
    /// </summary>
    public virtual string BaseAddress { get; set; } = null!;

}
