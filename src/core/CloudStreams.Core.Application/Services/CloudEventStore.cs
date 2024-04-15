// Copyright © 2024-Present The Cloud Streams Authors
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

using Microsoft.Extensions.Hosting;
using Neuroglia.Data.Infrastructure.EventSourcing;
using Neuroglia.Data.Infrastructure.EventSourcing.Services;
using Neuroglia.Data.Infrastructure.ResourceOriented.Properties;
using Neuroglia.Serialization;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Represents the default implementation of the <see cref="ICloudEventStore"/> interface
/// </summary>
/// <param name="eventStore">The underlying service used to source events</param>
/// <param name="projectionManager">The service used to manage event-driven projections</param>
/// <param name="serializer">The service used to serialize/deserialize data to/from JSON</param>
public class CloudEventStore(IEventStore eventStore, IProjectionManager projectionManager, IJsonSerializer serializer)
    : IHostedService, ICloudEventStore
{

    /// <summary>
    /// Gets the underlying service used to source events
    /// </summary>
    protected IEventStore EventStore => eventStore;

    /// <summary>
    /// Gets the service used to manage event-driven projections
    /// </summary>
    protected IProjectionManager ProjectionManager => projectionManager;

    /// <summary>
    /// Gets the service used to serialize/deserialize data to/from JSON
    /// </summary>
    protected IJsonSerializer Serializer { get; } = serializer;

    /// <inheritdoc/>
    public virtual Task StartAsync(CancellationToken cancellationToken) => this.SetupProjectionsAsync(cancellationToken);

    /// <inheritdoc/>
    public virtual Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual async Task<CloudEventRecord> AppendAsync(CloudEventDescriptor e, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        var streamName = Streams.All;
        var descriptor = await this.ParseEventDescriptorAsync(e, cancellationToken).ConfigureAwait(false);
        var offset = await this.EventStore.AppendAsync(streamName, [descriptor], cancellationToken: cancellationToken).ConfigureAwait(false);
        return new(streamName, offset, e.Metadata, e.Data);
    }

    /// <inheritdoc/>
    public virtual async Task<StreamMetadata> GetStreamMetadataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var firstEvent = (await this.EventStore.ReadAsync(Streams.All, StreamReadDirection.Forwards, StreamPosition.StartOfStream, 1, cancellationToken: cancellationToken).ToListAsync(cancellationToken)).Single();
            var lastEvent = (await this.EventStore.ReadAsync(Streams.All, StreamReadDirection.Backwards, StreamPosition.EndOfStream, 1, cancellationToken: cancellationToken).ToListAsync(cancellationToken)).Single();
            return new()
            {
                FirstEvent = firstEvent.Timestamp,
                LastEvent = lastEvent.Timestamp,
                Length = lastEvent.Offset + 1
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
        try
        {
            var streamName = partition.GetStreamName();
            var firstEvent = (await this.EventStore.ReadAsync(streamName, StreamReadDirection.Forwards, StreamPosition.StartOfStream, 1, cancellationToken: cancellationToken).ToListAsync(cancellationToken)).Single();
            var lastEvent = (await this.EventStore.ReadAsync(streamName, StreamReadDirection.Backwards, StreamPosition.EndOfStream, 1, cancellationToken: cancellationToken).ToListAsync(cancellationToken)).Single();
            return new()
            {
                Id = partition.Id,
                Type = partition.Type,
                FirstEvent = firstEvent.Timestamp,
                LastEvent = lastEvent.Timestamp,
                Length = lastEvent.Offset + 1
            };
        }
        catch (StreamNotFoundException)
        {
            throw new ProblemDetailsException(new ProblemDetails(ProblemTypes.NotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, $"Failed to find the stream '{partition.GetStreamName()}' of the partition of type '{EnumHelper.Stringify(partition.Type)}' with id '{partition.Id}'. Related projection does not exist or has not been properly configured"));
        }
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<string> ListPartitionIdsAsync(CloudEventPartitionType partitionType, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var typeName = EnumHelper.Stringify(partitionType).Replace('-', '_');
        List<string>? partitionsIds = null;
        try
        {
            partitionsIds = await this.ProjectionManager.GetStateAsync<List<string>>(Projections.CloudEventPartitionsMetadataPrefix + typeName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch { }
        if (partitionsIds == null) yield break;
        await foreach (var id in partitionsIds.ToAsyncEnumerable()) yield return id;
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<CloudEventRecord> ReadAsync(StreamReadDirection readDirection, long offset, ulong? length = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var streamName = Streams.All;
        IAsyncEnumerable<IEventRecord> records;
        try { records = this.EventStore.ReadAsync(streamName, readDirection, offset, length ?? ulong.MaxValue, cancellationToken); }
        catch (StreamNotFoundException) { yield break; }
        await foreach (var record in records) yield return this.ReadRecord(record);
    }

    /// <inheritdoc/>
    public virtual IAsyncEnumerable<CloudEventRecord> ReadPartitionAsync(PartitionReference partition, StreamReadDirection readDirection, long offset, ulong? length = null, CancellationToken cancellationToken = default)
    {
        switch (partition.Type)
        {
            case CloudEventPartitionType.BySource:
                if (!Uri.TryCreate(partition.Id, UriKind.RelativeOrAbsolute, out var source)) throw new Exception();
                return this.ReadBySourceAsync(readDirection, source, offset, length, cancellationToken);
            case CloudEventPartitionType.BySubject:
                return this.ReadBySubjectAsync(readDirection, partition.Id, offset, length, cancellationToken);
            case CloudEventPartitionType.ByType:
                return this.ReadByTypeAsync(readDirection, partition.Id, offset, length, cancellationToken);
            case CloudEventPartitionType.ByCorrelationId:
                return this.ReadByCorrelationIdAsync(readDirection, partition.Id, offset, length, cancellationToken);
            case CloudEventPartitionType.ByCausationId:
                return this.ReadByCausationIdAsync(readDirection, partition.Id, offset, length, cancellationToken);
            default:
                throw new NotSupportedException($"The specified {nameof(CloudEventPartitionType)} '{partition.Type}' is not supported");
        }
    }

    /// <inheritdoc/>
    public virtual async Task<IObservable<CloudEventRecord>> ObserveAsync(long offset = -1, CancellationToken cancellationToken = default)
    {
        var subscription = await this.EventStore.ObserveAsync(
            Streams.All,
            offset,
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return subscription.Select(this.ReadRecord);
    }

    /// <inheritdoc/>
    public virtual async Task<IObservable<CloudEventRecord>> ObservePartitionAsync(PartitionReference partition, long offset = -1, CancellationToken cancellationToken = default)
    {
        var subscription = await this.EventStore.ObserveAsync(
           partition.GetStreamName(),
           offset,
           cancellationToken: cancellationToken)
           .ConfigureAwait(false);
        return subscription.Select(this.ReadRecord);
    }

    /// <inheritdoc/>
    public virtual Task TruncateAsync(ulong beforeVersion, CancellationToken cancellationToken = default) => this.EventStore.TruncateAsync(Streams.All, beforeVersion, cancellationToken);

    /// <summary>
    /// Creates and configures the projections required by Cloud Streams
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task SetupProjectionsAsync(CancellationToken cancellationToken = default)
    {
        try { if (await this.ProjectionManager.GetStatusAsync(Projections.PartitionBySource, cancellationToken: cancellationToken) != null) return; }
        catch { }

        try
        {
            await this.ProjectionManager.CreateAsync<object>(Projections.PartitionBySource,
                projection => projection
                    .FromStream(Streams.All)
                    .When((state, e) => e.Metadata!.Get("source") != null)
                    .Then((state, e) => Projection.LinkEventTo("$by-source-" + e.Metadata!.Get("source"), e)),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch { }

        try
        {
            await this.ProjectionManager.CreateAsync<object>(Projections.PartitionBySubject,
            projection => projection
                .FromStream(Streams.All)
                .When((state, e) => e.Metadata!.Get("subject") != null)
                .Then((state, e) => Projection.LinkEventTo("$by-subject-" + e.Metadata!.Get("subject"), e)),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch { }

        try
        {
            await this.ProjectionManager.CreateAsync<object>(Projections.PartitionByCausationId,
           projection => projection
                .FromStream(Streams.All)
                .When((state, e) => e.Metadata!.Get("$causationId") != null)
                .Then((state, e) => Projection.LinkEventTo("$by-causation-" + e.Metadata!.Get("$causationId"), e)),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch { }

        foreach (var partitionType in Enum.GetValues<CloudEventPartitionType>())
        {
            var typeName = EnumHelper.Stringify(partitionType).Replace('-', '_');
            var metadataPath = typeName.Replace("by", string.Empty).ToCamelCase();
            if (partitionType == CloudEventPartitionType.ByCorrelationId || partitionType == CloudEventPartitionType.ByCausationId) metadataPath = "$" + metadataPath.Replace("-id", "Id");
            try
            {
                await this.ProjectionManager.CreateAsync<List<object>>(Projections.CloudEventPartitionsMetadataPrefix + typeName,
                     projection => projection
                        .FromStream(Streams.All)
                        .Given(() => new List<object>())
                        .When((state, e) => e.Metadata!.Get(metadataPath) != null && !state.Contains(e.Metadata!.Get(metadataPath)))
                        .Then((state, e) => state.Add(e.Metadata!.Get(metadataPath))),
                     cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch { }
        }
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
        ArgumentNullException.ThrowIfNull(source);
        var streamName = Streams.ByCloudEventSource(source);
        IAsyncEnumerable<IEventRecord> records;
        try { records = this.EventStore.ReadAsync(streamName, readDirection, offset, length ?? ulong.MaxValue, cancellationToken); }
        catch (StreamNotFoundException)
        {
            throw new ProblemDetailsException(new ProblemDetails(ProblemTypes.NotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, $"Failed to find the stream that belongs to the partition of type '{EnumHelper.Stringify(CloudEventPartitionType.BySource)}'. Related projection does not exist or has not been properly configured"));
        }
        await foreach (var record in records) { yield return this.ReadRecord(record); }
    }

    /// <summary>
    /// Reads stored <see cref="CloudEvent"/>s by subject
    /// </summary>
    /// <param name="readDirection">The direction in which to read</param>
    /// <param name="subject">The subject of the <see cref="CloudEvent"/>s to read</param>
    /// <param name="offset">The offset starting from which to read events</param>
    /// <param name="length">The amount of <see cref="CloudEvent"/>s to read</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing the <see cref="CloudEvent"/>s read from the store</returns>
    /// <inheritdoc/>
    protected virtual async IAsyncEnumerable<CloudEventRecord> ReadBySubjectAsync(StreamReadDirection readDirection, string subject, long offset, ulong? length = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentNullException(nameof(subject));
        var streamName = Streams.ByCloudEventSubject(subject);
        IAsyncEnumerable<IEventRecord> records;
        try { records = this.EventStore.ReadAsync(streamName, readDirection, offset, length ?? ulong.MaxValue, cancellationToken); }
        catch (StreamNotFoundException)
        {
            throw new ProblemDetailsException(new ProblemDetails(ProblemTypes.NotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, $"Failed to find the stream that belongs to the partition of type '{EnumHelper.Stringify(CloudEventPartitionType.BySubject)}'. Related projection does not exist or has not been properly configured"));
        }
        await foreach (var record in records) yield return this.ReadRecord(record);
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
        var streamName = Streams.ByCloudEventType(type);
        IAsyncEnumerable<IEventRecord> records;
        try { records = this.EventStore.ReadAsync(streamName, readDirection, offset, length ?? ulong.MaxValue, cancellationToken: cancellationToken); }
        catch (StreamNotFoundException)
        {
            throw new ProblemDetailsException(new ProblemDetails(ProblemTypes.NotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, $"Failed to find the stream that belongs to the partition of type '{EnumHelper.Stringify(CloudEventPartitionType.ByType)}'. Related projection does not exist or has not been properly configured"));
        }
        await foreach (var record in records)
        {
            yield return this.ReadRecord(record);
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
        var streamName = Streams.ByCorrelationId(correlationId);
        IAsyncEnumerable<IEventRecord> records;
        try { records = this.EventStore.ReadAsync(streamName, readDirection, offset, length ?? ulong.MaxValue, cancellationToken: cancellationToken); }
        catch (StreamNotFoundException)
        {
            throw new ProblemDetailsException(new ProblemDetails(ProblemTypes.NotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, $"Failed to find the stream that belongs to the partition of type '{EnumHelper.Stringify(CloudEventPartitionType.ByCorrelationId)}'. Related projection does not exist or has not been properly configured"));
        }
        await foreach (var record in records) yield return this.ReadRecord(record);
    }

    /// <summary>
    /// Reads stored <see cref="CloudEvent"/>s by causation id
    /// </summary>
    /// <param name="readDirection">The direction in which to read</param>
    /// <param name="causationId">The causation id of the <see cref="CloudEvent"/>s to read</param>
    /// <param name="offset">The offset starting from which to read events</param>
    /// <param name="length">The amount of <see cref="CloudEvent"/>s to read</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing the <see cref="CloudEvent"/>s read from the store</returns>
    protected virtual async IAsyncEnumerable<CloudEventRecord> ReadByCausationIdAsync(StreamReadDirection readDirection, string causationId, long offset, ulong? length = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(causationId)) throw new ArgumentNullException(nameof(causationId));
        var streamName = Streams.ByCausationId(causationId);
        IAsyncEnumerable<IEventRecord> records;
        try { records = this.EventStore.ReadAsync(streamName, readDirection, offset, length ?? ulong.MaxValue, cancellationToken: cancellationToken); }
        catch (StreamNotFoundException)
        {
            throw new ProblemDetailsException(new ProblemDetails(ProblemTypes.NotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, $"Failed to find the stream that belongs to the partition of type '{EnumHelper.Stringify(CloudEventPartitionType.ByCausationId)}'. Related projection does not exist or has not been properly configured"));
        }
        await foreach (var record in records) yield return this.ReadRecord(record);
    }

    /// <summary>
    /// Parses the specified <see cref="CloudEvent"/> into a new <see cref="IEventDescriptor"/>
    /// </summary>
    /// <param name="eventDescriptor">An object that describes the <see cref="CloudEvent"/> to serialize</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The serialized <see cref="IEventDescriptor"/></returns>
    protected virtual async Task<IEventDescriptor> ParseEventDescriptorAsync(CloudEventDescriptor eventDescriptor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventDescriptor);
        var e = await this.ParseCloudEventAsync(eventDescriptor, cancellationToken).ConfigureAwait(false);
        var type = e.Type!;
        var contextAttributes = this.Serializer.Deserialize<IDictionary<string, object>>(this.Serializer.SerializeToByteArray(eventDescriptor))!;
        contextAttributes.Remove(CloudEventAttributes.Data);
        return new EventDescriptor(type, eventDescriptor.Data, eventDescriptor.Metadata.ContextAttributes);
    }

    /// <summary>
    /// Parses the specified <see cref="CloudEvent"/> into a new <see cref="IEventDescriptor"/>
    /// </summary>
    /// <param name="eventDescriptor">An object that describes the <see cref="CloudEvent"/> to serialize</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The serialized <see cref="IEventDescriptor"/></returns>
    protected virtual Task<CloudEvent> ParseCloudEventAsync(CloudEventDescriptor eventDescriptor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventDescriptor);
        var e = (JsonObject)this.Serializer.SerializeToNode(eventDescriptor.Metadata.ContextAttributes)!;
        var data = this.Serializer.SerializeToNode(eventDescriptor.Data);
        e[CloudEventAttributes.Data] = data;
        return Task.FromResult(this.Serializer.Deserialize<CloudEvent>(this.Serializer.SerializeToByteArray(e))!);
    }

    /// <summary>
    /// Deserializes the specified <see cref="IEventRecord"/> into a new <see cref="CloudEventRecord"/>
    /// </summary>
    /// <param name="e">The <see cref="IEventRecord"/> to convert</param>
    /// <returns>A new <see cref="CloudEventRecord"/></returns>
    protected virtual CloudEventRecord ReadRecord(IEventRecord e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return new(e.StreamId, e.Offset, new() { ContextAttributes = e.Metadata ?? new Dictionary<string, object>() }, e.Data);
    }

}
