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
/// Represents an <see cref="IBrokerMetrics"/> implementation that counts increments instead of emitting them
/// </summary>
public class RecordingBrokerMetrics
    : IBrokerMetrics
{

    int _totalPublishedEvents;
    int _totalDeliveryFailures;

    /// <summary>
    /// Gets the amount of times <see cref="IncrementTotalPublishedEvents"/> has been called
    /// </summary>
    public int TotalPublishedEvents => Volatile.Read(ref this._totalPublishedEvents);

    /// <summary>
    /// Gets the amount of times <see cref="IncrementTotalDeliveryFailures"/> has been called
    /// </summary>
    public int TotalDeliveryFailures => Volatile.Read(ref this._totalDeliveryFailures);

    /// <summary>
    /// Resets both counters to zero
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref this._totalPublishedEvents, 0);
        Interlocked.Exchange(ref this._totalDeliveryFailures, 0);
        Interlocked.Exchange(ref this._totalAcknowledgementTimeouts, 0);
        lock (this._deliveryDurations) this._deliveryDurations.Clear();
        lock (this._acknowledgementDurations) this._acknowledgementDurations.Clear(); // the pending acknowledgements gauge is a level rather than a count, so it is deliberately kept
    }

    /// <inheritdoc/>
    public void IncrementTotalPublishedEvents(string subscription, string stream) => Interlocked.Increment(ref this._totalPublishedEvents);

    /// <inheritdoc/>
    public void IncrementTotalDeliveryFailures(string subscription, string stream) => Interlocked.Increment(ref this._totalDeliveryFailures);

    int _pendingAcknowledgements;
    int _totalAcknowledgementTimeouts;

    /// <summary>
    /// Gets the current value of the pending acknowledgements gauge
    /// </summary>
    public int PendingAcknowledgements => Volatile.Read(ref this._pendingAcknowledgements);

    /// <summary>
    /// Gets the amount of times <see cref="IncrementTotalAcknowledgementTimeouts"/> has been called
    /// </summary>
    public int TotalAcknowledgementTimeouts => Volatile.Read(ref this._totalAcknowledgementTimeouts);

    /// <inheritdoc/>
    public void AddPendingAcknowledgements(string subscription, string stream, int delta) => Interlocked.Add(ref this._pendingAcknowledgements, delta);

    /// <inheritdoc/>
    public void IncrementTotalAcknowledgementTimeouts(string subscription, string stream) => Interlocked.Increment(ref this._totalAcknowledgementTimeouts);

    readonly List<(string Outcome, TimeSpan Duration)> _deliveryDurations = [];
    readonly List<(string Outcome, TimeSpan Duration)> _acknowledgementDurations = [];
    Func<(string Stream, ulong Lag)?>? _lag;

    /// <summary>
    /// Gets the outcomes and durations of the delivery attempts recorded so far
    /// </summary>
    public IReadOnlyList<(string Outcome, TimeSpan Duration)> DeliveryDurations { get { lock (this._deliveryDurations) return [.. this._deliveryDurations]; } }

    /// <summary>
    /// Gets the outcomes and durations of the deferred acknowledgements recorded so far
    /// </summary>
    public IReadOnlyList<(string Outcome, TimeSpan Duration)> AcknowledgementDurations { get { lock (this._acknowledgementDurations) return [.. this._acknowledgementDurations]; } }

    /// <summary>
    /// Gets the lag currently reported by the registered subscription, if any
    /// </summary>
    public (string Stream, ulong Lag)? Lag => this._lag?.Invoke();

    /// <summary>
    /// Gets a boolean indicating whether or not a subscription's lag is being observed
    /// </summary>
    public bool ObservesLag => this._lag != null;

    /// <inheritdoc/>
    public void RecordDeliveryDuration(string subscription, string stream, string outcome, TimeSpan duration) { lock (this._deliveryDurations) this._deliveryDurations.Add((outcome, duration)); }

    /// <inheritdoc/>
    public void RecordAcknowledgementDuration(string subscription, string stream, string outcome, TimeSpan duration) { lock (this._acknowledgementDurations) this._acknowledgementDurations.Add((outcome, duration)); }

    /// <inheritdoc/>
    public IDisposable ObserveLag(string subscription, Func<(string Stream, ulong Lag)?> observe)
    {
        this._lag = observe;
        return new LagRegistration(this, observe);
    }

    /// <inheritdoc/>
    public void Dispose() => GC.SuppressFinalize(this);

    sealed class LagRegistration(RecordingBrokerMetrics metrics, Func<(string Stream, ulong Lag)?> observe)
        : IDisposable
    {
        public void Dispose() => Interlocked.CompareExchange(ref metrics._lag, null, observe);
    }

}
