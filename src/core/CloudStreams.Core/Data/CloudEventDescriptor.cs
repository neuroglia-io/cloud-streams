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

namespace CloudStreams.Core.Data;

/// <summary>
/// Represents an object used to describe a cloud event
/// </summary>
[DataContract]
public class CloudEventDescriptor
{

    /// <summary>
    /// Initializes a new <see cref="CloudEventDescriptor"/>
    /// </summary>
    public CloudEventDescriptor() { }

    /// <summary>
    /// Initializes a new <see cref="CloudEventDescriptor"/>
    /// </summary>
    /// <param name="metadata">An recorded cloud event's metadata</param>
    /// <param name="data">The </param>
    public CloudEventDescriptor(CloudEventMetadata metadata, object? data = null)
    {
        this.Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        this.Data = data;
    }

    /// <summary>
    /// Gets/sets the recorded cloud event's metadata
    /// </summary>
    [DataMember(Order = 1, Name = "metadata"), JsonPropertyName("metadata"), YamlMember(Alias = "metadata")]
    public virtual CloudEventMetadata Metadata { get; set; } = null!;


    /// <summary>
    /// Gets/sets the cloud event's data
    /// </summary>
    [DataMember(Order = 2, Name = "data"), JsonPropertyName("data"), YamlMember(Alias = "data")]
    public virtual object? Data { get; set; } = null!;

}