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

using System.Reactive.Subjects;
using System.Runtime.CompilerServices;

namespace CloudStreams.UnitTests.Services;

/// <summary>
/// An in-memory <see cref="ICloudEventStore"/> backed by a list of <see cref="CloudEventRecord"/>s and a <see cref="Subject{T}"/>
/// </summary>
/// <remarks>
/// Hand-written rather than substituted because <c>ReadOneAsync</c> is an extension method over <see cref="ReadAsync"/>:
/// a mocking library cannot intercept it, so the read path has to behave for real.
/// </remarks>
public class FakeCloudEventStore
    : ICloudEventStore, IDisposable
{

    readonly Lock _lock = new();
    readonly Subject<CloudEventRecord> _stream = new();
    readonly List<CloudEventRecord> _records = [];
    int _reads;
    bool _disposed;

    /// <summary>
    /// Gets a list containing the records held by the store, ordered by sequence
    /// </summary>
    public IReadOnlyList<CloudEventRecord> Records { get { lock (this._lock) return [.. this._records]; } }

    /// <summary>
    /// Gets the <see cref="Subject{T}"/> used to push live <see cref="CloudEventRecord"/>s to observers
    /// </summary>
    public Subject<CloudEventRecord> Stream => this._stream;

    /// <summary>
    /// Gets/sets an <see cref="Exception"/>, if any, to throw while the stream is being read
    /// </summary>
    public Exception? ReadFault { get; set; }

    /// <summary>
    /// Gets/sets the number of reads to serve before <see cref="ReadFault"/> is thrown.
    /// <para>
    /// Defaults to 0, which faults the first read. The catch-up loop reads one record per iteration, so setting this to N
    /// lets N records be dispatched before the loop fails — which is what "fails mid-iteration" actually means. The fault is
    /// raised from inside the enumeration rather than at call time, matching how a real store fails.
    /// </para>
    /// </summary>
    public int ReadFaultAfter { get; set; }

    /// <summary>
    /// Gets the number of times the stream has been read
    /// </summary>
    public int ReadCount { get { lock (this._lock) return this._reads; } }

    /// <summary>
    /// Gets the length of the stream, as reported by <see cref="GetStreamMetadataAsync"/>
    /// </summary>
    public ulong Length { get { lock (this._lock) return (ulong)this._records.Count; } }

    /// <summary>
    /// Adds the specified <see cref="CloudEventRecord"/>s to the store
    /// </summary>
    /// <param name="records">The <see cref="CloudEventRecord"/>s to add</param>
    /// <returns>The configured <see cref="FakeCloudEventStore"/></returns>
    public FakeCloudEventStore With(params CloudEventRecord[] records)
    {
        lock (this._lock) this._records.AddRange(records);
        return this;
    }

    /// <inheritdoc/>
    public Task<StreamMetadata> GetStreamMetadataAsync(CancellationToken cancellationToken = default) => Task.FromResult(new StreamMetadata() { Length = this.Length });

    /// <inheritdoc/>
    public async IAsyncEnumerable<CloudEventRecord> ReadAsync(StreamReadDirection readDirection, long offset, ulong? length = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<CloudEventRecord> snapshot;
        bool shouldFault;
        //snapshot under the lock: the returned sequence is enumerated lazily and by more than one caller, so querying the
        //live list would throw 'collection was modified' the moment a test appended to it
        lock (this._lock)
        {
            this._reads++;
            shouldFault = this.ReadFault != null && this._reads > this.ReadFaultAfter;
            snapshot = [.. this._records];
        }
        var records = readDirection == StreamReadDirection.Backwards
            ? snapshot.AsEnumerable().Reverse().Where(r => offset == StreamPosition.EndOfStream || (long)r.Sequence <= offset)
            : snapshot.Where(r => offset == StreamPosition.StartOfStream || (long)r.Sequence >= offset);
        if (length.HasValue) records = records.Take((int)length.Value);
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (shouldFault) throw this.ReadFault!;
            yield return record;
        }
        if (shouldFault) throw this.ReadFault!;
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<IObservable<CloudEventRecord>> ObserveAsync(long offset = StreamPosition.EndOfStream, CancellationToken cancellationToken = default) => Task.FromResult<IObservable<CloudEventRecord>>(this._stream);

    /// <inheritdoc/>
    public IAsyncEnumerable<CloudEventRecord> ReadPartitionAsync(PartitionReference partition, StreamReadDirection readDirection, long offset, ulong? length = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(FakeCloudEventStore)} does not model partitions. SubscriptionHandler takes a separate branch for partitioned subscriptions, and delegating to the unpartitioned read here would let a partition-filtering regression pass green. Implement real partition filtering before writing a partitioned test.");

    /// <inheritdoc/>
    public Task<IObservable<CloudEventRecord>> ObservePartitionAsync(PartitionReference partition, long offset = StreamPosition.EndOfStream, CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(FakeCloudEventStore)} does not model partitions. See {nameof(ReadPartitionAsync)}.");

    /// <inheritdoc/>
    public Task<CloudEventRecord> AppendAsync(CloudEventDescriptor e, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<PartitionMetadata> GetPartitionMetadataAsync(PartitionReference partition, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public IAsyncEnumerable<string> ListPartitionIdsAsync(CloudEventPartitionType partitionType, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task TruncateAsync(ulong beforeVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this._disposed) return;
        this._stream.Dispose();
        this._disposed = true;
        GC.SuppressFinalize(this);
    }

}
