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
/// Represents a publisher allowed to publish cloud events to a gateway, and the sources it may publish cloud events of (see #215)
/// </summary>
/// <remarks>
/// A publisher is identified either by an API key, or by bearer tokens issued by the authority Cloud Streams is configured with, whose subject and claims match
/// </remarks>
[DataContract]
public record IngestionPublisher
{

    /// <summary>
    /// Gets/sets the publisher's name, which identifies it in rate limits and metrics
    /// </summary>
    [Required, MinLength(1)]
    [DataMember(Order = 1, Name = "name", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("name"), YamlMember(Order = 1, Alias = "name")]
    public virtual string Name { get; set; } = null!;

    /// <summary>
    /// Gets/sets the API key that identifies the publisher, if any: the key itself, or preferably the secret of the gateway's namespace that holds it
    /// </summary>
    [DataMember(Order = 2, Name = "apiKey"), JsonPropertyOrder(2), JsonPropertyName("apiKey"), YamlMember(Order = 2, Alias = "apiKey")]
    public virtual ApiKeyAuthenticationPolicy? ApiKey { get; set; }

    /// <summary>
    /// Gets/sets the subject (<c>sub</c> claim) of the bearer tokens that identify the publisher, if any
    /// </summary>
    [DataMember(Order = 3, Name = "subject"), JsonPropertyOrder(3), JsonPropertyName("subject"), YamlMember(Order = 3, Alias = "subject")]
    public virtual string? Subject { get; set; }

    /// <summary>
    /// Gets/sets the claims the bearer tokens that identify the publisher must all have, if any, e.g. <c>azp: payments</c>
    /// </summary>
    [DataMember(Order = 4, Name = "claims"), JsonPropertyOrder(4), JsonPropertyName("claims"), YamlMember(Order = 4, Alias = "claims")]
    public virtual IDictionary<string, string>? Claims { get; set; }

    /// <summary>
    /// Gets/sets the patterns of the sources the publisher may publish cloud events of. It may publish cloud events of any source if not set
    /// </summary>
    [DataMember(Order = 5, Name = "sources"), JsonPropertyOrder(5), JsonPropertyName("sources"), YamlMember(Order = 5, Alias = "sources")]
    public virtual List<string>? Sources { get; set; }

    /// <summary>
    /// Gets a boolean indicating whether or not the publisher is identified by bearer tokens
    /// </summary>
    [IgnoreDataMember, JsonIgnore, YamlIgnore]
    public virtual bool UsesToken => !string.IsNullOrWhiteSpace(this.Subject) || this.Claims?.Count > 0;

}
