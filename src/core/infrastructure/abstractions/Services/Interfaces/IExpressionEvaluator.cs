namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Defines the fundamentals of a service used to evaluate runtime expressions
/// </summary>
public interface IExpressionEvaluator
{

    /// <summary>
    /// Evaluates the specified runtime expression
    /// </summary>
    /// <param name="expression">The runtime expression to evaluate</param>
    /// <param name="input">The data to evaluate the runtime expression against</param>
    /// <param name="arguments">A key/value mapping of the arguments used during evaluation, if any</param>
    /// <param name="expectedType">The expected type of the evaluation's result</param>
    /// <returns>The evaluation's result</returns>
    object? Evaluate(string expression, object input, IDictionary<string, object>? arguments = null, Type? expectedType = null);

}