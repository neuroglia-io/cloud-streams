namespace CloudStreams.Core.Application.Queries.Streams;

/// <summary>
/// Represents the <see cref="IQuery{TResult}"/> used to list stored <see cref="CloudEvent"/>s
/// </summary>
public class ReadEventStreamQuery
    : IQuery<IAsyncEnumerable<CloudEvent>>
{

    /// <summary>
    /// Initializes a new <see cref="ReadEventStreamQuery"/>
    /// </summary>
    /// <param name="options">The object used to configure the query to perform</param>
    public ReadEventStreamQuery(CloudEventStreamReadOptions options)
    {
        Options = options;
    }

    /// <summary>
    /// Gets the object used to configure the query to perform
    /// </summary>
    public CloudEventStreamReadOptions Options { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="ReadEventStreamQuery"/> instances
/// </summary>
public class ReadCloudEventStreamQueryHandler
    : IQueryHandler<ReadEventStreamQuery, IAsyncEnumerable<CloudEvent>>
{

    /// <inheritdoc/>
    public ReadCloudEventStreamQueryHandler(ICloudEventStore eventStore)
    {
        this._EventStore = eventStore;
    }

    ICloudEventStore _EventStore;

    /// <inheritdoc/>
    public Task<Response<IAsyncEnumerable<CloudEvent>>> Handle(ReadEventStreamQuery query, CancellationToken cancellationToken)
    {
        var length = query.Options.Length > CloudEventStreamReadOptions.MaxLength ? CloudEventStreamReadOptions.MaxLength : query.Options.Length;
        if (length < 1) length = 1;
        var offset = query.Options.Offset;
        if (!offset.HasValue)
        {
            switch (query.Options.Direction)
            {
                case StreamReadDirection.Forwards:
                    offset = CloudEventStreamPosition.StartOfStream;
                    break;
                case StreamReadDirection.Backwards:
                    offset = CloudEventStreamPosition.EndOfStream;
                    break;
                default:
                    return Task.FromResult(this.ValidationFailed(new KeyValuePair<string, string[]>[] { new(nameof(query.Options.Direction).ToLowerInvariant(), new string[] { $"The specified {nameof(StreamReadDirection)} '{query.Options.Direction}' is not supported" }) }));
            }
        }
        var events = query.Options.Partition == null ?
            this._EventStore.ReadAsync(query.Options.Direction, offset.Value, length, cancellationToken: cancellationToken)
            : this._EventStore.ReadPartitionAsync(query.Options.Partition, query.Options.Direction, offset.Value, length, cancellationToken: cancellationToken);
        return Task.FromResult(this.Ok(events));
    }

}
