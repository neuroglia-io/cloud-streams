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

namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure a retry backoff duration
/// </summary>
[DataContract]
public record RetryBackoffDuration
{

    /// <summary>
    /// Gets/sets the duration's type
    /// </summary>
    [Required]
    [DataMember(Order = 1, Name = "type", IsRequired = true), JsonPropertyName("type"), YamlMember(Alias = "type")]
    public virtual RetryBackoffDurationType Type { get; set; }

    /// <summary>
    /// Gets/sets the period of time to wait between retry attempts 
    /// </summary>
    [Required]
    [DataMember(Order = 2, Name = "period", IsRequired = true), JsonPropertyName("period"), YamlMember(Alias = "period")]
    public virtual Duration Period { get; set; } = null!;

    /// <summary>
    /// Gets/sets a value representing the power to which the specified period of time is to be raised to obtain the time to wait between each retry attempts
    /// </summary>
    [DataMember(Order = 3, Name = "exponent", IsRequired = true), JsonPropertyName("exponent"), YamlMember(Alias = "exponent")]
    public virtual double? Exponent { get; set; }

    /// <summary>
    /// Gets/sets the maximum period of time to wait between retry attempts, if any. If not set, an <see cref="RetryBackoffDurationType.Incremental"/> or
    /// <see cref="RetryBackoffDurationType.Exponential"/> duration grows without bound, which is only defensible for a retry sequence that is itself bounded
    /// </summary>
    [DataMember(Order = 4, Name = "maxPeriod"), JsonPropertyName("maxPeriod"), YamlMember(Alias = "maxPeriod")]
    public virtual Duration? MaxPeriod { get; set; }

    /// <summary>
    /// Creates a new constant <see cref="RetryBackoffDuration"/>
    /// </summary>
    /// <param name="period">The constant period of time to wait between retry attempts</param>
    /// <returns>A new constant <see cref="RetryBackoffDuration"/></returns>
    public static RetryBackoffDuration Constant(Duration period) => new() { Type = RetryBackoffDurationType.Constant, Period = period };

    /// <summary>
    /// Creates a new multiplier-based <see cref="RetryBackoffDuration"/>
    /// </summary>
    /// <param name="period">The constant period of time to wait between retry attempts</param>
    /// <param name="maxPeriod">The maximum period of time to wait between retry attempts, if any</param>
    /// <returns>A new multiplier-based <see cref="RetryBackoffDuration"/></returns>
    public static RetryBackoffDuration Incremental(Duration period, Duration? maxPeriod = null) => new() { Type = RetryBackoffDurationType.Incremental, Period = period, MaxPeriod = maxPeriod };

    /// <summary>
    /// Creates a new exponential <see cref="RetryBackoffDuration"/>
    /// </summary>
    /// <param name="period">The constant period of time to wait between retry attempts</param>
    /// <param name="exponent">The value representing the power to which the specified period of time is to be raised to obtain the time to wait between each retry attempts</param>
    /// <param name="maxPeriod">The maximum period of time to wait between retry attempts, if any</param>
    /// <returns>A new exponential <see cref="RetryBackoffDuration"/></returns>
    public static RetryBackoffDuration Exponential(Duration period, double exponent, Duration? maxPeriod = null) => new() { Type = RetryBackoffDurationType.Exponential, Period = period, Exponent = exponent, MaxPeriod = maxPeriod };

}