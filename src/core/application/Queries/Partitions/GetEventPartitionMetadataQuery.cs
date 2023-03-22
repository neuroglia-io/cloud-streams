namespace CloudStreams.Core.Application.Queries.Partitions;

/// <summary>
/// Represents the <see cref="IQuery{TResult}"/> used to get the metadata of the application's cloud event stream
/// </summary>
public class GetEventPartitionMetadataQuery
    : IQuery<PartitionMetadata>
{

    /// <summary>
    /// Initializes a new <see cref="GetEventPartitionMetadataQuery"/>
    /// </summary>
    /// <param name="partition">The cloud event partition to get metadata for</param>
    public GetEventPartitionMetadataQuery(PartitionReference partition)
    {
        this.Partition = partition;
    }

    /// <summary>
    /// Gets the cloud event partition to get metadata for
    /// </summary>
    [Required]
    public virtual PartitionReference Partition { get; protected set; }

}

/// <summary>
/// Represents the service used to handle <see cref="GetEventPartitionMetadataQuery"/> instances
/// </summary>
public class GetEventStreamMetadataQueryHandler
    : IQueryHandler<GetEventPartitionMetadataQuery, PartitionMetadata>
{

    /// <inheritdoc/>
    public GetEventStreamMetadataQueryHandler(ICloudEventStore cloudEvents)
    {
        this._CloudEvents = cloudEvents;
    }

    ICloudEventStore _CloudEvents;

    async Task<Response<PartitionMetadata>> MediatR.IRequestHandler<GetEventPartitionMetadataQuery, Response<PartitionMetadata>>.Handle(GetEventPartitionMetadataQuery query, CancellationToken cancellationToken)
    {
        return this.Ok(await this._CloudEvents.GetPartitionMetadataAsync(query.Partition, cancellationToken));
    }

}
