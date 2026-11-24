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
    }

    /// <inheritdoc/>
    public void IncrementTotalPublishedEvents(string? stream, string? subscriber) => Interlocked.Increment(ref this._totalPublishedEvents);

    /// <inheritdoc/>
    public void IncrementTotalDeliveryFailures(string? stream, string? subscriber) => Interlocked.Increment(ref this._totalDeliveryFailures);

    /// <inheritdoc/>
    public void Dispose() => GC.SuppressFinalize(this);

}
