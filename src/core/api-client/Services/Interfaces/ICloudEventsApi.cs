using CloudStreams.Core.Data.Models;

namespace CloudStreams.Core.Api.Client.Services;

/// <summary>
/// Defines the fundamentals of the Cloud Streams API used to manage <see cref="CloudEvent"/>s
/// </summary>
public interface ICloudEventsApi
{

    /// <summary>
    /// Gets the API used to manage <see cref="CloudEvent"/> partitions
    /// </summary>
    ICloudEventPartitionsApi Partitions { get; }

    /// <summary>
    /// Gets the API used to manage the gateway's <see cref="CloudEvent"/> stream
    /// </summary>
    ICloudEventStreamApi Stream { get; }

}
