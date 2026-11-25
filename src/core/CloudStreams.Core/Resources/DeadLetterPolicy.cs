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
/// Represents an object used to configure what becomes of the cloud events a subscription fails to deliver
/// </summary>
/// <remarks>
/// A cloud event whose delivery fails for good, because the retry policy is exhausted or the subscriber responded with a status code it does not retry, is set aside
/// with the reason why, and delivery carries on with the next one, rather than faulting the subscription or dropping the event (see #207)
/// </remarks>
[DataContract]
public record DeadLetterPolicy
{

    /// <summary>
    /// Gets/sets the uri of the service to send dead-lettered cloud events to, as structured cloud events, if any. If not set, dead-lettered cloud events are stored
    /// in the subscription's dead-letter stream, which the API browses and redelivers
    /// </summary>
    [DataMember(Order = 1, Name = "uri"), JsonPropertyOrder(1), JsonPropertyName("uri"), YamlMember(Order = 1, Alias = "uri")]
    public virtual Uri? Uri { get; set; }

}
