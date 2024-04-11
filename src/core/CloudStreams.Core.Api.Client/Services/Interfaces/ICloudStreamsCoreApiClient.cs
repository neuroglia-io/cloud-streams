namespace CloudStreams.Core.Api.Client.Services;

/// <summary>
/// Defines the fundamentals of a service used to interact with a Cloud Streams gateway's API
/// </summary>
public interface ICloudStreamsCoreApiClient
{

    /// <summary>
    /// Gets the API used to manage cloud events
    /// </summary>
    ICloudEventsApi CloudEvents { get; }

    /// <summary>
    /// Gets the API used to manage resources
    /// </summary>
    IResourceManagementApiClient Resources { get; }

}
