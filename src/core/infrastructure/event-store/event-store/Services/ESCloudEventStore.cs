using System.Net.Mime;
using System.Runtime.CompilerServices;

namespace CloudStreams.Infrastructure.Services;

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
    /// <param name="cloudEventFormatter">The service used to format <see cref="CloudEvent"/>s</param>
    public ESCloudEventStore(ILoggerFactory loggerFactory, EventStoreClient streams, EventStoreProjectionManagementClient projections, CloudEventFormatter cloudEventFormatter)
    {
        this.Logger = loggerFactory.CreateLogger(this.GetType());
        this.Streams = streams;
        this.Projections = projections;
        this.CloudEventFormatter = cloudEventFormatter;
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

    /// <summary>
    /// Gets the service used to format <see cref="CloudEvent"/>s
    /// </summary>
    protected CloudEventFormatter CloudEventFormatter { get; }

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
    public virtual async IAsyncEnumerable<CloudEvent> ReadAsync(StreamReadDirection readDirection, long offset, ulong? amount = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var streamName = EventStoreStreams.All;
        var resolveLinkTos = true;
        var readResult = this.Streams.ReadStreamAsync(readDirection.ToDirection(), streamName, StreamPosition.FromInt64(offset), (long)(amount ?? long.MaxValue), resolveLinkTos, cancellationToken: cancellationToken);
        await foreach (var resolvedEvent in readResult)
        {
            yield return await this.DeserializeAsync(resolvedEvent, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<CloudEvent> ReadBySourceAsync(StreamReadDirection readDirection, Uri source, long offset, ulong? amount = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        var streamName = EventStoreStreams.ByCloudEventSource(source);
        var resolveLinkTos = true;
        var readResult = this.Streams.ReadStreamAsync(readDirection.ToDirection(), streamName, StreamPosition.FromInt64(offset), (long)(amount ?? long.MaxValue), resolveLinkTos, cancellationToken: cancellationToken);
        await foreach (var resolvedEvent in readResult)
        {
            yield return await this.DeserializeAsync(resolvedEvent, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<CloudEvent> ReadByTypeAsync(StreamReadDirection readDirection, string type, long offset, ulong? amount = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(type)) throw new ArgumentNullException(nameof(type));
        var streamName = EventStoreStreams.ByCloudEventType(type);
        var resolveLinkTos = true;
        var readResult = this.Streams.ReadStreamAsync(readDirection.ToDirection(), streamName, StreamPosition.FromInt64(offset), (long)(amount ?? long.MaxValue), resolveLinkTos, cancellationToken: cancellationToken);
        await foreach (var resolvedEvent in readResult)
        {
            yield return await this.DeserializeAsync(resolvedEvent, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<CloudEvent> ReadByCorrelationIdAsync(StreamReadDirection readDirection, string correlationId, long offset, ulong? amount = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentNullException(nameof(correlationId));
        var streamName = EventStoreStreams.ByCorrelationId(correlationId);
        var resolveLinkTos = true;
        var readResult = this.Streams.ReadStreamAsync(readDirection.ToDirection(), streamName, StreamPosition.FromInt64(offset), (long)(amount ?? long.MaxValue), resolveLinkTos, cancellationToken: cancellationToken);
        await foreach (var resolvedEvent in readResult)
        {
            yield return await this.DeserializeAsync(resolvedEvent, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public virtual Task<string> SubscribeAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(); //todo
    }

    /// <inheritdoc/>
    public virtual async Task TruncateAsync(long beforeVersion, CancellationToken cancellationToken = default)
    {
        var streamName = EventStoreStreams.All;
        await this.Streams.SetStreamMetadataAsync(streamName, StreamState.Any, new StreamMetadata(truncateBefore: StreamPosition.FromInt64(beforeVersion)), cancellationToken: cancellationToken);
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
            await this.Streams.ReadStreamAsync(Direction.Forwards, EventStoreProjections.ByCloudEventSource, StreamPosition.Start, cancellationToken: cancellationToken).ToListAsync();
        }
        catch(StreamNotFoundException)
        {
            try
            {
                using var stream = typeof(ESCloudEventStore).Assembly.GetManifestResourceStream(string.Join('.', typeof(EventStoreProjections).Namespace, "Assets", "projections", "cse_by_source.js"))!;
                using var streamReader = new StreamReader(stream);
                var query = await streamReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                await this.Projections.CreateContinuousAsync(EventStoreProjections.ByCloudEventSource, query, true, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch { }
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
        var encoded = this.CloudEventFormatter.EncodeStructuredModeMessage(e, out _);
        var metadataObject = Serializer.Json.Deserialize<JsonObject>(encoded.Span)!;
        metadataObject.Remove("data", out var dataObject);
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
        var eventObject = Serializer.Json.Deserialize<JsonObject>(e.Event.Metadata.Span);
        if (eventObject == null) throw new Exception($"The resolved event at position '{e.OriginalPosition!.Value}' in stream '{e.OriginalStreamId}' is in a invalid/unsupported cloud event image format");
        if (dataObject != null) eventObject["data"] = dataObject;
        var rawEvent = Encoding.UTF8.GetBytes(Serializer.Json.Serialize(eventObject));
        using var stream = new MemoryStream(rawEvent);
        string? dataContentType = null;
        if (eventObject.TryGetPropertyValue("datacontenttype", out var dataContentTypeNode) && dataContentTypeNode != null) dataContentType = Serializer.Json.Deserialize<string>(dataContentTypeNode);
        if (string.IsNullOrWhiteSpace(dataContentType)) dataContentType = MediaTypeNames.Application.Json;
        var contentType = new ContentType(dataContentType);
        var extensionAttributes = Array.Empty<CloudEventAttribute>();
        return Task.FromResult(this.CloudEventFormatter.DecodeStructuredModeMessage(stream, contentType, extensionAttributes.AsEnumerable()));
    }

}