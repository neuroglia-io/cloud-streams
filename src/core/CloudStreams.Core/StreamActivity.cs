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

namespace CloudStreams.Core;

/// <summary>
/// Represents an object that describes the recent activity of the cloud event stream: how many cloud events have been recorded over time, and by whom (see #225)
/// </summary>
[DataContract]
public record StreamActivity
{

    /// <summary>
    /// Gets the maximum amount of buckets an activity may be split into
    /// </summary>
    public const int MaxBuckets = 96;

    /// <summary>
    /// Gets the maximum amount of recent cloud events the most active sources and types may be computed from
    /// </summary>
    public const int MaxSample = 1000;

    /// <summary>
    /// Gets/sets the start of the period the activity covers
    /// </summary>
    [DataMember(Order = 1, Name = "from"), JsonPropertyName("from")]
    public virtual DateTimeOffset From { get; set; }

    /// <summary>
    /// Gets/sets the end of the period the activity covers
    /// </summary>
    [DataMember(Order = 2, Name = "to"), JsonPropertyName("to")]
    public virtual DateTimeOffset To { get; set; }

    /// <summary>
    /// Gets/sets the amount of cloud events recorded in each consecutive slice of the period, oldest first
    /// </summary>
    [DataMember(Order = 3, Name = "buckets"), JsonPropertyName("buckets")]
    public virtual List<StreamActivityBucket> Buckets { get; set; } = [];

    /// <summary>
    /// Gets/sets the amount of most recent cloud events <see cref="Sources"/> and <see cref="Types"/> have been computed from
    /// </summary>
    [DataMember(Order = 4, Name = "sampleSize"), JsonPropertyName("sampleSize")]
    public virtual int SampleSize { get; set; }

    /// <summary>
    /// Gets/sets the sources that produced the most cloud events of the sample, most active first
    /// </summary>
    [DataMember(Order = 5, Name = "sources"), JsonPropertyName("sources")]
    public virtual List<StreamActivityCount> Sources { get; set; } = [];

    /// <summary>
    /// Gets/sets the types of the most cloud events of the sample, most frequent first
    /// </summary>
    [DataMember(Order = 6, Name = "types"), JsonPropertyName("types")]
    public virtual List<StreamActivityCount> Types { get; set; } = [];

}

/// <summary>
/// Represents the amount of cloud events recorded during a slice of a <see cref="StreamActivity"/>'s period
/// </summary>
[DataContract]
public record StreamActivityBucket
{

    /// <summary>
    /// Gets/sets the start of the slice
    /// </summary>
    [DataMember(Order = 1, Name = "from"), JsonPropertyName("from")]
    public virtual DateTimeOffset From { get; set; }

    /// <summary>
    /// Gets/sets the end of the slice
    /// </summary>
    [DataMember(Order = 2, Name = "to"), JsonPropertyName("to")]
    public virtual DateTimeOffset To { get; set; }

    /// <summary>
    /// Gets/sets the amount of cloud events recorded during the slice
    /// </summary>
    [DataMember(Order = 3, Name = "count"), JsonPropertyName("count")]
    public virtual ulong Count { get; set; }

}

/// <summary>
/// Represents the amount of cloud events that share a value, such as a source or a type
/// </summary>
[DataContract]
public record StreamActivityCount
{

    /// <summary>
    /// Gets/sets the shared value
    /// </summary>
    [DataMember(Order = 1, Name = "value"), JsonPropertyName("value")]
    public virtual string Value { get; set; } = null!;

    /// <summary>
    /// Gets/sets the amount of cloud events that have the value
    /// </summary>
    [DataMember(Order = 2, Name = "count"), JsonPropertyName("count")]
    public virtual int Count { get; set; }

}
