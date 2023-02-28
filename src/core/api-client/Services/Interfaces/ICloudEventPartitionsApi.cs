using CloudStreams.Core.Data.Models;

namespace CloudStreams.Core.Api.Client.Services;

/// <summary>
/// Defines the fundamentals of the Cloud Streams gateway API used to manage <see cref="CloudEvent"/> partitions
/// </summary>
public interface ICloudEventPartitionsApi
{

    /// <summary>
    /// Gets the metadata used to describe the specified <see cref="CloudEvent"/> partition
    /// </summary>
    /// <param name="type">The type of the <see cref="CloudEvent"/> partition to get the metadata of</param>
    /// <param name="id">The id of the <see cref="CloudEvent"/> partition to get the metadata of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="PartitionMetadata"/> used to describe the specified <see cref="CloudEvent"/> partition</returns>
    Task<PartitionMetadata?> GetPartitionMetadataAsync(CloudEventPartitionType type, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the metadata of all partitions of the specified type
    /// </summary>
    /// <param name="type">The type of the partitions to list the metadata of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> used to enumerate the metadata of all partitions of the specified type</returns>
    Task<IAsyncEnumerable<PartitionMetadata?>> ListPartitionsByTypeAsync(CloudEventPartitionType type, CancellationToken cancellationToken = default);

}