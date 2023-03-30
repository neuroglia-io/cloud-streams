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
/// Represents an object used to describe the status of a cloud event stream
/// </summary>
[DataContract]
public class CloudEventStreamStatus
{

    /// <summary>
    /// Gets/sets the acked offset in the cloud event stream starting from which to receive events
    /// </summary>
    [DataMember(Order = 1, Name = "ackedOffset"), JsonPropertyName("ackedOffset"), YamlMember(Alias = "ackedOffset")]
    public virtual ulong? AckedOffset { get; set; }

    /// <summary>
    /// Gets/sets an object that describes the last fault that occured while streaming events to subscribers. Streaming is interrupted when fault is set, requiring a user to manually resume streaming
    /// </summary>
    [DataMember(Order = 2, Name = "fault"), JsonPropertyName("fault"), YamlMember(Alias = "fault")]
    public virtual ProblemDetails? Fault { get; set; }

}