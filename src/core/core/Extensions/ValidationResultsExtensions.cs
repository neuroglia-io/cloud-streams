namespace CloudStreams;

/// <summary>
/// Defines extensions for <see cref="ValidationResults"/>
/// </summary>
public static class ValidationResultsExtensions
{

    /// <summary>
    /// Generates a new <see cref="ValidationResults"/> error message
    /// </summary>
    /// <param name="validationResults">The <see cref="ValidationResults"/> to generate the error message of</param>
    /// <returns>The <see cref="ValidationResults"/>'s error message</returns>
    public static string? GetErrorMessage(this ValidationResults validationResults)
    {
        if (validationResults.IsValid) return null;
        else if (validationResults.NestedResults.Count > 0) return string.Join(Environment.NewLine, validationResults.NestedResults.Where(r => !r.IsValid).Select(r => r.GetErrorMessages()));
        else return validationResults.Message;
    }

    /// <summary>
    /// Generates a new <see cref="ValidationResults"/> error message
    /// </summary>
    /// <param name="validationResults">The <see cref="ValidationResults"/> to generate the error message of</param>
    /// <returns>The <see cref="ValidationResults"/>'s error message</returns>
    public static IEnumerable<string> GetErrorMessages(this ValidationResults validationResults)
    {
        if (validationResults.IsValid) yield break;
        if (!validationResults.HasNestedResults)
        {
            yield return validationResults.GetErrorMessage()!;
            yield break;
        }
        foreach (var nestedValidationResults in validationResults.NestedResults.Where(r => !r.IsValid))
        {
            yield return nestedValidationResults.GetErrorMessage()!;
        }
    }

}
