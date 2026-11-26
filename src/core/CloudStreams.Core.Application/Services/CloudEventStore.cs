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

using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
/// <param name="logger">The service used to perform logging</param>
/// <param name="partitionIndex">The index of the partitions cloud events have been published to, if any</param>
/// <param name="retentionStore">The service used to get and set the retention policy of the cloud event stream, if any</param>
public class CloudEventStore(IEventStore eventStore, IProjectionManager projectionManager, IJsonSerializer serializer, ILogger<CloudEventStore>? logger = null, IPartitionIndex? partitionIndex = null,
    IStreamRetentionStore? retentionStore = null)
    : IHostedService, ICloudEventStore, IDisposable
{

    /// <summary>
    /// Gets the maximum amount of times a read of a partition is resumed after its oldest links expired while it was being read
    /// </summary>
    const int MaxExpiredLinkRecoveries = 3;

    readonly CancellationTokenSource _cancellationTokenSource = new();
    /// <summary>
    /// The offset of the first link of each partition stream known to be readable: the links before it point to events that have expired (see #206).
    /// It only ever grows, since events expire from the oldest
    /// </summary>
    readonly System.Collections.Concurrent.ConcurrentDictionary<string, ulong> _firstRetainedLinks = new(StringComparer.Ordinal);
    Task? _projectionSetupTask;
    volatile IReadOnlyCollection<string>? _missingProjections;

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; } = (ILogger?)logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    /// <summary>
    /// Gets the names of the projections that are missing, as of the last attempt at setting them up, or null if no attempt has completed yet
    /// </summary>
    public IReadOnlyCollection<string>? MissingProjections => this._missingProjections;

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
    /// <remarks>Projections are set up in the background, and retried until they all exist, so that an unavailable event store does not prevent the host from starting (see #186)</remarks>
    public virtual Task StartAsync(CancellationToken cancellationToken)
    {
        this._projectionSetupTask = Task.Run(() => this.EnsureProjectionsAsync(this._cancellationTokenSource.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual async Task StopAsync(CancellationToken cancellationToken)
    {
        this._cancellationTokenSource.Cancel();
        if (this._projectionSetupTask != null) await Task.WhenAny(this._projectionSetupTask, Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The store is registered both as itself and, through factories, as the services it implements: the container disposes of it once per registration, so
    /// disposing of it more than once must not throw, or every graceful shutdown would end with an unhandled exception
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this._disposed, 1) == 1) return;
        this._cancellationTokenSource.Cancel();
        this._cancellationTokenSource.Dispose();
        GC.SuppressFinalize(this);
    }

    int _disposed;

    /// <inheritdoc/>
    public virtual async Task<CloudEventRecord> AppendAsync(CloudEventDescriptor e, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        var streamName = StreamNameHelper.All;
        // the namespace is recorded with every cloud event, which the projections partition by (see #215)
        e.Metadata.ContextAttributes[CloudEventMetadataAttributes.Namespace] = GetNamespace(e.Metadata);
        var descriptor = await this.ParseEventDescriptorAsync(e, cancellationToken).ConfigureAwait(false);
        var offset = await this.EventStore.AppendAsync(streamName, [descriptor], cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.IndexPartitionsAsync(e.Metadata, cancellationToken).ConfigureAwait(false);
        return new(streamName, offset, e.Metadata, e.Data);
    }

    /// <summary>
    /// Gets the namespace the cloud event described by the specified metadata belongs to
    /// </summary>
    /// <param name="metadata">The metadata of the cloud event</param>
    /// <returns>The namespace the cloud event belongs to, which is the default namespace if its metadata names none</returns>
    public static string GetNamespace(CloudEventMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return metadata.ContextAttributes != null && metadata.ContextAttributes.TryGetValue(CloudEventMetadataAttributes.Namespace, out var value) && value?.ToString() is { Length: > 0 } @namespace
            ? @namespace
            : Namespace.DefaultNamespaceName;
    }

    /// <summary>
    /// Gets the partitions the specified cloud event belongs to
    /// </summary>
    /// <param name="metadata">The metadata of the cloud event to get the partitions of</param>
    /// <returns>The partitions the specified cloud event belongs to</returns>
    public static IEnumerable<PartitionReference> GetPartitions(CloudEventMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var attributes = new (CloudEventPartitionType Type, string Attribute)[]
        {
            (CloudEventPartitionType.BySource, CloudEventAttributes.Source),
            (CloudEventPartitionType.ByType, CloudEventAttributes.Type),
            (CloudEventPartitionType.BySubject, CloudEventAttributes.Subject),
            (CloudEventPartitionType.ByCorrelationId, "$correlationId"),
            (CloudEventPartitionType.ByCausationId, "$causationId")
        };
        foreach (var (type, attribute) in attributes)
        {
            if (!metadata.ContextAttributes.TryGetValue(attribute, out var value) || value == null) continue;
            var id = value is Uri uri ? uri.OriginalString : value.ToString();
            if (!string.IsNullOrEmpty(id)) yield return new(type, id);
        }
    }

    /// <summary>
    /// Adds the partitions the cloud event described by the specified metadata belongs to to the partition index, if any
    /// </summary>
    /// <param name="metadata">The metadata of the cloud event to index the partitions of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>The event is stored by then: failing to index its partitions only affects listing them, so it is logged rather than failing the ingestion</remarks>
    protected virtual async Task IndexPartitionsAsync(CloudEventMetadata metadata, CancellationToken cancellationToken)
    {
        if (partitionIndex == null) return;
        try
        {
            await partitionIndex.AddAsync(GetNamespace(metadata), GetPartitions(metadata), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.Logger.LogError(ex, "Failed to index the partitions of a cloud event: they may be missing from partition listings");
        }
    }

    /// <inheritdoc/>
    public virtual async Task<StreamMetadata> GetStreamMetadataAsync(string? @namespace, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(@namespace))
            {
                // the stream of a namespace links to the events of the cloud event stream, some of which may have expired (see #206)
                var streamName = StreamNameHelper.ByNamespace(@namespace);
                var firstLink = await this.ReadStreamAsync(streamName, StreamReadDirection.Forwards, StreamPosition.StartOfStream, 1, null, cancellationToken).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
                var lastLink = await this.ReadStreamAsync(streamName, StreamReadDirection.Backwards, StreamPosition.EndOfStream, 1, null, cancellationToken).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
                var namespaceRetention = await this.GetRetentionAsync(cancellationToken).ConfigureAwait(false);
                if (firstLink == null || lastLink == null) return new() { Retention = namespaceRetention };
                return new()
                {
                    FirstEvent = firstLink.Timestamp,
                    LastEvent = lastLink.Timestamp,
                    Length = lastLink.Sequence + 1,
                    FirstOffset = firstLink.Sequence,
                    Retention = namespaceRetention
                };
            }
            var firstEvent = (await this.EventStore.ReadAsync(StreamNameHelper.All, StreamReadDirection.Forwards, StreamPosition.StartOfStream, 1, cancellationToken: cancellationToken).ToListAsync(cancellationToken)).SingleOrDefault();
            var lastEvent = (await this.EventStore.ReadAsync(StreamNameHelper.All, StreamReadDirection.Backwards, StreamPosition.EndOfStream, 1, cancellationToken: cancellationToken).ToListAsync(cancellationToken)).SingleOrDefault();
            // a stream that exists but holds no event, e.g. after it has been truncated, is described like a stream that does not exist yet
            var retention = await this.GetRetentionAsync(cancellationToken).ConfigureAwait(false);
            if (firstEvent == null || lastEvent == null) return new() { Retention = retention };
            return new()
            {
                FirstEvent = firstEvent.Timestamp,
                LastEvent = lastEvent.Timestamp,
                Length = lastEvent.Offset + 1,
                FirstOffset = firstEvent.Offset,
                Retention = retention
            };
        }
        catch (StreamNotFoundException)
        {
            return new() { Retention = await this.GetRetentionAsync(cancellationToken).ConfigureAwait(false) };
        }
    }

    /// <summary>
    /// Gets the retention policy of the cloud event stream, if any
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The retention policy of the cloud event stream, or null if it retains every event or if the policy could not be read</returns>
    protected virtual async Task<StreamRetention?> GetRetentionAsync(CancellationToken cancellationToken)
    {
        if (retentionStore == null) return null;
        try { return await retentionStore.GetAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.Logger.LogWarning(ex, "Failed to read the retention policy of the cloud event stream");
            return null;
        }
    }

    /// <inheritdoc/>
    public virtual async Task<PartitionMetadata> GetPartitionMetadataAsync(string? @namespace, PartitionReference partition, CancellationToken cancellationToken = default)
    {
        try
        {
            var streamName = partition.GetStreamName(@namespace);
            var notFound = () => PartitionStreamNotFound(partition.Type, streamName);
            var firstEvent = await this.ReadStreamAsync(streamName, StreamReadDirection.Forwards, StreamPosition.StartOfStream, 1, notFound, cancellationToken).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            var lastEvent = await this.ReadStreamAsync(streamName, StreamReadDirection.Backwards, StreamPosition.EndOfStream, 1, notFound, cancellationToken).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (firstEvent == null || lastEvent == null)
            {
                // reading a partition stream that does not exist throws: this one exists, but every event it links to has expired (see #206)
                throw new ProblemDetailsException(new ProblemDetails(ProblemTypes.NotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, $"Every event of the partition of type '{EnumHelper.Stringify(partition.Type)}' with id '{partition.Id}' has expired"));
            }
            return new()
            {
                Id = partition.Id,
                Type = partition.Type,
                FirstEvent = firstEvent.Timestamp ?? default,
                LastEvent = lastEvent.Timestamp ?? default,
                Length = lastEvent.Sequence + 1,
                FirstOffset = firstEvent.Sequence
            };
        }
        catch (StreamNotFoundException)
        {
            throw new ProblemDetailsException(new ProblemDetails(ProblemTypes.NotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, $"Failed to find the stream '{partition.GetStreamName(@namespace)}' of the partition of type '{EnumHelper.Stringify(partition.Type)}' with id '{partition.Id}'. Related projection does not exist or has not been properly configured"));
        }
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<string> ListPartitionIdsAsync(CloudEventPartitionType partitionType, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var typeName = EnumHelper.Stringify(partitionType).Replace('-', '_');
        var projectionName = ProjectionNameHelper.CloudEventPartitionsMetadataPrefix + typeName;
        List<string>? partitionsIds = null;
        // a missing projection lists no partition, while any other failure is reported rather than being mistaken for an empty list (see #187)
        try
        {
            if (!await this.ProjectionExistsAsync(projectionName, cancellationToken).ConfigureAwait(false)) this.Logger.LogDebug("The projection '{projection}', which listed the partitions of type '{partitionType}' in earlier versions, does not exist", projectionName, partitionType);
            else partitionsIds = await this.ProjectionManager.GetStateAsync<List<string>>(projectionName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.Logger.LogError(ex, "Failed to read the projection '{projection}'", projectionName);
            throw new ProblemDetailsException(new ProblemDetails(new Uri("https://cloud-streams.io/problems/event-store-unavailable"), "Service Unavailable", (int)HttpStatusCode.ServiceUnavailable, $"Failed to list the partitions of type '{EnumHelper.Stringify(partitionType)}': the event store is unavailable"));
        }
        if (partitionsIds == null) yield break;
        await foreach (var id in partitionsIds.ToAsyncEnumerable()) yield return id;
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<CloudEventRecord> ReadAsync(string? @namespace, StreamReadDirection readDirection, long offset, ulong? length = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var streamName = string.IsNullOrWhiteSpace(@namespace) ? StreamNameHelper.All : StreamNameHelper.ByNamespace(@namespace);
        // an empty store, or a namespace without events, has no stream yet: reading it yields nothing
        await foreach (var record in this.ReadStreamAsync(streamName, readDirection, offset, length, null, cancellationToken).ConfigureAwait(false)) yield return record;
    }

    /// <inheritdoc/>
    public virtual IAsyncEnumerable<CloudEventRecord> ReadPartitionAsync(string? @namespace, PartitionReference partition, StreamReadDirection readDirection, long offset, ulong? length = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(partition);
        if (string.IsNullOrWhiteSpace(partition.Id)) throw new ArgumentNullException(nameof(partition), "The id of the partition to read is required");
        if (partition.Type == CloudEventPartitionType.BySource && !Uri.TryCreate(partition.Id, UriKind.RelativeOrAbsolute, out _))
            throw new ProblemDetailsException(new ProblemDetails(new Uri("https://cloud-streams.io/problems/validation-failed"), "Validation Failed", (int)HttpStatusCode.BadRequest, $"The id '{partition.Id}' of a partition of type '{EnumHelper.Stringify(CloudEventPartitionType.BySource)}' must be a valid URI"));
        var streamName = partition.GetStreamName(@namespace);
        return this.ReadStreamAsync(streamName, readDirection, offset, length, () => PartitionStreamNotFound(partition.Type, streamName), cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Records are appended in the order they are recorded, so their timestamps never decrease along a stream: the offset is found by a binary search, which reads
    /// a single record per step (see #211)
    /// </remarks>
    public virtual async Task<ulong?> FindOffsetAsync(string? @namespace, DateTimeOffset time, PartitionReference? partition = null, CancellationToken cancellationToken = default)
    {
        ulong length, first;
        try
        {
            if (partition == null)
            {
                var metadata = await this.GetStreamMetadataAsync(@namespace, cancellationToken).ConfigureAwait(false);
                (length, first) = (metadata.Length ?? 0, metadata.FirstOffset ?? 0);
            }
            else
            {
                var metadata = await this.GetPartitionMetadataAsync(@namespace, partition, cancellationToken).ConfigureAwait(false);
                (length, first) = (metadata.Length, metadata.FirstOffset);
            }
        }
        catch (Exception ex) when (ex is StreamNotFoundException || (ex is ProblemDetailsException notFound && notFound.Problem.Status == (int)HttpStatusCode.NotFound))
        {
            return null;
        }
        // the events before the first one retained have expired (see #206)
        ulong low = first, high = length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            var records = partition == null
                ? this.ReadAsync(@namespace, StreamReadDirection.Forwards, (long)middle, 1, cancellationToken)
                : this.ReadPartitionAsync(@namespace, partition, StreamReadDirection.Forwards, (long)middle, 1, cancellationToken);
            var record = await records.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            // a record that cannot be read, or whose timestamp is unknown, is deemed recent, which errs on the side of delivering more
            if (record?.Timestamp == null || record.Timestamp >= time) high = middle;
            else low = middle + 1;
        }
        return low < length ? low : null;
    }

    /// <summary>
    /// Reads the records of the specified stream
    /// </summary>
    /// <param name="streamName">The name of the stream to read</param>
    /// <param name="readDirection">The direction in which to read</param>
    /// <param name="offset">The offset starting from which to read records</param>
    /// <param name="length">The amount of records to read</param>
    /// <param name="notFound">A function that creates the exception to throw if the stream does not exist. If not set, reading a stream that does not exist yields nothing</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing the records read from the stream</returns>
    /// <remarks>
    /// The underlying store reports a missing stream while the records are being enumerated, not when the read is requested, so the enumeration itself is guarded
    /// </remarks>
    protected virtual IAsyncEnumerable<CloudEventRecord> ReadStreamAsync(string streamName, StreamReadDirection readDirection, long offset, ulong? length, Func<Exception>? notFound, CancellationToken cancellationToken = default)
    {
        return streamName == StreamNameHelper.All
            ? this.ReadRecordsAsync(streamName, readDirection, offset, length, notFound, cancellationToken)
            : this.ReadLinksAsync(streamName, readDirection, offset, length, notFound, cancellationToken);
    }

    /// <summary>
    /// Reads the records of the specified partition stream, whose events are links to the events of the cloud event stream, skipping the links to events that have expired
    /// </summary>
    /// <param name="streamName">The name of the partition stream to read</param>
    /// <param name="readDirection">The direction in which to read</param>
    /// <param name="offset">The offset starting from which to read records</param>
    /// <param name="length">The amount of records to read</param>
    /// <param name="notFound">A function that creates the exception to throw if the stream does not exist. If not set, reading a stream that does not exist yields nothing</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing the records read from the stream</returns>
    /// <remarks>
    /// The events of the cloud event stream expire according to its retention policy, but the links of partition streams do not, and the underlying event store fails
    /// to read a link whose event has expired (see #206). Since events expire from the oldest, expired links are always the first ones of a partition stream: a read
    /// forwards that fails on one is resumed from the first link whose event is retained, and a read backwards that reaches one ends there. Reads that meet no expired
    /// link cost nothing more
    /// </remarks>
    protected virtual async IAsyncEnumerable<CloudEventRecord> ReadLinksAsync(string streamName, StreamReadDirection readDirection, long offset, ulong? length, Func<Exception>? notFound, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var remaining = length ?? ulong.MaxValue;
        if (remaining == 0) yield break;
        var next = offset;
        if (readDirection == StreamReadDirection.Forwards && this._firstRetainedLinks.TryGetValue(streamName, out var known) && (next < 0 || (ulong)next < known)) next = (long)known;
        for (var recoveries = 0; ; recoveries++)
        {
            await using var enumerator = this.ReadRecordsAsync(streamName, readDirection, next, length.HasValue ? remaining : null, notFound, cancellationToken).GetAsyncEnumerator(cancellationToken);
            var expired = false;
            while (true)
            {
                bool hasNext;
                try { hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false); }
                catch (Exception ex) when (IsExpiredLink(ex))
                {
                    expired = true;
                    hasNext = false;
                }
                if (!hasNext) break;
                var record = enumerator.Current;
                yield return record;
                if (--remaining == 0) yield break;
                next = readDirection == StreamReadDirection.Forwards ? (long)record.Sequence + 1 : (long)record.Sequence - 1;
                if (next < 0) yield break;
            }
            // reading backwards ends with the first expired link, since the ones before it have expired too
            if (!expired || readDirection == StreamReadDirection.Backwards) yield break;
            if (recoveries >= MaxExpiredLinkRecoveries) throw new InvalidOperationException($"Failed to read the stream '{streamName}': its links kept expiring while it was being read");
            var first = await this.FindFirstRetainedLinkAsync(streamName, next < 0 ? 0 : (ulong)next, cancellationToken).ConfigureAwait(false);
            if (!first.HasValue) yield break;
            next = (long)first.Value;
        }
    }

    /// <summary>
    /// Finds the offset of the first link of the specified partition stream whose event is retained
    /// </summary>
    /// <param name="streamName">The name of the partition stream</param>
    /// <param name="expired">The offset of a link known to have expired</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The offset of the first link whose event is retained, or null if every event of the partition has expired</returns>
    /// <remarks>A binary search between the expired link and the last one, which takes a few dozen reads at most. Its result is kept, and starts the next reads</remarks>
    protected virtual async Task<ulong?> FindFirstRetainedLinkAsync(string streamName, ulong expired, CancellationToken cancellationToken)
    {
        IEventRecord? last;
        try { last = await this.EventStore.ReadAsync(streamName, StreamReadDirection.Backwards, StreamPosition.EndOfStream, 1, cancellationToken).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) when (IsExpiredLink(ex)) { last = null; }
        catch (StreamNotFoundException) { return null; }
        if (last == null)
        {
            // not remembered: links appended later are retained
            this.Logger.LogDebug("Every event linked by the stream '{stream}' has expired", streamName);
            return null;
        }
        ulong low = expired + 1, high = last.Offset;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            bool retained;
            try
            {
                await this.EventStore.ReadAsync(streamName, StreamReadDirection.Forwards, (long)middle, 1, cancellationToken).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
                retained = true;
            }
            catch (Exception ex) when (IsExpiredLink(ex)) { retained = false; }
            if (retained) high = middle;
            else low = middle + 1;
        }
        this._firstRetainedLinks.AddOrUpdate(streamName, low, (_, current) => Math.Max(current, low));
        this.Logger.LogDebug("The events linked by the stream '{stream}' before offset {offset} have expired", streamName, low);
        return low;
    }

    /// <summary>
    /// Determines whether the specified exception has been thrown by the underlying event store because it read a link whose event has expired
    /// </summary>
    /// <param name="ex">The exception to check</param>
    /// <returns>A boolean indicating whether the exception has been thrown because of an expired link</returns>
    /// <remarks>The event store does not report expired links: it fails to deserialize them, which is how they are recognized</remarks>
    protected static bool IsExpiredLink(Exception ex) => ex is NullReferenceException && ex.StackTrace?.Contains("DeserializeEventRecord", StringComparison.Ordinal) == true;

    /// <summary>
    /// Reads the records of the specified stream as they are
    /// </summary>
    /// <param name="streamName">The name of the stream to read</param>
    /// <param name="readDirection">The direction in which to read</param>
    /// <param name="offset">The offset starting from which to read records</param>
    /// <param name="length">The amount of records to read</param>
    /// <param name="notFound">A function that creates the exception to throw if the stream does not exist. If not set, reading a stream that does not exist yields nothing</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing the records read from the stream</returns>
    protected virtual async IAsyncEnumerable<CloudEventRecord> ReadRecordsAsync(string streamName, StreamReadDirection readDirection, long offset, ulong? length, Func<Exception>? notFound, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IAsyncEnumerator<IEventRecord> enumerator;
        try { enumerator = this.EventStore.ReadAsync(streamName, readDirection, offset, length, cancellationToken).GetAsyncEnumerator(cancellationToken); }
        catch (StreamNotFoundException) when (notFound != null) { throw notFound(); }
        catch (StreamNotFoundException) { yield break; }
        await using (enumerator.ConfigureAwait(false))
        {
            while (true)
            {
                bool hasNext;
                try { hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false); }
                catch (StreamNotFoundException) when (notFound != null) { throw notFound(); }
                catch (StreamNotFoundException) { hasNext = false; }
                if (!hasNext) yield break;
                yield return this.ReadRecord(enumerator.Current);
            }
        }
    }

    /// <summary>
    /// Creates a new <see cref="ProblemDetailsException"/> describing that the stream of the specified partition does not exist
    /// </summary>
    /// <param name="partitionType">The type of the partition whose stream does not exist</param>
    /// <param name="streamName">The name of the stream that does not exist</param>
    /// <returns>A new <see cref="ProblemDetailsException"/></returns>
    protected static ProblemDetailsException PartitionStreamNotFound(CloudEventPartitionType partitionType, string streamName) => new(new ProblemDetails(ProblemTypes.NotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound,
        $"Failed to find the stream '{streamName}' that belongs to the partition of type '{EnumHelper.Stringify(partitionType)}'. Either no event has been published to the partition yet, or the related projection does not exist or has not been properly configured"));

    /// <inheritdoc/>
    public virtual async Task<IObservable<CloudEventRecord>> ObserveAsync(string? @namespace, long offset = -1, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(@namespace)) return (await this.ObserveStreamAsync(StreamNameHelper.All, offset, cancellationToken).ConfigureAwait(false)).Select(this.ReadRecord);
        return await this.ObserveLinksAsync(StreamNameHelper.ByNamespace(@namespace), offset, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public virtual Task<IObservable<CloudEventRecord>> ObservePartitionAsync(string? @namespace, PartitionReference partition, long offset = -1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(partition);
        return this.ObserveLinksAsync(partition.GetStreamName(@namespace), offset, cancellationToken);
    }

    /// <summary>
    /// Observes the specified stream of links to the events of the cloud event stream, such as the stream of a namespace or of a partition
    /// </summary>
    /// <param name="streamName">The name of the stream to observe</param>
    /// <param name="offset">The offset of the first record to observe, or <see cref="StreamPosition.EndOfStream"/> to only observe the records appended from now on</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IObservable{T}"/> used to observe the records of the stream</returns>
    protected virtual async Task<IObservable<CloudEventRecord>> ObserveLinksAsync(string streamName, long offset, CancellationToken cancellationToken)
    {
        // observing from an expired link would fail: it starts with the first link whose event is retained (see #206)
        if (offset != StreamPosition.EndOfStream && this._firstRetainedLinks.TryGetValue(streamName, out var first) && (ulong)Math.Max(offset, 0) < first) offset = (long)first;
        else if (offset != StreamPosition.EndOfStream)
        {
            try { await this.EventStore.ReadAsync(streamName, StreamReadDirection.Forwards, offset, 1, cancellationToken).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false); }
            catch (Exception ex) when (IsExpiredLink(ex))
            {
                var retained = await this.FindFirstRetainedLinkAsync(streamName, (ulong)Math.Max(offset, 0), cancellationToken).ConfigureAwait(false);
                if (retained.HasValue) offset = (long)retained.Value;
                else offset = StreamPosition.EndOfStream;
            }
            catch (StreamNotFoundException) { }
        }
        return (await this.ObserveStreamAsync(streamName, offset, cancellationToken).ConfigureAwait(false)).Select(this.ReadRecord);
    }

    /// <summary>
    /// Observes the specified stream
    /// </summary>
    /// <param name="streamId">The id of the stream to observe</param>
    /// <param name="offset">The offset of the first record to observe, or <see cref="StreamPosition.EndOfStream"/> to only observe the records appended from now on</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IObservable{T}"/> used to observe the specified stream</returns>
    /// <remarks>Observing a stream from an offset is gapless: the records appended while the observation starts are not lost (see <see cref="GaplessStreamObservation"/>)</remarks>
    protected virtual async Task<IObservable<IEventRecord>> ObserveStreamAsync(string streamId, long offset, CancellationToken cancellationToken)
    {
        if (offset != StreamPosition.EndOfStream) return await GaplessStreamObservation.ObserveAsync(this.EventStore, streamId, offset, cancellationToken).ConfigureAwait(false);
        return await this.EventStore.ObserveAsync(streamId, offset, cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The underlying event store returned a null observable while observing stream '{streamId}'");
    }

    /// <inheritdoc/>
    public virtual Task TruncateAsync(ulong beforeVersion, CancellationToken cancellationToken = default) => this.EventStore.TruncateAsync(StreamNameHelper.All, beforeVersion, cancellationToken);

    /// <summary>
    /// Gets the definitions of the projections required by Cloud Streams, as the names of the projections and functions that create them
    /// </summary>
    /// <returns>The definitions of the projections required by Cloud Streams</returns>
    protected virtual IEnumerable<(string Name, Func<CancellationToken, Task> Create)> GetProjectionDefinitions()
    {
        // events stored by earlier versions have no namespace: they belong to the default namespace (see #215). The expressions are converted to JavaScript, which
        // is why the default namespace is written out, and why a conditional is used rather than a null-coalescing operator, which the conversion does not support
        yield return (ProjectionNameHelper.PartitionByNamespace, cancellationToken => this.ProjectionManager.CreateAsync<object>(ProjectionNameHelper.PartitionByNamespace,
            projection => projection
                .FromStream(StreamNameHelper.All)
                .When((state, e) => e.Metadata != null)
                .Then((state, e) => Projection.LinkEventTo(StreamNameHelper.ByNamespacePrefix + (e.Metadata!.Get("$namespace") != null ? e.Metadata!.Get("$namespace") : "default"), e)),
            cancellationToken: cancellationToken));
        yield return (ProjectionNameHelper.PartitionBySource, cancellationToken => this.ProjectionManager.CreateAsync<object>(ProjectionNameHelper.PartitionBySource,
            projection => projection
                .FromStream(StreamNameHelper.All)
                .When((state, e) => e.Metadata!.Get("source") != null)
                .Then((state, e) => Projection.LinkEventTo(StreamNameHelper.ByCloudEventSourcePrefix + (e.Metadata!.Get("$namespace") != null ? e.Metadata!.Get("$namespace") : "default") + "/" + e.Metadata!.Get("source"), e)),
            cancellationToken: cancellationToken));
        yield return (ProjectionNameHelper.PartitionBySubject, cancellationToken => this.ProjectionManager.CreateAsync<object>(ProjectionNameHelper.PartitionBySubject,
            projection => projection
                .FromStream(StreamNameHelper.All)
                .When((state, e) => e.Metadata!.Get("subject") != null)
                .Then((state, e) => Projection.LinkEventTo(StreamNameHelper.ByCloudEventSubjectPrefix + (e.Metadata!.Get("$namespace") != null ? e.Metadata!.Get("$namespace") : "default") + "/" + e.Metadata!.Get("subject"), e)),
            cancellationToken: cancellationToken));
        yield return (ProjectionNameHelper.PartitionByType, cancellationToken => this.ProjectionManager.CreateAsync<object>(ProjectionNameHelper.PartitionByType,
            projection => projection
                .FromStream(StreamNameHelper.All)
                .When((state, e) => e.Metadata!.Get("type") != null)
                .Then((state, e) => Projection.LinkEventTo(StreamNameHelper.ByCloudEventTypePrefix + (e.Metadata!.Get("$namespace") != null ? e.Metadata!.Get("$namespace") : "default") + "/" + e.Metadata!.Get("type"), e)),
            cancellationToken: cancellationToken));
        yield return (ProjectionNameHelper.PartitionByCorrelationId, cancellationToken => this.ProjectionManager.CreateAsync<object>(ProjectionNameHelper.PartitionByCorrelationId,
            projection => projection
                .FromStream(StreamNameHelper.All)
                .When((state, e) => e.Metadata!.Get("$correlationId") != null)
                .Then((state, e) => Projection.LinkEventTo(StreamNameHelper.ByCorrelationIdPrefix + (e.Metadata!.Get("$namespace") != null ? e.Metadata!.Get("$namespace") : "default") + "/" + e.Metadata!.Get("$correlationId"), e)),
            cancellationToken: cancellationToken));
        yield return (ProjectionNameHelper.PartitionByCausationId, cancellationToken => this.ProjectionManager.CreateAsync<object>(ProjectionNameHelper.PartitionByCausationId,
            projection => projection
                .FromStream(StreamNameHelper.All)
                .When((state, e) => e.Metadata!.Get("$causationId") != null)
                .Then((state, e) => Projection.LinkEventTo(StreamNameHelper.ByCausationIdPrefix + (e.Metadata!.Get("$namespace") != null ? e.Metadata!.Get("$namespace") : "default") + "/" + e.Metadata!.Get("$causationId"), e)),
            cancellationToken: cancellationToken));
        // the ids of the partitions are no longer listed by projections, whose state grows with every partition, but by the partition index (see #185)
    }

    /// <summary>
    /// Creates the projections required by Cloud Streams that do not exist yet
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The names of the projections that are still missing</returns>
    /// <remarks>
    /// Each projection is checked and created on its own, so that a projection introduced by a newer version is created on existing deployments, and a failure is
    /// logged instead of being swallowed (see #186)
    /// </remarks>
    public virtual async Task<IReadOnlyCollection<string>> SetupProjectionsAsync(CancellationToken cancellationToken = default)
    {
        var missing = new List<string>();
        foreach (var (name, create) in this.GetProjectionDefinitions())
        {
            try
            {
                if (await this.ProjectionExistsAsync(name, cancellationToken).ConfigureAwait(false)) continue;
                await create(cancellationToken).ConfigureAwait(false);
                this.Logger.LogInformation("Created the projection '{projection}'", name);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // another application, such as a broker started alongside a gateway, may have created the projection in the meantime, which EventStoreDB reports as a conflict
                if (await this.ProjectionExistsQuietlyAsync(name, cancellationToken).ConfigureAwait(false))
                {
                    this.Logger.LogDebug("The projection '{projection}' has been created concurrently", name);
                    continue;
                }
                this.Logger.LogError(ex, "Failed to set up the projection '{projection}'", name);
                missing.Add(name);
            }
        }
        this._missingProjections = missing;
        return missing;
    }

    /// <summary>
    /// Determines whether the specified projection exists
    /// </summary>
    /// <param name="name">The name of the projection to check</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A boolean indicating whether or not the specified projection exists</returns>
    /// <remarks>EventStoreDB reports a missing projection with a 'NotFound' <see cref="RpcException"/>, while any other failure means that existence is unknown, and is thrown</remarks>
    protected virtual async Task<bool> ProjectionExistsAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            return await this.ProjectionManager.GetStatusAsync(name, cancellationToken).ConfigureAwait(false) != null;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return false;
        }
    }

    /// <summary>
    /// Determines whether the specified projection exists, considering that it does not if that cannot be determined
    /// </summary>
    /// <param name="name">The name of the projection to check</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A boolean indicating whether or not the specified projection is known to exist</returns>
    protected virtual async Task<bool> ProjectionExistsQuietlyAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            return await this.ProjectionExistsAsync(name, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>
    /// Sets up the projections required by Cloud Streams, retrying with backoff until they all exist
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task EnsureProjectionsAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; !cancellationToken.IsCancellationRequested; attempt++)
        {
            try
            {
                var missing = await this.SetupProjectionsAsync(cancellationToken).ConfigureAwait(false);
                if (missing.Count == 0) return;
                var delay = TimeSpan.FromSeconds(Math.Min(5 * Math.Pow(2, attempt - 1), 300));
                this.Logger.LogWarning("{count} projection(s) could not be set up: {projections}. Retrying in {delay}...", missing.Count, string.Join(", ", missing), delay);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Parses the specified <see cref="CloudEvent"/> into a new <see cref="IEventDescriptor"/>
    /// </summary>
    /// <param name="eventDescriptor">An object that describes the <see cref="CloudEvent"/> to serialize</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The serialized <see cref="IEventDescriptor"/></returns>
    /// <remarks>
    /// The type of the event is read from its context attributes, rather than by round-tripping the whole event through JSON (see #188). An event without data,
    /// such as one whose data is binary and therefore carried by its <c>data_base64</c> attribute, is stored with JSON <c>null</c> data: the event store serializes
    /// data according to its runtime type, which a null reference does not have
    /// </remarks>
    protected virtual Task<IEventDescriptor> ParseEventDescriptorAsync(CloudEventDescriptor eventDescriptor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventDescriptor);
        if (!eventDescriptor.Metadata.ContextAttributes.TryGetValue(CloudEventAttributes.Type, out var typeAttribute) || typeAttribute?.ToString() is not { Length: > 0 } type)
            throw new ArgumentException($"The cloud event to append must define the '{CloudEventAttributes.Type}' context attribute", nameof(eventDescriptor));
        return Task.FromResult<IEventDescriptor>(new EventDescriptor(type, eventDescriptor.Data ?? NullData, eventDescriptor.Metadata.ContextAttributes));
    }

    /// <summary>
    /// Gets the data stored for events that have none, which serializes as JSON <c>null</c>
    /// </summary>
    protected static readonly object NullData = System.Text.Json.JsonDocument.Parse("null").RootElement.Clone();

    /// <summary>
    /// Deserializes the specified <see cref="IEventRecord"/> into a new <see cref="CloudEventRecord"/>
    /// </summary>
    /// <param name="e">The <see cref="IEventRecord"/> to convert</param>
    /// <returns>A new <see cref="CloudEventRecord"/></returns>
    protected virtual CloudEventRecord ReadRecord(IEventRecord e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return new(e.StreamId, e.Offset, new() { ContextAttributes = e.Metadata ?? new Dictionary<string, object>() }, e.Data) { Timestamp = e.Timestamp };
    }

}
