using CloudStreams.Data.Models;

namespace CloudStreams.Api.Gateway.Client.Services;

/// <summary>
/// Defines the fundamentals of the API used to manage a Cloud Streams gateway's <see cref="CloudEvent"/> stream 
/// </summary>
public interface ICloudEventStreamApi
{

    /// <summary>
    /// Reads the gateway's <see cref="CloudEvent"/> stream
    /// </summary>
    /// <param name="options">An object used to configure the read options</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing all the <see cref="CloudEvent"/>s read from the gateway's stream</returns>
    Task<IAsyncEnumerable<CloudEvent?>> ReadStreamAsync(CloudEventStreamReadOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the metadata used to describe the gateway's <see cref="CloudEvent"/> stream
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="CloudEventStreamMetadata"/> used to describe the gateway's <see cref="CloudEvent"/> stream</returns>
    Task<CloudEventStreamMetadata> GetStreamMetadataAsync(CancellationToken cancellationToken = default);

}
