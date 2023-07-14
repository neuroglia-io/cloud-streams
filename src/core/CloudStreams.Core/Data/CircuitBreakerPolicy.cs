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

using CloudStreams.Core.Serialization.Json.Converters;

namespace CloudStreams.Core.Data;

/// <summary>
/// Represents an object used to configure a circuit breaker
/// </summary>
[DataContract]
public class CircuitBreakerPolicy
{

    /// <summary>
    /// Initializes a new <see cref="CircuitBreakerPolicy"/>
    /// </summary>
    public CircuitBreakerPolicy() { }

    /// <summary>
    /// Initializes a new <see cref="CircuitBreakerPolicy"/>
    /// </summary>
    /// <param name="breakAfter">The maximum attempts after which to break the circuit</param>
    /// <param name="breakDuration">The duration the circuit remains broker</param>
    public CircuitBreakerPolicy(int breakAfter, TimeSpan breakDuration)
    {
        this.BreakAfter = breakAfter;
        this.BreakDuration = breakDuration;
    }

    /// <summary>
    /// Gets/sets the maximum attempts after which to break the circuit
    /// </summary>
    [Required]
    [DataMember(Order = 1, Name = "breakAfter", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("breakAfter"), YamlMember(Order = 1, Alias = "breakAfter")]
    public virtual int BreakAfter { get; set; }

    /// <summary>
    /// Gets/sets the duration the circuit remains broker
    /// </summary>
    [Required, JsonConverter(typeof(Iso8601TimeSpanConverter))]
    [DataMember(Order = 2, Name = "breakDuration", IsRequired = true), JsonPropertyOrder(2), JsonPropertyName("breakDuration"), YamlMember(Order = 2, Alias = "breakDuration")]
    public virtual TimeSpan BreakDuration { get; set; }

}