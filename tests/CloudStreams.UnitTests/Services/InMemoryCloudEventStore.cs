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

namespace CloudStreams.UnitTests.Services;

/// <summary>
/// Represents an in-memory <see cref="ICloudEventStore"/> backed by a list of records
/// </summary>
/// <remarks>
/// A substitute is not enough here: <c>ReadOneAsync</c> is a static extension over <c>ReadAsync</c>, so it cannot be intercepted, and leaving
/// <c>ReadAsync</c> unstubbed makes catch-up throw inside a fire-and-forget task where nothing surfaces it. A real store also makes the
/// record count the stream length, so the two cannot drift apart.
/// </remarks>
public sealed class InMemoryCloudEventStore
    : ICloudEventStore, IDisposable
{

    readonly List<CloudEventRecord> _records = [];
    readonly Subject<CloudEventRecord> _live = new();
    readonly Lock _lock = new();

    /// <summary>
    /// Gets/sets a boolean indicating whether observing should hand back a null stream, reproducing the contract violation behind issue #113
    /// </summary>
    public bool ObserveReturnsNull { get; set; }

    /// <summary>
    /// Gets the records currently held, ordered by sequence
    /// </summary>
    public IReadOnlyList<CloudEventRecord> Records { get { lock (this._lock) return [.. this._records]; } }

    /// <summary>
    /// Gets the amount of records currently held, which is also the stream's length
    /// </summary>
    public ulong Length { get { lock (this._lock) return (ulong)this._records.Count; } }

    /// <summary>
    /// Seeds the store with the specified amount of records, sequenced from zero
    /// </summary>
    public InMemoryCloudEventStore Seed(int count)
    {
        lock (this._lock) for (var i = 0; i < count; i++) this._records.Add(BrokerTestContext.CreateRecord((ulong)this._records.Count));
        return this;
    }

    /// <summary>
    /// Appends a record and pushes it to observers, the way a real append does
    /// </summary>
    public CloudEventRecord Publish()
    {
        CloudEventRecord record;
        lock (this._lock)
        {
            record = BrokerTestContext.CreateRecord((ulong)this._records.Count);
            this._records.Add(record);
        }
        this._live.OnNext(record);
        return record;
    }

    /// <inheritdoc/>
    public Task<StreamMetadata> GetStreamMetadataAsync(CancellationToken cancellationToken = default) => Task.FromResult(new StreamMetadata() { Length = this.Length });

    /// <inheritdoc/>
    public Task<PartitionMetadata> GetPartitionMetadataAsync(PartitionReference partition, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PartitionMetadata() { Type = partition.Type, Id = partition.Id, Length = this.Length });

    /// <inheritdoc/>
    public IAsyncEnumerable<CloudEventRecord> ReadAsync(StreamReadDirection readDirection, long offset, ulong? length = null, CancellationToken cancellationToken = default) => this.Read(readDirection, offset, length);

    /// <inheritdoc/>
    public IAsyncEnumerable<CloudEventRecord> ReadPartitionAsync(PartitionReference partition, StreamReadDirection readDirection, long offset, ulong? length = null, CancellationToken cancellationToken = default) => this.Read(readDirection, offset, length);

    /// <inheritdoc/>
    public Task<IObservable<CloudEventRecord>> ObserveAsync(long offset = StreamPosition.EndOfStream, CancellationToken cancellationToken = default) => Task.FromResult<IObservable<CloudEventRecord>>(this.ObserveReturnsNull ? null! : this._live);

    /// <inheritdoc/>
    public Task<IObservable<CloudEventRecord>> ObservePartitionAsync(PartitionReference partition, long offset = StreamPosition.EndOfStream, CancellationToken cancellationToken = default) => Task.FromResult<IObservable<CloudEventRecord>>(this.ObserveReturnsNull ? null! : this._live);

    /// <inheritdoc/>
    public Task<CloudEventRecord> AppendAsync(CloudEventDescriptor e, CancellationToken cancellationToken = default) => Task.FromResult(this.Publish());

    /// <inheritdoc/>
    public IAsyncEnumerable<string> ListPartitionIdsAsync(CloudEventPartitionType partitionType, CancellationToken cancellationToken = default) => AsyncEnumerable.Empty<string>();

    /// <inheritdoc/>
    public Task TruncateAsync(ulong beforeVersion, CancellationToken cancellationToken = default)
    {
        lock (this._lock) this._records.RemoveAll(r => r.Sequence < beforeVersion);
        return Task.CompletedTask;
    }

    IAsyncEnumerable<CloudEventRecord> Read(StreamReadDirection readDirection, long offset, ulong? length)
    {
        List<CloudEventRecord> matches;
        lock (this._lock)
        {
            matches = readDirection == StreamReadDirection.Backwards
                ? [.. this._records.Where(r => offset == StreamPosition.EndOfStream || r.Sequence <= (ulong)offset).Reverse()]
                : [.. this._records.Where(r => offset == StreamPosition.EndOfStream || r.Sequence >= (ulong)offset)];
        }
        if (length.HasValue) matches = [.. matches.Take((int)length.Value)];
        return matches.ToAsyncEnumerable();
    }

    /// <inheritdoc/>
    public void Dispose() => this._live.Dispose();

}
