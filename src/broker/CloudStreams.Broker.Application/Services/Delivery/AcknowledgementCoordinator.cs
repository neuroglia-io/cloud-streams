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

using System.Diagnostics;

namespace CloudStreams.Broker.Application.Services.Delivery;

/// <summary>
/// Tracks the acknowledgement a subscriber has deferred, by responding <c>202 Accepted</c>, and schedules its expiry (see #163)
/// </summary>
/// <param name="onPendingCountChanged">A callback invoked with +1 when an acknowledgement starts being awaited, and with -1 when it stops being awaited</param>
/// <param name="onTimeout">A callback invoked with the token of an acknowledgement that has expired</param>
/// <remarks>
/// <para>
/// A subscription awaits at most one acknowledgement at a time, during which delivery is suspended. An acknowledgement is resolved once the resource watch has first
/// reported it pending, which it is said to be observed, then reported it gone, which means that the subscriber has called back: the watch lags behind the broker's own
/// patches, and may otherwise report no pending acknowledgement right after it has been recorded.
/// </para>
/// <para>The coordinator's state is guarded by its own lock, which is never held while calling back</para>
/// </remarks>
public sealed class AcknowledgementCoordinator(Action<int> onPendingCountChanged, Func<string, Task> onTimeout)
    : IDisposable
{

    readonly Lock _lock = new();
    long? _pendingSince;
    CancellationTokenSource? _timeoutCancellationTokenSource;

    /// <summary>
    /// Gets a boolean indicating whether or not an acknowledgement is awaited
    /// </summary>
    public bool IsAwaiting { get; private set; }

    /// <summary>
    /// Gets the hash of the token of the awaited acknowledgement, if any
    /// </summary>
    public string? Token { get; private set; }

    /// <summary>
    /// Gets the offset of the cloud event whose acknowledgement is awaited
    /// </summary>
    public ulong Offset { get; private set; }

    /// <summary>
    /// Gets a boolean indicating whether or not the resource watch has reported the awaited acknowledgement pending
    /// </summary>
    public bool IsObserved { get; private set; }

    /// <summary>
    /// Starts awaiting the specified acknowledgement
    /// </summary>
    /// <param name="token">The hash of the acknowledgement's token</param>
    /// <param name="offset">The offset of the cloud event whose acknowledgement is deferred</param>
    /// <param name="observed">A boolean indicating whether or not the acknowledgement is known to be recorded already, e.g. because it has been restored from the status after a restart</param>
    public void Begin(string token, ulong offset, bool observed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        bool started;
        lock (this._lock)
        {
            started = !this.IsAwaiting;
            this.IsAwaiting = true;
            // an acknowledgement restored from the status after a restart has been pending for an unknown time, which is therefore not recorded
            this._pendingSince = observed ? null : Stopwatch.GetTimestamp();
            this.Token = token;
            this.Offset = offset;
            this.IsObserved = observed;
        }
        if (started) onPendingCountChanged(1);
    }

    /// <summary>
    /// Stops awaiting the specified acknowledgement, and cancels its expiry
    /// </summary>
    /// <param name="token">The hash of the token of the acknowledgement to stop awaiting. If not set, stops awaiting whatever acknowledgement is awaited</param>
    /// <param name="pendingFor">How long the acknowledgement has been pending, if known</param>
    /// <returns>A boolean indicating whether or not the specified acknowledgement was awaited</returns>
    public bool End(string? token, out TimeSpan? pendingFor)
    {
        CancellationTokenSource? timeoutCancellationTokenSource;
        pendingFor = null;
        lock (this._lock)
        {
            if (!this.IsAwaiting || (token != null && token != this.Token)) return false;
            if (this._pendingSince.HasValue) pendingFor = Stopwatch.GetElapsedTime(this._pendingSince.Value);
            this._pendingSince = null;
            this.IsAwaiting = false;
            this.Token = null;
            this.IsObserved = false;
            timeoutCancellationTokenSource = this._timeoutCancellationTokenSource;
            this._timeoutCancellationTokenSource = null;
        }
        CancelAndDispose(timeoutCancellationTokenSource);
        onPendingCountChanged(-1);
        return true;
    }

    /// <summary>
    /// Records that the resource watch has reported the specified acknowledgement pending
    /// </summary>
    /// <param name="token">The hash of the token of the acknowledgement the watch reported</param>
    public void MarkObserved(string token)
    {
        lock (this._lock) if (this.IsAwaiting && token == this.Token) this.IsObserved = true;
    }

    /// <summary>
    /// Gets the offset of the awaited acknowledgement, if it has been observed
    /// </summary>
    /// <param name="offset">The offset of the cloud event whose acknowledgement is awaited</param>
    /// <returns>A boolean indicating whether or not an acknowledgement is awaited, and has been observed</returns>
    public bool TryGetObserved(out ulong offset)
    {
        lock (this._lock)
        {
            offset = this.Offset;
            return this.IsAwaiting && this.IsObserved;
        }
    }

    /// <summary>
    /// Gets the offset of the specified acknowledgement, if it is awaited
    /// </summary>
    /// <param name="token">The hash of the acknowledgement's token</param>
    /// <param name="offset">The offset of the cloud event whose acknowledgement is awaited</param>
    /// <returns>A boolean indicating whether or not the specified acknowledgement is awaited</returns>
    public bool TryGet(string token, out ulong offset)
    {
        lock (this._lock)
        {
            offset = this.Offset;
            return this.IsAwaiting && this.Token == token;
        }
    }

    /// <summary>
    /// Schedules the expiry of the specified acknowledgement, unless it is no longer awaited. Any expiry scheduled before is canceled
    /// </summary>
    /// <param name="token">The hash of the acknowledgement's token</param>
    /// <param name="expiresAt">The date and time at which the acknowledgement expires</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that cancels the expiry, e.g. when the subscription's handler is disposed of</param>
    public void ScheduleTimeout(string token, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationTokenSource? previous;
        lock (this._lock)
        {
            if (!this.IsAwaiting || this.Token != token)
            {
                timeoutCancellationTokenSource.Dispose();
                return;
            }
            previous = this._timeoutCancellationTokenSource;
            this._timeoutCancellationTokenSource = timeoutCancellationTokenSource;
        }
        CancelAndDispose(previous);
        var timeoutToken = timeoutCancellationTokenSource.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var delay = expiresAt - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero) await Task.Delay(delay, timeoutToken).ConfigureAwait(false);
                if (!timeoutToken.IsCancellationRequested) await onTimeout(token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException) { }
        }, CancellationToken.None);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        CancellationTokenSource? timeoutCancellationTokenSource;
        lock (this._lock)
        {
            timeoutCancellationTokenSource = this._timeoutCancellationTokenSource;
            this._timeoutCancellationTokenSource = null;
        }
        CancelAndDispose(timeoutCancellationTokenSource);
    }

    static void CancelAndDispose(CancellationTokenSource? cancellationTokenSource)
    {
        if (cancellationTokenSource == null) return;
        try { cancellationTokenSource.Cancel(); }
        catch (ObjectDisposedException) { }
        cancellationTokenSource.Dispose();
    }

}
