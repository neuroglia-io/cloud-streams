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