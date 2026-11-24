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

using Neuroglia.Data.Infrastructure.EventSourcing;
using Neuroglia.Data.Infrastructure.EventSourcing.Services;
using System.Reactive.Disposables;

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Observes a stream from a given offset without missing the records appended while the observation starts
/// </summary>
/// <remarks>
/// <para>
/// The event store observes a stream from an offset by reading the records it holds, then subscribing to the records appended after its end. A record appended
/// between the read and the subscription is lost, which happens whenever events are published while a subscription starts, and routinely for partitions, whose
/// records are linked asynchronously by projections (see #204).
/// </para>
/// <para>
/// The observation is therefore made the other way around: the live records are subscribed to first, and buffered, before the records the stream holds are read.
/// The observer then receives the records read, followed by the live records that come after them, each exactly once.
/// </para>
/// </remarks>
public sealed class GaplessStreamObservation
    : IObservable<IEventRecord>, IDisposable
{

    readonly Lock _lock = new();
    readonly IDisposable _liveSubscription;
    List<IEventRecord>? _buffer = [];
    Exception? _error;
    bool _completed;
    IObserver<IEventRecord>? _observer;
    IReadOnlyList<IEventRecord> _history = [];
    long _lastOffset;
    bool _disposed;

    GaplessStreamObservation(IObservable<IEventRecord> live, long offset)
    {
        this._lastOffset = offset - 1;
        this._liveSubscription = live.Subscribe(this.OnNext, this.OnError, this.OnCompleted);
    }

    /// <summary>
    /// Observes the specified stream, starting at the specified offset
    /// </summary>
    /// <param name="eventStore">The event store to observe</param>
    /// <param name="streamId">The id of the stream to observe</param>
    /// <param name="offset">The offset of the first record to observe, which must not be the end of the stream</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IObservable{T}"/> that yields every record of the stream starting at the specified offset, then every record appended to it</returns>
    public static async Task<IObservable<IEventRecord>> ObserveAsync(IEventStore eventStore, string streamId, long offset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventStore);
        ArgumentOutOfRangeException.ThrowIfLessThan(offset, StreamPosition.StartOfStream);
        var live = await eventStore.ObserveAsync(streamId, StreamPosition.EndOfStream, cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The underlying event store returned a null observable while observing stream '{streamId}'");
        var observation = new GaplessStreamObservation(live, offset);
        try
        {
            observation._history = await eventStore.ReadAsync(streamId, StreamReadDirection.Forwards, offset, cancellationToken: cancellationToken).ToListAsync(cancellationToken).ConfigureAwait(false);
            return observation;
        }
        catch
        {
            observation.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<IEventRecord> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (this._lock)
        {
            ObjectDisposedException.ThrowIf(this._disposed, this);
            if (this._observer != null) throw new InvalidOperationException("A stream observation supports a single observer");
            foreach (var record in this._history) this.Emit(observer, record);
            this._history = [];
            foreach (var record in this._buffer!) this.Emit(observer, record);
            this._buffer = null;
            if (this._error != null) observer.OnError(this._error);
            else if (this._completed) observer.OnCompleted();
            else this._observer = observer;
        }
        return Disposable.Create(this.Dispose);
    }

    void Emit(IObserver<IEventRecord> observer, IEventRecord record)
    {
        // the records read and the live records overlap when a record is appended after the subscription but before the read: it is only emitted once
        if ((long)record.Offset <= this._lastOffset) return;
        this._lastOffset = (long)record.Offset;
        observer.OnNext(record);
    }

    void OnNext(IEventRecord record)
    {
        lock (this._lock)
        {
            if (this._observer == null) this._buffer?.Add(record);
            else this.Emit(this._observer, record);
        }
    }

    void OnError(Exception error)
    {
        lock (this._lock)
        {
            if (this._observer == null) this._error = error;
            else this._observer.OnError(error);
        }
    }

    void OnCompleted()
    {
        lock (this._lock)
        {
            if (this._observer == null) this._completed = true;
            else this._observer.OnCompleted();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (this._lock)
        {
            if (this._disposed) return;
            this._disposed = true;
            this._observer = null;
            this._buffer = null;
        }
        this._liveSubscription.Dispose();
    }

}
