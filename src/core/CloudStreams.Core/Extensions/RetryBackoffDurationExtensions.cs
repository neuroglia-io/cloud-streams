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
    public static TimeSpan ForAttempt(this RetryBackoffDuration duration, int attemptNumber)
    {
        ArgumentNullException.ThrowIfNull(duration);
        var timeSpan = duration.Period.ToTimeSpan();
        var computed = duration.Type switch
        {
            RetryBackoffDurationType.Constant => timeSpan,
            //note that this raises the attempt number to the exponent, so growth is polynomial rather than exponential
            RetryBackoffDurationType.Exponential => Math.Pow(attemptNumber, duration.Exponent ?? throw new ArgumentException($"The specified {nameof(RetryBackoffDuration)} declares type '{nameof(RetryBackoffDurationType.Exponential)}' but does not set an '{nameof(RetryBackoffDuration.Exponent)}'", nameof(duration))) * timeSpan,
            RetryBackoffDurationType.Incremental => attemptNumber * timeSpan,
            _ => throw new NotSupportedException($"The specified {nameof(RetryBackoffDurationType)} '{duration.Type}' is not supported")
        };
        if (duration.MaxPeriod == null) return computed;
        var maxPeriod = duration.MaxPeriod.ToTimeSpan();
        return computed > maxPeriod ? maxPeriod : computed;
    }

}