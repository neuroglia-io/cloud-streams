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
/// Represents an object used to configure a retry policy
/// </summary>
[DataContract]
public record RetryPolicy
{

    static readonly RetryBackoffDuration DefaultRetryBackoffDuration = RetryBackoffDuration.Constant(Duration.FromSeconds(3));
    const int DefaultMaxAttempts = 5;

    /// <summary>
    /// Initializes a new <see cref="RetryPolicy"/>
    /// </summary>
    public RetryPolicy() { }

    /// <summary>
    /// Initializes a new <see cref="RetryPolicy"/>
    /// </summary>
    /// <param name="backoffDuration"></param>
    /// <param name="maxAttempts"></param>
    public RetryPolicy(RetryBackoffDuration backoffDuration, int? maxAttempts = null)
    {
        this.BackoffDuration = backoffDuration ?? DefaultRetryBackoffDuration;
        this.MaxAttempts = maxAttempts;
    }

    /// <summary>
    /// Gets/sets an object used to configure the backoff duration between retry attempts
    /// </summary>
    [Required]
    [DataMember(Order = 1, Name = "backoffDuration", IsRequired = true), JsonPropertyName("backoffDuration"), YamlMember(Alias = "backoffDuration")]
    public virtual RetryBackoffDuration BackoffDuration { get; set; } = null!;

    /// <summary>
    /// Gets/sets the maximum retry attempts to perform. If not set, it will retry forever
    /// </summary>
    [DataMember(Order = 2, Name = "maxAttempts"), JsonPropertyName("maxAttempts"), YamlMember(Alias = "maxAttempts")]
    public virtual int? MaxAttempts { get; set; }

}
