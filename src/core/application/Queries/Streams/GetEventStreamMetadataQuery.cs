namespace CloudStreams.Core.Application.Queries.Streams;

/// <summary>
/// Represents the <see cref="IQuery{TResult}"/> used to get the metadata of the application's cloud event stream
/// </summary>
public class GetEventStreamMetadataQuery
    : IQuery<StreamMetadata>
{



}

/// <summary>
/// Represents the service used to handle <see cref="GetEventStreamMetadataQuery"/> instances
/// </summary>
public class GetEventPartitionMetadataQueryHandler
    : IQueryHandler<GetEventStreamMetadataQuery, StreamMetadata>
{

    /// <inheritdoc/>
    public GetEventPartitionMetadataQueryHandler(ICloudEventStore cloudEvents)
    {
        this._CloudEvents = cloudEvents;
    }

    ICloudEventStore _CloudEvents;

    async Task<Response<StreamMetadata>> MediatR.IRequestHandler<GetEventStreamMetadataQuery, Response<StreamMetadata>>.Handle(GetEventStreamMetadataQuery request, CancellationToken cancellationToken)
    {
        return this.Ok(await this._CloudEvents.GetStreamMetadataAsync(cancellationToken));
    }

}
