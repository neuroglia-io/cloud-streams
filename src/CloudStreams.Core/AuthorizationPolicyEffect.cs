namespace CloudStreams.Core;

/// <summary>
/// Enumerates default authorization policy effects
/// </summary>
public static class AuthorizationPolicyEffect
{

    /// <summary>
    /// Indicates that the policy's effect is to grant authorization when it applies
    /// </summary>
    public const string Authorize = "authorize";
    
    /// <summary>
    /// Indicates that the policy's effect is to forbid authorization when it applies
    /// </summary>
    public const string Forbid = "forbid";

}
