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
/// Represents an object used to describe a set of rules that apply to a specific cloud event source
/// </summary>
[DataContract]
public class CloudEventAuthorizationPolicy
{

    /// <summary>
    /// Gets/sets the strategy to use when deciding whether or not the authorization policy applies<para></para>
    /// See <see cref="RuleBasedDecisionStrategy"/>
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "decisionStrategy", IsRequired = true), JsonPropertyName("decisionStrategy"), YamlMember(Alias = "decisionStrategy")]
    public virtual string DecisionStrategy { get; set; } = null!;

    /// <summary>
    /// Gets/sets a list containing the rules the policy is made out of
    /// </summary>
    [DataMember(Order = 2, Name = "rules"), JsonPropertyName("rules"), YamlMember(Alias = "rules")]
    public virtual List<CloudEventAuthorizationRule>? Rules { get; set; } = new();

}