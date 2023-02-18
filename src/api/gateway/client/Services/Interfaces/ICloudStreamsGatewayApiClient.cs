using CloudStreams.Data.Models;

namespace CloudStreams.Api.Gateway.Client.Services;

/// <summary>
/// Defines the fundamentals of a service used to interact with a Cloud Streams gateway's API
/// </summary>
public interface ICloudStreamsGatewayApiClient
{

    /// <summary>
    /// Gets the API used to manage <see cref="CloudEvent"/>s
    /// </summary>
    ICloudEventsApi CloudEvents { get; }

}