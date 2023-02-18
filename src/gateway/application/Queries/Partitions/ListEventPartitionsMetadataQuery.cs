namespace CloudStreams.Gateway.Application.Queries.Partitions;

/// <summary>
/// Represents the <see cref="IQuery{TResult}"/> used to list the metadata of event partitions
/// </summary>
public class ListEventPartitionsMetadataQuery
    : IQuery<IAsyncEnumerable<CloudEventPartitionMetadata>>
{

    /// <summary>
    /// Initializes a new <see cref="ListEventPartitionsMetadataQuery"/>
    /// </summary>
    /// <param name="partitionType">The type of partitions to list the metadata of</param>
    public ListEventPartitionsMetadataQuery(CloudEventPartitionType partitionType)
    {
        this.PartitionType = partitionType;
    }

    /// <summary>
    /// Gets the type of partitions to list the metadata of
    /// </summary>
    [Required]
    public virtual CloudEventPartitionType PartitionType { get; set; }

}

/// <summary>
/// Represents the service used to handle <see cref="ListEventPartitionsMetadataQuery"/> instances
/// </summary>
public class ListEventPartitionsMetadataQueryHandler
    : IQueryHandler<ListEventPartitionsMetadataQuery, IAsyncEnumerable<CloudEventPartitionMetadata>>
{

    /// <inheritdoc/>
    public ListEventPartitionsMetadataQueryHandler(ICloudEventStore cloudEvents)
    {
        this._CloudEvents = cloudEvents;
    }

    ICloudEventStore _CloudEvents;

    Task<Response<IAsyncEnumerable<CloudEventPartitionMetadata>>> MediatR.IRequestHandler<ListEventPartitionsMetadataQuery, Response<IAsyncEnumerable<CloudEventPartitionMetadata>>>.Handle(ListEventPartitionsMetadataQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(this.Ok(this._CloudEvents.ListPartitionsMetadataAsync(query.PartitionType, cancellationToken)));
    }

}
