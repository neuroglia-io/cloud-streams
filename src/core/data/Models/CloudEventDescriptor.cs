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
/// Represents an object used to describe a cloud event record
/// </summary>
[DataContract]
public class CloudEventRecord
{

    /// <summary>
    /// Initializes a new <see cref="CloudEventRecord"/>
    /// </summary>
    public CloudEventRecord() { }

    /// <summary>
    /// Initializes a new <see cref="CloudEventRecord"/>
    /// </summary>
    /// <param name="streamId">The id of the stream the recorded cloud event belongs to</param>
    /// <param name="sequence">The sequence of the recorded cloud event in the stream it belongs to</param>
    /// <param name="metadata">An recorded cloud event's metadata</param>
    /// <param name="data">The recorded cloud event's data</param>
    public CloudEventRecord(string streamId, ulong sequence, CloudEventMetadata metadata, object data)
    {
        if(string.IsNullOrWhiteSpace(streamId)) throw new ArgumentNullException(nameof(streamId));
        this.StreamId = streamId;
        this.Sequence = sequence;
        this.Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        this.Data = data;
    }

    /// <summary>
    /// Gets/sets the id of the stream the recorded cloud event belongs to
    /// </summary>
    [DataMember(Order = 1, Name = "streamId"), JsonPropertyName("streamId"), YamlMember(Alias = "streamId")]
    public virtual string StreamId { get; set; } = null!;

    /// <summary>
    /// Gets/sets the sequence of the recorded cloud event in the stream it belongs to
    /// </summary>
    [DataMember(Order = 2, Name = "sequence"), JsonPropertyName("sequence"), YamlMember(Alias = "sequence")]
    public virtual ulong Sequence { get; set; }

    /// <summary>
    /// Gets/sets the recorded cloud event's metadata
    /// </summary>
    [DataMember(Order = 3, Name = "metadata"), JsonPropertyName("metadata"), YamlMember(Alias = "metadata")]
    public virtual CloudEventMetadata Metadata { get; set; } = null!;

    /// <summary>
    /// Gets/sets the recorded cloud event's data
    /// </summary>
    [DataMember(Order = 4, Name = "data"), JsonPropertyName("data"), YamlMember(Alias = "data")]
    public virtual object Data { get; set; } = null!;

}