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

namespace CloudStreams.Broker.Application.Services.Delivery;

/// <summary>
/// Schedules the retries of a delivery that failed, as configured by a retry policy: which failures are retried, how many times, after which delay, and when the
/// circuit breaks (see #163)
/// </summary>
/// <remarks>A schedule describes a single delivery: a new one is created for each delivery that fails</remarks>
/// <param name="policy">The retry policy to apply</param>
public sealed class DeliveryRetrySchedule(HttpClientRetryPolicy policy)
{

    readonly RetryBackoffDuration _backoffDuration = policy.BackoffDuration ?? RetryPolicy.DefaultRetryBackoffDuration;

    /// <summary>
    /// Gets the retry policy to apply
    /// </summary>
    public HttpClientRetryPolicy Policy { get; } = policy;

    /// <summary>
    /// Gets the maximum amount of retries. Negative to retry forever
    /// </summary>
    public int MaxAttempts { get; } = policy.MaxAttempts ?? RetryPolicy.DefaultMaxAttempts;

    /// <summary>
    /// Gets the amount of retries scheduled so far
    /// </summary>
    public int Attempts { get; private set; }

    /// <summary>
    /// Gets the amount of failures since the circuit last broke, the initial failure included
    /// </summary>
    public int ConsecutiveFailures { get; private set; } = 1;

    /// <summary>
    /// Gets a boolean indicating whether or not every retry allowed has been scheduled
    /// </summary>
    public bool IsExhausted => this.MaxAttempts >= 0 && this.Attempts >= this.MaxAttempts;

    /// <summary>
    /// Determines whether or not the specified failure may be retried
    /// </summary>
    /// <param name="failure">The failure to evaluate</param>
    /// <returns>A boolean indicating whether or not the failure may be retried</returns>
    /// <remarks>Failures that carry no status code (connection failures, timeouts) are always retried, since they say nothing about the event itself</remarks>
    public bool IsRetryable(HttpRequestException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        if (!failure.StatusCode.HasValue || this.Policy.StatusCodes == null || this.Policy.StatusCodes.Count == 0) return true;
        return this.Policy.StatusCodes.Contains((int)failure.StatusCode.Value);
    }

    /// <summary>
    /// Schedules the next retry
    /// </summary>
    /// <param name="circuitBroken">A boolean indicating whether or not the circuit broke, in which case the delay is at least the policy's break duration</param>
    /// <returns>The delay to wait before retrying</returns>
    public TimeSpan Next(out bool circuitBroken)
    {
        this.Attempts++;
        var delay = this._backoffDuration.ForAttempt(this.Attempts);
        circuitBroken = this.Policy.CircuitBreaker != null && this.ConsecutiveFailures >= this.Policy.CircuitBreaker.BreakAfter;
        if (circuitBroken)
        {
            var breakDuration = this.Policy.CircuitBreaker!.BreakDuration.ToTimeSpan();
            if (breakDuration > delay) delay = breakDuration;
            this.ConsecutiveFailures = 0;
        }
        return delay;
    }

    /// <summary>
    /// Records that a retry failed
    /// </summary>
    public void RecordFailure() => this.ConsecutiveFailures++;

}
