// Copyright © 2023-Present The Cloud Streams Authors
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

using CloudStreams.Core.Data;

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
        if(duration == null) throw new ArgumentNullException(nameof(duration));
        return duration.Type switch
        {
            RetryBackoffDurationType.Constant => duration.Period,
            RetryBackoffDurationType.Exponential => Math.Pow(attemptNumber, duration.Exponent!.Value) * duration.Period,
            RetryBackoffDurationType.Incremental => attemptNumber * duration.Period,
            _ => throw new NotSupportedException($"The specified {nameof(RetryBackoffDurationType)} '{duration.Type}' is not supported")
        };
    }

}