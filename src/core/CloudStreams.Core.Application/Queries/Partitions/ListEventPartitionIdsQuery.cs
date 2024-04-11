using CloudStreams.Core.Application.Services;
using System.ComponentModel.DataAnnotations;

namespace CloudStreams.Core.Application.Queries.Partitions;

/// <summary>
/// Represents the <see cref="IQuery{TResult}"/> used to list the ids of event partitions
/// </summary>
/// <remarks>
/// Initializes a new <see cref="ListEventPartitionIdsQuery"/>
/// </remarks>
/// <param name="partitionType">The type of partitions to list the ids of</param>
public class ListEventPartitionIdsQuery(CloudEventPartitionType partitionType)
        : Query<IAsyncEnumerable<string>>
{

    /// <summary>
    /// Gets the type of partitions to list the ids of
    /// </summary>
    [Required]
    public virtual CloudEventPartitionType PartitionType { get; set; } = partitionType;

}

/// <summary>
/// Represents the service used to handle <see cref="ListEventPartitionIdsQuery"/> instances
/// </summary>
public class ListEventPartitionIdsQueryHandler(ICloudEventStore eventStore)
    : IQueryHandler<ListEventPartitionIdsQuery, IAsyncEnumerable<string>>
{

    /// <inheritdoc/>
    public virtual Task<IOperationResult<IAsyncEnumerable<string>>> HandleAsync(ListEventPartitionIdsQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(this.Ok(eventStore.ListPartitionIdsAsync(query.PartitionType, cancellationToken)));
    }

}
