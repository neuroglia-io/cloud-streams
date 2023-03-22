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

namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to configure the options of a query used to read a cloud event stream
/// </summary>
[DataContract]
public record StreamReadOptions
{

    /// <summary>
    /// Gets the maximum length of a stream read operation
    /// </summary>
    public const long MaxLength = 100;

    /// <summary>
    /// Initializes a new <see cref="StreamReadOptions"/>
    /// </summary>
    public StreamReadOptions() { }

    /// <summary>
    /// Initializes a new <see cref="StreamReadOptions"/>
    /// </summary>
    /// <param name="partition">A reference to the cloud event partition to read</param>
    /// <param name="direction">The direction in which to read the stream of cloud events</param>
    /// <param name="offset">The offset starting from which to read the stream</param>
    /// <param name="length">The amount of events to read from the stream</param>
    public StreamReadOptions(PartitionReference partition, StreamReadDirection direction = StreamReadDirection.Forwards, long? offset = null, ulong length = MaxLength)
    {
        this.Partition = partition;
        this.Direction = direction;
        this.Offset = offset;
        this.Length = length;
    }

    /// <summary>
    /// Initializes a new <see cref="StreamReadOptions"/>
    /// </summary>
    /// <param name="direction">The direction in which to read the stream of cloud events</param>
    /// <param name="offset">The offset starting from which to read the stream</param>
    /// <param name="length">The amount of events to read from the stream</param>
    public StreamReadOptions(StreamReadDirection direction = StreamReadDirection.Forwards, long? offset = null, ulong length = MaxLength) : this(null!, direction, offset, length) { }

    /// <summary>
    /// Gets/sets a reference to the cloud event partition to read, if any
    /// </summary>
    [DataMember(Order = 1, Name = "partition"), JsonPropertyName("partition"), YamlMember(Alias = "partition")]
    public virtual PartitionReference? Partition { get; set; }

    /// <summary>
    /// Gets/sets the direction in which to read the stream of cloud events
    /// </summary>
    [DefaultValue(StreamReadDirection.Forwards)]
    [DataMember(Order = 2, Name = "direction"), JsonPropertyName("direction"), YamlMember(Alias = "direction")]
    public virtual StreamReadDirection Direction { get; set; } = StreamReadDirection.Forwards;

    /// <summary>
    /// Gets/sets the offset starting from which to read the stream
    /// </summary>
    [DataMember(Order = 3, Name = "offset"), JsonPropertyName("offset"), YamlMember(Alias = "offset")]
    public virtual long? Offset { get; set; }

    /// <summary>
    /// Gets/sets the amount of events to read from the stream
    /// </summary>
    [DefaultValue(MaxLength), Range(1, MaxLength)]
    [DataMember(Order = 4, Name = "length"), JsonPropertyName("length"), YamlMember(Alias = "length")]
    public virtual ulong Length { get; set; } = MaxLength;

}