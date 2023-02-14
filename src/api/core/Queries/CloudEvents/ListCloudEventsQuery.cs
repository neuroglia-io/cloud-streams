using CloudStreams.Data.Models;
using CloudStreams.Infrastructure.Services;

namespace CloudStreams.Api.Queries.CloudEvents;

/// <summary>
/// Represents the <see cref="IQuery{TResult}"/> used to list stored <see cref="CloudEvent"/>s
/// </summary>
public class ListCloudEventsQuery
    : IQuery<IAsyncEnumerable<CloudEvent>>
{

    /// <summary>
    /// Initializes a new <see cref="ListCloudEventsQuery"/>
    /// </summary>
    /// <param name="options">The object used to configure the query to perform</param>
    public ListCloudEventsQuery(CloudEventStreamReadOptions options)
    {
        this.Options = options;
    }

    /// <summary>
    /// Gets the object used to configure the query to perform
    /// </summary>
    public CloudEventStreamReadOptions Options { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="ListCloudEventsQuery"/> instances
/// </summary>
public class ListCloudEventsQueryHandler
    : IQueryHandler<ListCloudEventsQuery, IAsyncEnumerable<CloudEvent>>
{

    /// <inheritdoc/>
    public ListCloudEventsQueryHandler(ICloudEventStore eventStore)
    {
        this._EventStore = eventStore;
    }

    ICloudEventStore _EventStore;

    /// <inheritdoc/>
    public Task<Response<IAsyncEnumerable<CloudEvent>>> Handle(ListCloudEventsQuery query, CancellationToken cancellationToken)
    {
        var length = query.Options.Length > CloudEventStreamReadOptions.MaxLength ? CloudEventStreamReadOptions.MaxLength : query.Options.Length;
        if (length < 1) length = 1;
        var offset = query.Options.Offset;
        if (!offset.HasValue)
        {
            switch (query.Options.Direction)
            {
                case StreamReadDirection.Forwards:
                    offset = CloudEventStreamPosition.Start;
                    break;
                case StreamReadDirection.Backwards:
                    offset = CloudEventStreamPosition.End;
                    break;
                default:
                    return Task.FromResult(this.ValidationFailed(new KeyValuePair<string, string[]>[] { new(nameof(query.Options.Direction).ToLowerInvariant(), new string[] { $"The specified {nameof(StreamReadDirection)} '{query.Options.Direction}' is not supported" })}));
            }
        }
        var events = query.Options.Partition == null ? 
            this._EventStore.ReadAsync(query.Options.Direction, offset.Value, length, cancellationToken: cancellationToken) 
            : this._EventStore.ReadPartitionAsync(query.Options.Partition, query.Options.Direction, offset.Value, length, cancellationToken: cancellationToken);
        return Task.FromResult(this.Ok(events));
    }

}
