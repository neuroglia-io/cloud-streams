namespace CloudStreams.Core;

/// <summary>
/// Enumerates default cloud event authorization rule types
/// </summary>
public static class CloudEventAuthorizationRuleType
{

    /// <summary>
    /// Indicates a policy that performs checks on cloud event context attributes
    /// </summary>
    public const string Attribute = "attribute";
    /// <summary>
    /// Indicates a policy that performs checks on cloud event payloads
    /// </summary>
    public const string Payload = "payload";
    /// <summary>
    /// Indicates a policy that grants or refuses access based on the time of day
    /// </summary>
    public const string TimeOfDay = "timeOfDay";
    /// <summary>
    /// Indicates a policy that grants or refuses access over a given period of time
    /// </summary>
    public const string Temporary = "temporary";

}