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
/// Represents an object used to configure a cloud event stream
/// </summary>
[DataContract]
public record CloudEventStream
{

    /// <summary>
    /// Gets/sets the desired offset in the cloud event stream starting from which to receive events.<para></para>
    /// '0' specifies the start of the stream, '-1' the end of the stream. Defaults to '-1'
    /// </summary>
    [DataMember(Order = 1, Name = "offset"), JsonPropertyName("offset"), YamlMember(Alias = "offset")]
    public virtual long? Offset { get; set; }

}