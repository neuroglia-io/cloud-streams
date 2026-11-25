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
/// Describes what became of one of the cloud events of a batch published to a gateway
/// </summary>
[DataContract]
public record CloudEventIngestionResult
{

    /// <summary>
    /// Gets/sets the index of the cloud event in the batch
    /// </summary>
    [DataMember(Order = 1, Name = "index"), JsonPropertyOrder(1), JsonPropertyName("index"), YamlMember(Order = 1, Alias = "index")]
    public virtual int Index { get; set; }

    /// <summary>
    /// Gets/sets the id of the cloud event, if it could be read
    /// </summary>
    [DataMember(Order = 2, Name = "id"), JsonPropertyOrder(2), JsonPropertyName("id"), YamlMember(Order = 2, Alias = "id")]
    public virtual string? Id { get; set; }

    /// <summary>
    /// Gets/sets the source of the cloud event, if it could be read
    /// </summary>
    [DataMember(Order = 3, Name = "source"), JsonPropertyOrder(3), JsonPropertyName("source"), YamlMember(Order = 3, Alias = "source")]
    public virtual string? Source { get; set; }

    /// <summary>
    /// Gets/sets the status the gateway would have answered had the cloud event been published alone: <c>201</c> if it has been stored, <c>200</c> if it is a
    /// duplicate, <c>202</c> if it has been ignored, or the status of the error that refused it
    /// </summary>
    [DataMember(Order = 4, Name = "status"), JsonPropertyOrder(4), JsonPropertyName("status"), YamlMember(Order = 4, Alias = "status")]
    public virtual int Status { get; set; }

    /// <summary>
    /// Gets/sets what became of the cloud event, if it has been accepted. See <see cref="CloudStreamsHeaders.Outcomes"/>
    /// </summary>
    [DataMember(Order = 5, Name = "outcome"), JsonPropertyOrder(5), JsonPropertyName("outcome"), YamlMember(Order = 5, Alias = "outcome")]
    public virtual string? Outcome { get; set; }

    /// <summary>
    /// Gets/sets the problem that describes why the cloud event has been refused, if it has
    /// </summary>
    [DataMember(Order = 6, Name = "problem"), JsonPropertyOrder(6), JsonPropertyName("problem"), YamlMember(Order = 6, Alias = "problem")]
    public virtual ProblemDetails? Problem { get; set; }

}
