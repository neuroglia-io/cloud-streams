namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="EvaluationResults"/>
/// </summary>
public static class EvaluationResultsExtensions
{

    /// <summary>
    /// Converts the <see cref="EvaluationResults"/> to an <see cref="IEnumerable{T}"/> of errors, if any
    /// </summary>
    /// <param name="evaluationResults">The <see cref="EvaluationResults"/> to convert</param>
    /// <returns>A new <see cref="IEnumerable{T}"/> containing the <see cref="EvaluationResults"/>'s errors, if any</returns>
    public static IEnumerable<KeyValuePair<string, string[]>>? ToErrorList(this EvaluationResults evaluationResults)
    {
        if (evaluationResults.IsValid) return null;
        var errors = new List<KeyValuePair<string, string[]>>();
        if (evaluationResults.Errors?.Any() == true) errors = evaluationResults.Errors.Select(e =>new KeyValuePair<string, string[]>(evaluationResults.InstanceLocation.ToString(), new string[] { e.Value })).ToList();
        if (!evaluationResults.Details.Any()) return null;
        foreach(var detail in evaluationResults.Details)
        {
            var childErrors = detail.ToErrorList();
            if (childErrors == null) continue;
            foreach (var error in childErrors)
            {
                errors.Add(error);
            }
        }
        return errors;
    }

}
