using CloudStreams.Core.Data.Models;

namespace CloudStreams.Core.Api.Client.Services;

/// <summary>
/// Defines the fundamentals of a service used to interact with a Cloud Streams API server
/// </summary>
public interface ICloudStreamsApiClient
{

    /// <summary>
    /// Gets the API used to manage <see cref="CloudEvent"/>s
    /// </summary>
    ICloudEventsApi CloudEvents { get; }

}