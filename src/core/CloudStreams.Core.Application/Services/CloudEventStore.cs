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
public class CloudEventStore(IEventStore eventStore, IProjectionManager projectionManager, IJsonSerializer serializer, ILogger<CloudEventStore>? logger = null, IPartitionIndex? partitionIndex = null)
    : IHostedService, ICloudEventStore, IDisposable
{

    readonly CancellationTokenSource _cancellationTokenSource = new();
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
    public void Dispose()
    {
        this._cancellationTokenSource.Cancel();
        this._cancellationTokenSource.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public virtual async Task<CloudEventRecord> AppendAsync(CloudEventDescriptor e, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        var streamName = Streams.All;
        var descriptor = await this.ParseEventDescriptorAsync(e, cancellationToken).ConfigureAwait(false);
        var offset = await this.EventStore.AppendAsync(streamName, [descriptor], cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.IndexPartitionsAsync(e.Metadata, cancellationToken).ConfigureAwait(false);
        return new(streamName, offset, e.Metadata, e.Data);
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
            await partitionIndex.AddAsync(GetPartitions(metadata), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.Logger.LogError(ex, "Failed to index the partitions of a cloud event: they may be missing from partition listings");
        }
    }

    /// <inheritdoc/>
    public virtual async Task<StreamMetadata> GetStreamMetadataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var firstEvent = (await this.EventStore.ReadAsync(Streams.All, StreamReadDirection.Forwards, StreamPosition.StartOfStream, 1, cancellationToken: cancellationToken).ToListAsync(cancellationToken)).SingleOrDefault();
            var lastEvent = (await this.EventStore.ReadAsync(Streams.All, StreamReadDirection.Backwards, StreamPosition.EndOfStream, 1, cancellationToken: cancellationToken).ToListAsync(cancellationToken)).SingleOrDefault();
            // a stream that exists but holds no event, e.g. after it has been truncated, is described like a stream that does not exist yet
            if (firstEvent == null || lastEvent == null) return new();
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
            var firstEvent = (await this.EventStore.ReadAsync(streamName, StreamReadDirection.Forwards, StreamPosition.StartOfStream, 1, cancellationToken: cancellationToken).ToListAsync(cancellationToken)).SingleOrDefault();
            var lastEvent = (await this.EventStore.ReadAsync(streamName, StreamReadDirection.Backwards, StreamPosition.EndOfStream, 1, cancellationToken: cancellationToken).ToListAsync(cancellationToken)).SingleOrDefault();
            if (firstEvent == null || lastEvent == null) throw PartitionStreamNotFound(partition.Type, streamName);
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
        var projectionName = Projections.CloudEventPartitionsMetadataPrefix + typeName;
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
    public virtual async IAsyncEnumerable<CloudEventRecord> ReadAsync(StreamReadDirection readDirection, long offset, ulong? length = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var streamName = Streams.All;
        // an empty store has no stream yet: reading it yields nothing
        await foreach (var record in this.ReadStreamAsync(streamName, readDirection, offset, length, null, cancellationToken).ConfigureAwait(false)) yield return record;
    }

    /// <inheritdoc/>
    public virtual IAsyncEnumerable<CloudEventRecord> ReadPartitionAsync(PartitionReference partition, StreamReadDirection readDirection, long offset, ulong? length = null, CancellationToken cancellationToken = default)
    {
        switch (partition.Type)
        {
            case CloudEventPartitionType.BySource:
                if (!Uri.TryCreate(partition.Id, UriKind.RelativeOrAbsolute, out var source)) throw new ProblemDetailsException(new ProblemDetails(new Uri("https://cloud-streams.io/problems/validation-failed"), "Validation Failed", (int)HttpStatusCode.BadRequest, $"The id '{partition.Id}' of a partition of type '{EnumHelper.Stringify(CloudEventPartitionType.BySource)}' must be a valid URI"));
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
    protected virtual async IAsyncEnumerable<CloudEventRecord> ReadStreamAsync(string streamName, StreamReadDirection readDirection, long offset, ulong? length, Func<Exception>? notFound, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IAsyncEnumerator<IEventRecord> enumerator;
        try { enumerator = this.EventStore.ReadAsync(streamName, readDirection, offset, length ?? ulong.MaxValue, cancellationToken).GetAsyncEnumerator(cancellationToken); }
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
    public virtual async Task<IObservable<CloudEventRecord>> ObserveAsync(long offset = -1, CancellationToken cancellationToken = default)
    {
        var subscription = await this.EventStore.ObserveAsync(
            Streams.All,
            offset,
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (subscription == null)
        {
            throw new InvalidOperationException($"The underlying event store returned a null observable while observing stream '{Streams.All}' at offset '{offset}'.");
        }
        return subscription.Select(this.ReadRecord);
    }

    /// <inheritdoc/>
    public virtual async Task<IObservable<CloudEventRecord>> ObservePartitionAsync(PartitionReference partition, long offset = -1, CancellationToken cancellationToken = default)
    {
        var streamName = partition.GetStreamName();
        var subscription = await this.EventStore.ObserveAsync(
           streamName,
           offset,
           cancellationToken: cancellationToken)
           .ConfigureAwait(false);
        if (subscription == null)
        {
            throw new InvalidOperationException($"The underlying event store returned a null observable while observing partition stream '{streamName}' at offset '{offset}'.");
        }
        return subscription.Select(this.ReadRecord);
    }

    /// <inheritdoc/>
    public virtual Task TruncateAsync(ulong beforeVersion, CancellationToken cancellationToken = default) => this.EventStore.TruncateAsync(Streams.All, beforeVersion, cancellationToken);

    /// <summary>
    /// Gets the definitions of the projections required by Cloud Streams, as the names of the projections and functions that create them
    /// </summary>
    /// <returns>The definitions of the projections required by Cloud Streams</returns>
    protected virtual IEnumerable<(string Name, Func<CancellationToken, Task> Create)> GetProjectionDefinitions()
    {
        yield return (Projections.PartitionBySource, cancellationToken => this.ProjectionManager.CreateAsync<object>(Projections.PartitionBySource,
            projection => projection
                .FromStream(Streams.All)
                .When((state, e) => e.Metadata!.Get("source") != null)
                .Then((state, e) => Projection.LinkEventTo("$by-source-" + e.Metadata!.Get("source"), e)),
            cancellationToken: cancellationToken));
        yield return (Projections.PartitionBySubject, cancellationToken => this.ProjectionManager.CreateAsync<object>(Projections.PartitionBySubject,
            projection => projection
                .FromStream(Streams.All)
                .When((state, e) => e.Metadata!.Get("subject") != null)
                .Then((state, e) => Projection.LinkEventTo("$by-subject-" + e.Metadata!.Get("subject"), e)),
            cancellationToken: cancellationToken));
        yield return (Projections.PartitionByCausationId, cancellationToken => this.ProjectionManager.CreateAsync<object>(Projections.PartitionByCausationId,
            projection => projection
                .FromStream(Streams.All)
                .When((state, e) => e.Metadata!.Get("$causationId") != null)
                .Then((state, e) => Projection.LinkEventTo("$by-causation-" + e.Metadata!.Get("$causationId"), e)),
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
        await foreach (var record in this.ReadStreamAsync(streamName, readDirection, offset, length, () => PartitionStreamNotFound(CloudEventPartitionType.BySource, streamName), cancellationToken).ConfigureAwait(false)) yield return record;
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
        await foreach (var record in this.ReadStreamAsync(streamName, readDirection, offset, length, () => PartitionStreamNotFound(CloudEventPartitionType.BySubject, streamName), cancellationToken).ConfigureAwait(false)) yield return record;
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
        await foreach (var record in this.ReadStreamAsync(streamName, readDirection, offset, length, () => PartitionStreamNotFound(CloudEventPartitionType.ByType, streamName), cancellationToken).ConfigureAwait(false)) yield return record;
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
        await foreach (var record in this.ReadStreamAsync(streamName, readDirection, offset, length, () => PartitionStreamNotFound(CloudEventPartitionType.ByCorrelationId, streamName), cancellationToken).ConfigureAwait(false)) yield return record;
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
        await foreach (var record in this.ReadStreamAsync(streamName, readDirection, offset, length, () => PartitionStreamNotFound(CloudEventPartitionType.ByCausationId, streamName), cancellationToken).ConfigureAwait(false)) yield return record;
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
