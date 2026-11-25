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

namespace CloudStreams.Core.Api.Configuration;

/// <summary>
/// Represents the options used to configure the retention policy of the cloud event stream (see #206)
/// </summary>
/// <remarks>
/// The policy belongs to the stream, which every gateway shares: it is applied when an application that configures it starts, and left as it is by the others.
/// Configure the same policy on every gateway. To remove a policy, set both values to 0
/// </remarks>
public class RetentionOptions
{

    /// <summary>
    /// Gets the name of the configuration section the options are bound to
    /// </summary>
    public const string ConfigurationSection = "Retention";

    /// <summary>
    /// Gets/sets the maximum age of the events to retain, as an ISO 8601 duration (e.g. <c>P30D</c>), if any. <c>PT0S</c> retains events of any age
    /// </summary>
    public virtual string? MaxAge { get; set; }

    /// <summary>
    /// Gets/sets the maximum amount of events to retain, if any. 0 retains any amount of events
    /// </summary>
    public virtual ulong? MaxCount { get; set; }

    /// <summary>
    /// Gets the configured retention policy
    /// </summary>
    /// <returns>The configured retention policy, or null if none is configured, in which case the current policy of the stream is left as it is</returns>
    /// <exception cref="FormatException">The configured maximum age is not a valid ISO 8601 duration, or the maximum amount of events is too large</exception>
    public virtual StreamRetention? GetPolicy()
    {
        if (string.IsNullOrWhiteSpace(this.MaxAge) && !this.MaxCount.HasValue) return null;
        Duration? maxAge = null;
        if (!string.IsNullOrWhiteSpace(this.MaxAge))
        {
            if (!Duration.TryParse(this.MaxAge, out var parsed)) throw new FormatException($"The configured '{ConfigurationSection}:{nameof(MaxAge)}' '{this.MaxAge}' is not a valid, non-negative ISO 8601 duration, such as 'P30D' or 'PT12H'");
            if (parsed.ToTimeSpan() > TimeSpan.Zero && parsed.ToTimeSpan() < TimeSpan.FromSeconds(1)) throw new FormatException($"The configured '{ConfigurationSection}:{nameof(MaxAge)}' must be at least one second long");
            maxAge = parsed;
        }
        if (this.MaxCount > int.MaxValue) throw new FormatException($"The configured '{ConfigurationSection}:{nameof(MaxCount)}' may not exceed {int.MaxValue}");
        return new() { MaxAge = maxAge, MaxCount = this.MaxCount };
    }

}
