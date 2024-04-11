using CloudStreams.Core.Application.Services;
using System.ComponentModel.DataAnnotations;

namespace CloudStreams.Core.Application.Queries.Partitions;

/// <summary>
/// Represents the <see cref="IQuery{TResult}"/> used to get the metadata of the application's cloud event stream
/// </summary>
/// <remarks>
/// Initializes a new <see cref="GetEventPartitionMetadataQuery"/>
/// </remarks>
/// <param name="partition">The cloud event partition to get metadata for</param>
public class GetEventPartitionMetadataQuery(PartitionReference partition)
    : Query<PartitionMetadata>
{

    /// <summary>
    /// Gets the cloud event partition to get metadata for
    /// </summary>
    [Required]
    public virtual PartitionReference Partition { get; protected set; } = partition;

}

/// <summary>
/// Represents the service used to handle <see cref="GetEventPartitionMetadataQuery"/> instances
/// </summary>
public class GetEventStreamMetadataQueryHandler(ICloudEventStore eventStore)
    : IQueryHandler<GetEventPartitionMetadataQuery, PartitionMetadata>
{

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<PartitionMetadata>> HandleAsync(GetEventPartitionMetadataQuery query, CancellationToken cancellationToken)
    {
        return this.Ok(await eventStore.GetPartitionMetadataAsync(query.Partition, cancellationToken));
    }

}
