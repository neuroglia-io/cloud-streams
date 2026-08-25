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

using Polly.Utilities;

namespace CloudStreams.UnitTests.Services;

/// <summary>
/// Replaces Polly's sleep primitives with recorders that complete immediately, so that retry tests can assert the exact
/// backoff schedule a policy produces instead of waiting for it in real time
/// </summary>
/// <remarks>
/// <see cref="SystemClock"/> is process-global mutable state, which is why the assembly disables test parallelization
/// (see Usings.cs). Always dispose, so <see cref="SystemClock.Reset"/> restores the real clock for subsequent tests.
/// </remarks>
public sealed class RecordingSystemClock
    : IDisposable
{

    /// <summary>
    /// The number of waits the clock records by default before failing the test
    /// </summary>
    public const int DefaultMaxDelays = 100;

    readonly Lock _lock = new();
    readonly List<TimeSpan> _delays = [];
    DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    bool _disposed;

    /// <summary>
    /// Gets/sets the number of waits the clock will record before failing the test.
    /// <para>
    /// The companion to <see cref="StubHttpMessageHandler.MaxRequests"/>, and necessary because that valve cannot catch every
    /// runaway: an unbounded retry whose attempts are being short-circuited - by an open circuit, or by a predicate that stops
    /// issuing requests - spins forever without sending anything. Against a virtual clock that loop is infinitely fast.
    /// </para>
    /// </summary>
    public int MaxDelays { get; set; } = DefaultMaxDelays;

    RecordingSystemClock()
    {
        SystemClock.SleepAsync = (delay, _) =>
        {
            this.Advance(delay);
            return Task.CompletedTask;
        };
        SystemClock.Sleep = (delay, _) => this.Advance(delay);
        //the circuit breaker measures its break duration against SystemClock.UtcNow. Leaving that as the real clock while
        //making every wait instantaneous collapses the whole retry sequence into a few microseconds, so a breaker would open
        //and never reset, and no test could exercise a break expiring. Virtual time advances by exactly what Polly waited for
        SystemClock.UtcNow = () => this.Now.UtcDateTime;
        SystemClock.DateTimeOffsetUtcNow = () => this.Now;
    }

    /// <summary>
    /// Gets the current virtual time
    /// </summary>
    public DateTimeOffset Now { get { lock (this._lock) return this._now; } }

    void Advance(TimeSpan delay)
    {
        lock (this._lock)
        {
            if (this._delays.Count >= this.MaxDelays) throw new InvalidOperationException($"Polly waited more than {this.MaxDelays} times. This is the runaway-retry safety valve: either the retry sequence is not terminating, or this test legitimately needs a higher RecordingSystemClock.MaxDelays.");
            this._delays.Add(delay);
            this._now += delay;
        }
    }

    /// <summary>
    /// Gets a list containing every backoff duration Polly waited for, in order
    /// </summary>
    public IReadOnlyList<TimeSpan> Delays { get { lock (this._lock) return [.. this._delays]; } }

    /// <summary>
    /// Gets the total amount of time Polly would have waited for
    /// </summary>
    public TimeSpan TotalDelay => this.Delays.Aggregate(TimeSpan.Zero, (total, delay) => total + delay);

    /// <summary>
    /// Installs a new <see cref="RecordingSystemClock"/>
    /// </summary>
    /// <returns>A new <see cref="RecordingSystemClock"/>, which must be disposed to restore Polly's real clock</returns>
    public static RecordingSystemClock Install() => new();

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this._disposed) return;
        SystemClock.Reset();
        this._disposed = true;
    }

}
