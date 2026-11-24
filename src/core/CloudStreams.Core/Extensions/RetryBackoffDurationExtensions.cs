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

using CloudStreams.Core.Resources;

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="RetryBackoffDuration"/>s
/// </summary>
public static class RetryBackoffDurationExtensions
{

    /// <summary>
    /// Computes the backoff duration for the specified retry attempt
    /// </summary>
    /// <param name="duration">The extended <see cref="RetryBackoffDuration"/></param>
    /// <param name="attemptNumber">The number of the retry attempt to get the backoff duration for</param>
    /// <returns>A new <see cref="TimeSpan"/> that represents the backoff duration for the specified retry attempt</returns>
    /// <remarks>
    /// The backoff never exceeds the configured maximum delay, which defaults to <see cref="RetryBackoffDuration.DefaultMaxDelay"/>, so that retrying forever can never
    /// overflow. An exponential backoff without exponent uses <see cref="RetryBackoffDuration.DefaultExponent"/> (see #169)
    /// </remarks>
    public static TimeSpan ForAttempt(this RetryBackoffDuration duration, int attemptNumber)
    {
        ArgumentNullException.ThrowIfNull(duration);
        if (attemptNumber < 0) attemptNumber = 0;
        var period = duration.Period?.ToTimeSpan() ?? TimeSpan.Zero;
        var maxDelay = duration.MaxDelay?.ToTimeSpan() ?? RetryBackoffDuration.DefaultMaxDelay;
        var factor = duration.Type switch
        {
            RetryBackoffDurationType.Constant => 1d,
            RetryBackoffDurationType.Exponential => Math.Pow(attemptNumber, duration.Exponent ?? RetryBackoffDuration.DefaultExponent),
            RetryBackoffDurationType.Incremental => attemptNumber,
            _ => throw new NotSupportedException($"The specified {nameof(RetryBackoffDurationType)} '{duration.Type}' is not supported")
        };
        var ticks = factor * period.Ticks;
        if (double.IsNaN(ticks) || ticks < 0) return TimeSpan.Zero;
        return ticks >= maxDelay.Ticks ? maxDelay : TimeSpan.FromTicks((long)ticks);
    }

}