using CloudStreams.Core.Data.Models;
using CloudStreams.Core.Infrastructure.Models;
using System.Net.Mime;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents the <see href="https://www.eventstore.com/">EventStore</see> based implementation of the <see cref="ICloudEventStore"/> interface
/// </summary>
public class ESCloudEventStore
    : BackgroundService, ICloudEventStore
{

    /// <summary>
    /// Initializes a new <see cref="ESCloudEventStore"/>
    /// </summary>
    /// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
    /// <param name="streams">The service used to interact with the <see href="https://www.eventstore.com/">EventStore</see> streams API</param>
    /// <param name="projections">The service used to interact with the <see href="https://www.eventstore.com/">EventStore</see> projections API</param>
    public ESCloudEventStore(ILoggerFactory loggerFactory, EventStoreClient streams, EventStoreProjectionManagementClient projections)
    {
        this.Logger = loggerFactory.CreateLogger(this.GetType());
        this.Streams = streams;
        this.Projections = projections;
    }

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the service used to interact with the <see href="https://www.eventstore.com/">EventStore</see> streams API
    /// </summary>
    protected EventStoreClient Streams { get; set; }

    /// <summary>
    /// Gets the service used to interact with the <see href="https://www.eventstore.com/">EventStore</see> projections API
    /// </summary>
    protected EventStoreProjectionManagementClient Projections { get; set; }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await this.SetupProjectionsAsync(stoppingToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public virtual async Task<ulong> AppendAsync(CloudEvent e, CancellationToken cancellationToken = default)
    {
        if (e == null) throw new ArgumentNullException(nameof(e));
        var streamName = EventStoreStreams.All;
        var eventData = await this.SerializeAsync(e, cancellationToken).ConfigureAwait(false);
        var writeResult = await this.Streams.AppendToStreamAsync(streamName, StreamState.Any, new EventData[] { eventData }, cancellationToken: cancellationToken).ConfigureAwait(false);
        return writeResult.NextExpectedStreamRevision.ToUInt64();
    }

    /// <inheritdoc/>
    public virtual async Task<Data.Models.StreamMetadata> GetStreamMetadataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var firstEvent = (await this.Streams.ReadStreamAsync(Direction.Forwards, EventStoreStreams.All, EventStore.Client.StreamPosition.Start, 1, cancellationToken: cancellationToken).ToListAsync(cancellationToken)).Single();
            var lastEvent = (await this.Streams.ReadStreamAsync(Direction.Backwards, EventStoreStreams.All, EventStore.Client.StreamPosition.End, 1, cancellationToken: cancellationToken).ToListAsync(cancellationToken)).Single();
            return new()
            {
                FirstEvent = firstEvent.OriginalEvent.Created,
                LastEvent = lastEvent.OriginalEvent.Created,
                Length = lastEvent.OriginalEventNumber + 1
            };
        }
        catch (StreamNotFoundException)
        {
            return new();
        }
    }

    /// <inheritdoc/>
    public virtual async Task<PartitionMetadata> GetPartitionMetadataAsync(PartitionReference partition, CancellationToken cancellationToken = default)
    {
        var streamName = partition.GetStreamName();
        var firstEvent = (await this.Streams.ReadStreamAsync(Direction.Forwards, streamName, EventStore.Client.StreamPosition.Start, 1, cancellationToken: cancellationToken).ToListAsync(cancellationToken)).Single();
        var lastEvent = (await this.Streams.ReadStreamAsync(Direction.Backwards, streamName, EventStore.Client.StreamPosition.End, 1, cancellationToken: cancellationToken).ToListAsync(cancellationToken)).Single();
        return new()
        {
            Id = partition.Id,
            Type = partition.Type,
            FirstEvent = firstEvent.OriginalEvent.Created,
            LastEvent = lastEvent.OriginalEvent.Created,
            Length = lastEvent.OriginalEventNumber + 1
        };
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<string> ListPartitionIdsAsync(CloudEventPartitionType partitionType, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var typeName = partitionType.ToString();
        var partitionsIds = await this.Projections.GetResultAsync<List<string>>(EventStoreProjections.CloudEventPartitionsMetadataPrefix + typeName, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (partitionsIds == null)
        {
            throw new NullReferenceException(nameof(partitionsIds));
        }
        await foreach(var id in partitionsIds.ToAsyncEnumerable())
        {
            yield return id;
        }
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<CloudEvent> ReadAsync(StreamReadDirection readDirection, long offset, ulong? length = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var streamName = EventStoreStreams.All;
        var resolveLinkTos = true;
        var position = offset == StreamPosition.EndOfStream ? EventStore.Client.StreamPosition.End : EventStore.Client.StreamPosition.FromInt64(offset);
        var readResult = this.Streams.ReadStreamAsync(readDirection.ToDirection(), streamName, position, (long)(length ?? long.MaxValue), resolveLinkTos, cancellationToken: cancellationToken);
        await foreach (var resolvedEvent in readResult)
        {
            yield return await this.DeserializeAsync(resolvedEvent, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public virtual IAsyncEnumerable<CloudEvent> ReadPartitionAsync(PartitionReference partition, StreamReadDirection readDirection, long offset, ulong? length = null, CancellationToken cancellationToken = default)
    {
        switch (partition.Type)
        {
            case CloudEventPartitionType.BySource:
                if (!Uri.TryCreate(partition.Id, UriKind.RelativeOrAbsolute, out var source)) throw new Exception();
                return this.ReadBySourceAsync(readDirection, source, offset, length, cancellationToken);
            case CloudEventPartitionType.ByType:
                return this.ReadByTypeAsync(readDirection, partition.Id, offset, length, cancellationToken);
            case CloudEventPartitionType.BySubject:
                return this.ReadByCorrelationIdAsync(readDirection, partition.Id, offset, length, cancellationToken);
            default:
                throw new NotSupportedException($"The specified {nameof(CloudEventPartitionType)} '{partition.Type}' is not supported");
        }
    }

    /// <inheritdoc/>
    public virtual async Task<IObservable<CloudEvent>> SubscribeAsync(long offset = StreamPosition.EndOfStream, CancellationToken cancellationToken = default)
    {
        var subject = new Subject<CloudEvent>();
        var subscription = await this.Streams.SubscribeToStreamAsync(
            EventStoreStreams.All, 
            offset.ToSubscriptionPosition(), 
            async (sub, e, cancellation) => subject.OnNext(await this.DeserializeAsync(e, cancellation).ConfigureAwait(false)), 
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Observable.Using
        (
            () => subscription,
            watch => subject
        );
    }

    /// <inheritdoc/>
    public virtual async Task<IObservable<CloudEvent>> SubscribeToPartitionAsync(PartitionReference partition, long offset = StreamPosition.EndOfStream, CancellationToken cancellationToken = default)
    {
        var subject = new Subject<CloudEvent>();
        var subscription = await this.Streams.SubscribeToStreamAsync(
            partition.GetStreamName(),
            offset.ToSubscriptionPosition(), 
            async (sub, e, cancellation) => subject.OnNext(await this.DeserializeAsync(e, cancellation).ConfigureAwait(false)), 
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Observable.Using
        (
            () => subscription,
            watch => subject
        );
    }

    /// <summary>
    /// Reads stored <see cref="CloudEvent"/>s by source
    /// </summary>
    /// <param name="readDirection">The direction in which to read</param>
    /// <param name="source">The source of the <see cref="CloudEvent"/>s to read</param>
    /// <param name="offset">The offset starting from which to read events</param>
    /// <param name="length">The amount of <see cref="CloudEvent"/>s to read</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing the <see cref="CloudEvent"/>s read from the store</returns>
    protected virtual async IAsyncEnumerable<CloudEvent> ReadBySourceAsync(StreamReadDirection readDirection, Uri source, long offset, ulong? length = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        var streamName = EventStoreStreams.ByCloudEventSource(source);
        var resolveLinkTos = true;
        var position = offset == StreamPosition.EndOfStream ? EventStore.Client.StreamPosition.End : EventStore.Client.StreamPosition.FromInt64(offset);
        var readResult = this.Streams.ReadStreamAsync(readDirection.ToDirection(), streamName, position, (long)(length ?? long.MaxValue), resolveLinkTos, cancellationToken: cancellationToken);
        await foreach (var resolvedEvent in readResult)
        {
            yield return await this.DeserializeAsync(resolvedEvent, cancellationToken);
        }
    }

    /// <summary>
    /// Reads stored <see cref="CloudEvent"/>s by type
    /// </summary>
    /// <param name="readDirection">The direction in which to read</param>
    /// <param name="type">The type of the <see cref="CloudEvent"/>s to read</param>
    /// <param name="offset">The offset starting from which to read events</param>
    /// <param name="length">The amount of <see cref="CloudEvent"/>s to read</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing the <see cref="CloudEvent"/>s read from the store</returns>
    /// <inheritdoc/>
    protected virtual async IAsyncEnumerable<CloudEvent> ReadByTypeAsync(StreamReadDirection readDirection, string type, long offset, ulong? length = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(type)) throw new ArgumentNullException(nameof(type));
        var streamName = EventStoreStreams.ByCloudEventType(type);
        var resolveLinkTos = true;
        var position = offset == StreamPosition.EndOfStream ? EventStore.Client.StreamPosition.End : EventStore.Client.StreamPosition.FromInt64(offset);
        var readResult = this.Streams.ReadStreamAsync(readDirection.ToDirection(), streamName, position, (long)(length ?? long.MaxValue), resolveLinkTos, cancellationToken: cancellationToken);
        await foreach (var resolvedEvent in readResult)
        {
            yield return await this.DeserializeAsync(resolvedEvent, cancellationToken);
        }
    }

    /// <summary>
    /// Reads stored <see cref="CloudEvent"/>s by correlation id
    /// </summary>
    /// <param name="readDirection">The direction in which to read</param>
    /// <param name="correlationId">The correlation id of the <see cref="CloudEvent"/>s to read</param>
    /// <param name="offset">The offset starting from which to read events</param>
    /// <param name="length">The amount of <see cref="CloudEvent"/>s to read</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing the <see cref="CloudEvent"/>s read from the store</returns>
    protected virtual async IAsyncEnumerable<CloudEvent> ReadByCorrelationIdAsync(StreamReadDirection readDirection, string correlationId, long offset, ulong? length = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentNullException(nameof(correlationId));
        var streamName = EventStoreStreams.ByCorrelationId(correlationId);
        var resolveLinkTos = true;
        var position = offset == StreamPosition.EndOfStream ? EventStore.Client.StreamPosition.End : EventStore.Client.StreamPosition.FromInt64(offset);
        var readResult = this.Streams.ReadStreamAsync(readDirection.ToDirection(), streamName, position, (long)(length ?? long.MaxValue), resolveLinkTos, cancellationToken: cancellationToken);
        await foreach (var resolvedEvent in readResult)
        {
            yield return await this.DeserializeAsync(resolvedEvent, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public virtual async Task TruncateAsync(long beforeVersion, CancellationToken cancellationToken = default)
    {
        var streamName = EventStoreStreams.All;
        await this.Streams.SetStreamMetadataAsync(streamName, StreamState.Any, new EventStore.Client.StreamMetadata(truncateBefore: EventStore.Client.StreamPosition.FromInt64(beforeVersion)), cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates and configures the projections required by Cloud Streams
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task SetupProjectionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (await this.Projections.GetStatusAsync(EventStoreProjections.PartitionBySource, cancellationToken: cancellationToken) != null) return;
        }
        catch { }

        await this.Projections.EnableAsync(EventStoreProjections.BuiltInProjections.Streams, cancellationToken: cancellationToken);
        await this.Projections.EnableAsync(EventStoreProjections.BuiltInProjections.PartitionByEventType, cancellationToken: cancellationToken);
        await this.Projections.EnableAsync(EventStoreProjections.BuiltInProjections.PartitionByCorrelationId, cancellationToken: cancellationToken);

        await this.Projections.AbortAsync(EventStoreProjections.BuiltInProjections.PartitionByCategory, cancellationToken: cancellationToken);
        await this.Projections.DisableAsync(EventStoreProjections.BuiltInProjections.PartitionByCategory, cancellationToken: cancellationToken);

        await this.Projections.AbortAsync(EventStoreProjections.BuiltInProjections.StreamByCategory, cancellationToken: cancellationToken);
        await this.Projections.DisableAsync(EventStoreProjections.BuiltInProjections.StreamByCategory, cancellationToken: cancellationToken);

        Stream stream;
        StreamReader streamReader;
        string query;

        //todo: the following code does not configure the stream as expected, because handler 'js' does not seem to perform the same operations than built in handler
        // -> issue opened: https://github.com/EventStore/EventStore-Client-Dotnet/issues/243
        //stream = typeof(ESCloudEventStore).Assembly.GetManifestResourceStream(string.Join('.', typeof(EventStoreProjections).Namespace, "Assets", "projections", "by_correlation_id.json"))!;
        //streamReader = new StreamReader(stream);
        //query = await streamReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        //streamReader.Dispose();
        //await this.Projections.UpdateAsync(EventStoreProjections.BuiltInProjections.PartitionByCorrelationId, query, true, cancellationToken: cancellationToken);

        stream = typeof(ESCloudEventStore).Assembly.GetManifestResourceStream(string.Join('.', typeof(EventStoreProjections).Namespace, "Assets", "Projections", "cloud-events-by_source.js"))!;
        streamReader = new StreamReader(stream);
        query = await streamReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        streamReader.Dispose();
        await this.Projections.CreateContinuousAsync(EventStoreProjections.PartitionBySource, query, true, cancellationToken: cancellationToken).ConfigureAwait(false);

        stream = typeof(ESCloudEventStore).Assembly.GetManifestResourceStream(string.Join('.', typeof(EventStoreProjections).Namespace, "Assets", "Projections", "cloud-events-partition-ids.js.tmpl"))!;
        streamReader = new StreamReader(stream);
        query = await streamReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        streamReader.Dispose();
        foreach (var value in Enum.GetValues<CloudEventPartitionType>())
        {
            var typeName = value.ToString();
            var propertyName = typeName.Replace("By", "").ToLower();
            await this.Projections.CreateContinuousAsync(EventStoreProjections.CloudEventPartitionsMetadataPrefix + typeName, query.Replace("##propertyName##", propertyName), true, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Serializes the specified <see cref="CloudEvent"/> into a new <see cref="EventData"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to serialize</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The serialized <see cref="EventData"/></returns>
    protected virtual Task<EventData> SerializeAsync(CloudEvent e, CancellationToken cancellationToken)
    {
        if (e == null) throw new ArgumentNullException(nameof(e));
        var id = Uuid.NewUuid();
        var type = e.Type!;
        var metadataObject = (JsonObject)Serializer.Json.SerializeToNode(e)!;
        metadataObject.Remove("data", out var dataObject);
        if (!string.IsNullOrWhiteSpace(e.Subject)) metadataObject.Add("$correlationId", e.Subject);
        var data = Encoding.UTF8.GetBytes(Serializer.Json.Serialize(dataObject));
        var metadata = Encoding.UTF8.GetBytes(Serializer.Json.Serialize(metadataObject));
        return Task.FromResult(new EventData(id, type, data, metadata, MediaTypeNames.Application.Json));
    }

    /// <summary>
    /// Deserializes the specified <see cref="ResolvedEvent"/> into a new <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="ResolvedEvent"/> to convert</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="CloudEvent"/></returns>
    protected virtual Task<CloudEvent> DeserializeAsync(ResolvedEvent e, CancellationToken cancellationToken)
    {
        var dataObject = Serializer.Json.Deserialize<JsonObject>(e.Event.Data.Span);
        var eventObject = Serializer.Json.Deserialize<JsonObject>(e.Event.Metadata.Span) ?? throw new Exception($"The resolved event at position '{e.OriginalPosition!.Value}' in stream '{e.OriginalStreamId}' is in a invalid/unsupported cloud event image format");
        eventObject.Remove("$correlationId");
        if (dataObject != null) eventObject["data"] = dataObject;
        var rawEvent = Encoding.UTF8.GetBytes(Serializer.Json.Serialize(eventObject));
        using var stream = new MemoryStream(rawEvent);
        string? dataContentType = null;
        if (!eventObject.TryGetPropertyValue(CloudEventExtensionAttributes.Sequence, out _)) eventObject[CloudEventExtensionAttributes.Sequence] = e.OriginalEventNumber.ToInt64();
        if (eventObject.TryGetPropertyValue(CloudEventAttributes.DataContentType, out var dataContentTypeNode) && dataContentTypeNode != null) dataContentType = Serializer.Json.Deserialize<string>(dataContentTypeNode);
        if (string.IsNullOrWhiteSpace(dataContentType)) dataContentType = MediaTypeNames.Application.Json;
        return Task.FromResult(Serializer.Json.Deserialize<CloudEvent>(eventObject)!);
    }

}