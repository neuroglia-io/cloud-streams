using CloudStreams.Data.Models;
using CloudStreams.Infrastructure.Services;

namespace CloudStreams.Api.Queries.Streams;

/// <summary>
/// Represents the <see cref="IQuery{TResult}"/> used to get the metadata of the application's cloud event stream
/// </summary>
public class GetEventStreamMetadataQuery
    : IQuery<CloudEventStreamMetadata>
{



}

/// <summary>
/// Represents the service used to handle <see cref="GetEventPartitionMetadataQuery"/> instances
/// </summary>
public class GetEventPartitionMetadataQueryHandler
    : IQueryHandler<GetEventStreamMetadataQuery, CloudEventStreamMetadata>
{

    /// <inheritdoc/>
    public GetEventPartitionMetadataQueryHandler(ICloudEventStore cloudEvents)
    {
        this._CloudEvents = cloudEvents;
    }

    ICloudEventStore _CloudEvents;

    async Task<Response<CloudEventStreamMetadata>> MediatR.IRequestHandler<GetEventStreamMetadataQuery, Response<CloudEventStreamMetadata>>.Handle(GetEventStreamMetadataQuery request, CancellationToken cancellationToken)
    {
        return this.Ok(await this._CloudEvents.GetStreamMetadataAsync(cancellationToken));
    }

}
