// Copyright © 2023-Present The Cloud Streams Authors
//
// Licensed under the Apache License, Version 2.0 (the "License"),
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
    public virtual async Task<CloudEventRecord> AppendAsync(CloudEvent e, IDictionary<string, string>? metadata, CancellationToken cancellationToken = default)
    {
        if (metadata == null) throw new ArgumentNullException(nameof(metadata));
        data ??= new { };
        var streamName = EventStoreStreams.All;
        var eventData = await this.SerializeToEventDataAsync(metadata, data, cancellationToken).ConfigureAwait(false);
        var writeResult = await this.Streams.AppendToStreamAsync(streamName, StreamState.Any, new EventData[] { eventData }, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new(streamName, writeResult.NextExpectedStreamRevision.ToUInt64(), metadata, data);
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
    public virtual async IAsyncEnumerable<CloudEventRecord> ReadAsync(StreamReadDirection readDirection, long offset, ulong? length = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var streamName = EventStoreStreams.All;
        var resolveLinkTos = true;
        var position = offset == StreamPosition.EndOfStream ? EventStore.Client.StreamPosition.End : EventStore.Client.StreamPosition.FromInt64(offset);
        var readResult = this.Streams.ReadStreamAsync(readDirection.ToDirection(), streamName, position, (long)(length ?? long.MaxValue), resolveLinkTos, cancellationToken: cancellationToken);
        await foreach (var resolvedEvent in readResult)
        {
            yield return await this.DeserializeResolvedEventAsync(resolvedEvent, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public virtual IAsyncEnumerable<CloudEventRecord> ReadPartitionAsync(PartitionReference partition, StreamReadDirection readDirection, long offset, ulong? length = null, CancellationToken cancellationToken = default)
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
    public virtual async Task<IObservable<CloudEventRecord>> SubscribeAsync(long offset = StreamPosition.EndOfStream, CancellationToken cancellationToken = default)
    {
        var subject = new Subject<CloudEventRecord>();
        var subscription = await this.Streams.SubscribeToStreamAsync(
            EventStoreStreams.All, 
            offset.ToSubscriptionPosition(), 
            async (sub, e, cancellation) => subject.OnNext(await this.DeserializeResolvedEventAsync(e, cancellation).ConfigureAwait(false)), 
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Observable.Using
        (
            () => subscription,
            watch => subject
        );
    }

    /// <inheritdoc/>
    public virtual async Task<IObservable<CloudEventRecord>> SubscribeToPartitionAsync(PartitionReference partition, long offset = StreamPosition.EndOfStream, CancellationToken cancellationToken = default)
    {
        var subject = new Subject<CloudEventRecord>();
        var subscription = await this.Streams.SubscribeToStreamAsync(
            partition.GetStreamName(),
            offset.ToSubscriptionPosition(), 
            async (sub, e, cancellation) => subject.OnNext(await this.DeserializeResolvedEventAsync(e, cancellation).ConfigureAwait(false)), 
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
    protected virtual async IAsyncEnumerable<CloudEventRecord> ReadBySourceAsync(StreamReadDirection readDirection, Uri source, long offset, ulong? length = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        var streamName = EventStoreStreams.ByCloudEventSource(source);
        var resolveLinkTos = true;
        var position = offset == StreamPosition.EndOfStream ? EventStore.Client.StreamPosition.End : EventStore.Client.StreamPosition.FromInt64(offset);
        var readResult = this.Streams.ReadStreamAsync(readDirection.ToDirection(), streamName, position, (long)(length ?? long.MaxValue), resolveLinkTos, cancellationToken: cancellationToken);
        await foreach (var resolvedEvent in readResult)
        {
            yield return await this.DeserializeResolvedEventAsync(resolvedEvent, cancellationToken);
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
    protected virtual async IAsyncEnumerable<CloudEventRecord> ReadByTypeAsync(StreamReadDirection readDirection, string type, long offset, ulong? length = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(type)) throw new ArgumentNullException(nameof(type));
        var streamName = EventStoreStreams.ByCloudEventType(type);
        var resolveLinkTos = true;
        var position = offset == StreamPosition.EndOfStream ? EventStore.Client.StreamPosition.End : EventStore.Client.StreamPosition.FromInt64(offset);
        var readResult = this.Streams.ReadStreamAsync(readDirection.ToDirection(), streamName, position, (long)(length ?? long.MaxValue), resolveLinkTos, cancellationToken: cancellationToken);
        await foreach (var resolvedEvent in readResult)
        {
            yield return await this.DeserializeResolvedEventAsync(resolvedEvent, cancellationToken);
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
    protected virtual async IAsyncEnumerable<CloudEventRecord> ReadByCorrelationIdAsync(StreamReadDirection readDirection, string correlationId, long offset, ulong? length = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentNullException(nameof(correlationId));
        var streamName = EventStoreStreams.ByCorrelationId(correlationId);
        var resolveLinkTos = true;
        var position = offset == StreamPosition.EndOfStream ? EventStore.Client.StreamPosition.End : EventStore.Client.StreamPosition.FromInt64(offset);
        var readResult = this.Streams.ReadStreamAsync(readDirection.ToDirection(), streamName, position, (long)(length ?? long.MaxValue), resolveLinkTos, cancellationToken: cancellationToken);
        await foreach (var resolvedEvent in readResult)
        {
            yield return await this.DeserializeResolvedEventAsync(resolvedEvent, cancellationToken);
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

        stream = typeof(ESCloudEventStore).Assembly.GetManifestResourceStream(string.Join('.', typeof(EventStoreProjections).Namespace, "Assets", "Projections", "bysource.js.tmpl"))!;
        streamReader = new StreamReader(stream);
        query = await streamReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        streamReader.Dispose();
        await this.Projections.CreateContinuousAsync(EventStoreProjections.PartitionBySource, query, true, cancellationToken: cancellationToken).ConfigureAwait(false);

        stream = typeof(ESCloudEventStore).Assembly.GetManifestResourceStream(string.Join('.', typeof(EventStoreProjections).Namespace, "Assets", "Projections", "partitionids.js.tmpl"))!;
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
    /// <param name="metadata">An <see cref="IDictionary{TKey, TValue}"/> containing additional information about the <see cref="CloudEvent"/></param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The serialized <see cref="EventData"/></returns>
    protected virtual Task<EventData> SerializeToEventDataAsync(CloudEvent e, IDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        if (e == null) throw new ArgumentNullException(nameof(e));
        var id = Uuid.NewUuid();
        var type = e.Type!;
        var contextAttributes = Serializer.Json.Deserialize<IDictionary<string, object>>(Serializer.Json.Serialize(e))!;
        contextAttributes.Remove(CloudEventAttributes.Data);
        var dataContentType = string.IsNullOrWhiteSpace(e.DataContentType) ? MediaTypeNames.Application.Json : e.DataContentType;
        var dataRaw = dataContentType switch
        {
            MediaTypeNames.Application.Json => Encoding.UTF8.GetBytes(Serializer.Json.Serialize(e.Data)),
            "application/x-yaml" or "text/yaml" or "text/x-yaml" => Encoding.UTF8.GetBytes(Serializer.Yaml.Serialize(e.Data)),
            MediaTypeNames.Application.Octet => e.Data is byte[] byteArray ? byteArray : e.Data is string base64String ? Convert.FromBase64String(base64String)
                : throw new NotSupportedException($"The cloud event payload must be an array of bytes or a base 64 encoded string when data content type has been set to '{MediaTypeNames.Application.Octet}'"),
            _ => throw new NotSupportedException($"The specified cloud event data content type '{dataContentType}' is not supported")
        };
        var esMetadata = new ESCloudEventMetadata(contextAttributes) { ExtensionData = metadata };
        var metadataRaw = Encoding.UTF8.GetBytes(Serializer.Json.Serialize(esMetadata));
        return Task.FromResult(new EventData(id, type, dataRaw, metadataRaw, dataContentType));
    }

    /// <summary>
    /// Deserializes the specified <see cref="ResolvedEvent"/> into a new <see cref="CloudEventRecord"/>
    /// </summary>
    /// <param name="e">The <see cref="ResolvedEvent"/> to convert</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="CloudEventRecord"/></returns>
    protected virtual Task<CloudEventRecord> DeserializeResolvedEventAsync(ResolvedEvent e, CancellationToken cancellationToken)
    {
        var data = e.Event.ContentType switch
        {
            MediaTypeNames.Application.Json => Serializer.Json.Deserialize<object>(e.Event.Data.Span),
            "application/x-yaml" or "text/yaml" or "text/x-yaml" => Serializer.Yaml.Deserialize<object>(e.Event.Data.Span),
            MediaTypeNames.Application.Octet => e.Event.Data,
            _ => throw new NotSupportedException($"The specified cloud event data content type '{dataContentType}' is not supported")
        }; //depends on resolved event's content type
        var metadata = Serializer.Json.Deserialize<ESCloudEventMetadata>(e.Event.Data.Span)!;
        var cloudEvent = (JsonObject)Serializer.Json.SerializeToNode(metadata.ContextAttributes)!;
        cloudEvent[CloudEventAttributes.Data] = dataObject;

        var dataObject = Serializer.Json.Deserialize<JsonObject>(e.Event.Data.Span);
        var eventObject = Serializer.Json.Deserialize<JsonObject>(e.Event.Metadata.Span) ?? throw new Exception($"The resolved event at position '{e.OriginalPosition!.Value}' in stream '{e.OriginalStreamId}' is in a invalid/unsupported cloud event image format");
        eventObject.Remove("$correlationId");
        if (dataObject != null) eventObject["data"] = dataObject;
        string? dataContentType = null;
        if (!eventObject.TryGetPropertyValue(CloudEventExtensionAttributes.Sequence, out _)) eventObject[CloudEventExtensionAttributes.Sequence] = e.OriginalEventNumber.ToInt64();
        if (eventObject.TryGetPropertyValue(CloudEventAttributes.DataContentType, out var dataContentTypeNode) && dataContentTypeNode != null) dataContentType = Serializer.Json.Deserialize<string>(dataContentTypeNode);
        if (string.IsNullOrWhiteSpace(dataContentType)) dataContentType = MediaTypeNames.Application.Json;
        return Task.FromResult(Serializer.Json.Deserialize<CloudEvent>(eventObject)!);
    }

}

public class ESCloudEventMetadata
    : IExtensible
{

    public virtual IDictionary<string, object> ContextAttributes { get; set; }

    public virtual IDictionary<string, object>? ExtensionData { get; set; }

}