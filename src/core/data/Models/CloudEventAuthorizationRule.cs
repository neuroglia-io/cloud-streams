namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an authorization rule that applies to events produced by a specific source
/// </summary>
[DataContract]
public class CloudEventAuthorizationRule
{

    /// <summary>
    /// Gets/sets the rule's type<para></para>
    /// See <see cref="CloudEventAuthorizationRuleType"/>
    /// </summary>
    [Required, JsonRequired, MinLength(1)]
    [DataMember(Order = 1, Name = "type", IsRequired = true), JsonPropertyName("type"), YamlMember(Alias = "type")]
    public virtual string Type { get; set; } = null!;

    /// <summary>
    /// Gets/sets the rule's effect. Defaults to '<see cref="AuthorizationPolicyEffect.Authorize"/>'<para></para>
    /// See <see cref="AuthorizationPolicyEffect"/>
    /// </summary>
    [DefaultValue(AuthorizationPolicyEffect.Authorize)]
    [DataMember(Order = 2, Name = "effect"), JsonPropertyName("effect"), YamlMember(Alias = "effect")]
    public virtual string Effect { get; set; } = AuthorizationPolicyEffect.Authorize;

    /// <summary>
    /// Gets/sets the date and time the policy applies from.<para></para>
    /// When type is set to '<see cref="CloudEventAuthorizationRuleType.TimeOfDay"/>', represents the time of the day starting from which the policy applies. Dates are ignored.<para></para>
    /// When type is set to '<see cref="CloudEventAuthorizationRuleType.Temporary"/>', represents the date and time starting from which the policy applies.<para></para>
    /// Ignored when type is set to any other value
    /// </summary>
    [DataMember(Order = 3, Name = "from"), JsonPropertyName("from"), YamlMember(Alias = "from")]
    public virtual DateTimeOffset? From { get; set; }

    /// <summary>
    /// Gets/sets the date and time the policy applies until.<para></para>
    /// When type is set to '<see cref="CloudEventAuthorizationRuleType.TimeOfDay"/>', represents the time of the day until which the policy applies. Dates are ignored.<para></para>
    /// When type is set to '<see cref="CloudEventAuthorizationRuleType.Temporary"/>', represents the date and time until which the policy applies.<para></para>
    /// Ignored when type is set to any other value
    /// </summary>
    [DataMember(Order = 4, Name = "to"), JsonPropertyName("to"), YamlMember(Alias = "to")]
    public virtual DateTimeOffset? To { get; set; }

    /// <summary>
    /// Gets/sets the name of the required attribute<para></para>
    /// Required when type is set to '<see cref="CloudEventAuthorizationRuleType.Attribute"/>', otherwise ignored
    /// </summary>
    [DataMember(Order = 5, Name = "attributeName"), JsonPropertyName("attributeName"), YamlMember(Alias = "attributeName")]
    public virtual string? AttributeName { get; set; }

    /// <summary>
    /// Gets/sets the value of the required attribute. Supports regular expressions<para></para>
    /// Required when type is set to '<see cref="CloudEventAuthorizationRuleType.Attribute"/>', otherwise ignored
    /// </summary>
    [DataMember(Order = 6, Name = "attributeValue"), JsonPropertyName("attributeValue"), YamlMember(Alias = "attributeValue")]
    public virtual string? AttributeValue { get; set; }

    /// <summary>
    /// Gets/sets the maximum size of incoming cloud events<para></para>
    /// Required when type is set to '<see cref="CloudEventAuthorizationRuleType.Payload"/>', otherwise ignored
    /// </summary>
    [DataMember(Order = 7, Name = "maxSize"), JsonPropertyName("maxSize"), YamlMember(Alias = "maxSize")]
    public virtual long? MaxSize { get; set; }

}
