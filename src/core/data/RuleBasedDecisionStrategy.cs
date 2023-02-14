namespace CloudStreams;

/// <summary>
/// Enumerates default rule-based decision strategies
/// </summary>
public static class RuleBasedDecisionStrategy
{

    /// <summary>
    /// Indicates that a majority of rules should apply for the decision to be taken
    /// </summary>
    public const string Consensus = "consensus";
    /// <summary>
    /// Indicates that at least one rule should apply for the decision to be taken
    /// </summary>
    public const string Minority = "minority";
    /// <summary>
    /// Indicates that all rules should apply for the decision to be taken
    /// </summary>
    public const string Unanimous = "unanimous";

}