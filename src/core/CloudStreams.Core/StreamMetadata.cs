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
/// Represents an object used to describe a cloud event stream
/// </summary>
[DataContract]
public record StreamMetadata
{

    /// <summary>
    /// Gets/sets the date and time at which the first event has been appended to the described stream
    /// </summary>
    [DataMember(Order = 1, Name = "firstEvent", IsRequired = true), JsonPropertyName("firstEvent"), YamlMember(Alias = "firstEvent")]
    public virtual DateTimeOffset? FirstEvent { get; set; }

    /// <summary>
    /// Gets/sets the date and time at which the last event has been appended to the described stream
    /// </summary>
    [DataMember(Order = 2, Name = "lastEvent", IsRequired = true), JsonPropertyName("lastEvent"), YamlMember(Alias = "lastEvent")]
    public virtual DateTimeOffset? LastEvent { get; set; }

    /// <summary>
    /// Gets/sets the described stream's length
    /// </summary>
    [DataMember(Order = 3, Name = "length", IsRequired = true), JsonPropertyName("length"), YamlMember(Alias = "length")]
    public virtual ulong? Length { get; set; }

    /// <summary>
    /// Gets/sets the offset of the first event the described stream retains, if any: the events before it have expired (see #206)
    /// </summary>
    [DataMember(Order = 4, Name = "firstOffset"), JsonPropertyName("firstOffset"), YamlMember(Alias = "firstOffset")]
    public virtual ulong? FirstOffset { get; set; }

    /// <summary>
    /// Gets/sets the retention policy of the described stream, if any. Without one, events are kept forever
    /// </summary>
    [DataMember(Order = 5, Name = "retention"), JsonPropertyName("retention"), YamlMember(Alias = "retention")]
    public virtual StreamRetention? Retention { get; set; }

}

/// <summary>
/// Represents the retention policy of the cloud event stream: events older than <see cref="MaxAge"/>, or beyond the <see cref="MaxCount"/> most recent ones, expire (see #206)
/// </summary>
/// <remarks>Expired events can no longer be read, by subscriptions or otherwise, and are removed from disk when EventStoreDB is scavenged</remarks>
[DataContract]
public record StreamRetention
{

    /// <summary>
    /// Gets/sets the maximum age of the events to retain, if any
    /// </summary>
    [DataMember(Order = 1, Name = "maxAge"), JsonPropertyName("maxAge"), YamlMember(Alias = "maxAge")]
    public virtual Duration? MaxAge { get; set; }

    /// <summary>
    /// Gets/sets the maximum amount of events to retain, if any
    /// </summary>
    [DataMember(Order = 2, Name = "maxCount"), JsonPropertyName("maxCount"), YamlMember(Alias = "maxCount")]
    public virtual ulong? MaxCount { get; set; }

    /// <summary>
    /// Gets a boolean indicating whether the policy retains every event
    /// </summary>
    [IgnoreDataMember, JsonIgnore, YamlIgnore]
    public bool IsUnlimited => (this.MaxAge == null || this.MaxAge.ToTimeSpan() <= TimeSpan.Zero) && (this.MaxCount == null || this.MaxCount == 0);

}
