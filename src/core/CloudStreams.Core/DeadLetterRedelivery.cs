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
/// Describes the redelivery of a dead letter to its subscriber
/// </summary>
[DataContract]
public record DeadLetterRedelivery
{

    /// <summary>
    /// Gets/sets the offset of the redelivered dead letter
    /// </summary>
    [DataMember(Order = 1, Name = "offset"), JsonPropertyOrder(1), JsonPropertyName("offset"), YamlMember(Order = 1, Alias = "offset")]
    public virtual ulong Offset { get; set; }

    /// <summary>
    /// Gets/sets the status code the subscriber responded with
    /// </summary>
    [DataMember(Order = 2, Name = "status"), JsonPropertyOrder(2), JsonPropertyName("status"), YamlMember(Order = 2, Alias = "status")]
    public virtual int Status { get; set; }

}
