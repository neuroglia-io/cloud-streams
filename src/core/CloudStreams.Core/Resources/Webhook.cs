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
/// Represents an object used to configure a webhook
/// </summary>
[DataContract]
public record Webhook
{

    /// <summary>
    /// Gets/sets the address of the service to post back to
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "serviceUri", IsRequired = true), JsonPropertyName("serviceUri"), YamlMember(Alias = "serviceUri")]
    public virtual Uri ServiceUri { get; set; } = null!;

}