namespace CloudStreams.Core.Application.Queries.Partitions;

/// <summary>
/// Represents the <see cref="IQuery{TResult}"/> used to list the ids of event partitions
/// </summary>
public class ListEventPartitionIdsQuery
    : IQuery<IAsyncEnumerable<string>>
{

    /// <summary>
    /// Initializes a new <see cref="ListEventPartitionIdsQuery"/>
    /// </summary>
    /// <param name="partitionType">The type of partitions to list the ids of</param>
    public ListEventPartitionIdsQuery(CloudEventPartitionType partitionType)
    {
        this.PartitionType = partitionType;
    }

    /// <summary>
    /// Gets the type of partitions to list the ids of
    /// </summary>
    [Required]
    public virtual CloudEventPartitionType PartitionType { get; set; }

}

/// <summary>
/// Represents the service used to handle <see cref="ListEventPartitionIdsQuery"/> instances
/// </summary>
public class ListEventPartitionIdsQueryHandler
    : IQueryHandler<ListEventPartitionIdsQuery, IAsyncEnumerable<string>>
{

    /// <inheritdoc/>
    public ListEventPartitionIdsQueryHandler(ICloudEventStore cloudEvents)
    {
        this._CloudEvents = cloudEvents;
    }

    ICloudEventStore _CloudEvents;

    Task<Response<IAsyncEnumerable<string>>> MediatR.IRequestHandler<ListEventPartitionIdsQuery, Response<IAsyncEnumerable<string>>>.Handle(ListEventPartitionIdsQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(this.Ok(this._CloudEvents.ListPartitionIdsAsync(query.PartitionType, cancellationToken)));
    }

}
