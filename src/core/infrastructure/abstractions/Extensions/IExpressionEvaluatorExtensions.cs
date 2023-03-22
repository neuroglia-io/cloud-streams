using CloudStreams.Core.Infrastructure.Services;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="IExpressionEvaluator"/>s
/// </summary>
public static class IExpressionEvaluatorExtensions
{

    /// <summary>
    /// Evaluates the specified runtime expression
    /// </summary>
    /// <typeparam name="TResult">The expected type of the evaluation's result</typeparam>
    /// <param name="evaluator">The extended <see cref="IExpressionEvaluator"/></param>
    /// <param name="expression">The runtime expression to evaluate</param>
    /// <param name="input">The data to evaluate the runtime expression against</param>
    /// <param name="arguments">A key/value mapping of the arguments used during evaluation, if any</param>
    /// <returns>The evaluation's result</returns>
    public static TResult? Evaluate<TResult>(this IExpressionEvaluator evaluator, string expression, object input, IDictionary<string, object>? arguments = null)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentNullException(nameof(expression));
        if (input == null) throw new ArgumentNullException(nameof(input));
        return (TResult?)evaluator.Evaluate(expression, input, arguments, typeof(TResult));
    }

    /// <summary>
    /// Evaluates the specified runtime expression based condition
    /// </summary>
    /// <param name="evaluator">The extended <see cref="IExpressionEvaluator"/></param>
    /// <param name="expression">The runtime expression to evaluate</param>
    /// <param name="input">The data to evaluate the runtime expression against</param>
    /// <param name="arguments">A key/value mapping of the arguments used during evaluation, if any</param>
    /// <returns>The evaluation's result</returns>
    public static bool EvaluateCondition(this IExpressionEvaluator evaluator, string expression, object input, IDictionary<string, object>? arguments = null)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentNullException(nameof(expression));
        if (input == null) throw new ArgumentNullException(nameof(input));
        return (bool?)evaluator.Evaluate(expression, input, arguments, typeof(bool)) == true;
    }

    /// <summary>
    /// Mutates the specified object by resolving all the runtime expressions it declares
    /// </summary>
    /// <param name="evaluator">The extended <see cref="IExpressionEvaluator"/></param>
    /// <param name="mutation">The mutation to perform, that is an object that declares - at any depth - runtime expressions to resolve against the specified data and arguments</param>
    /// <param name="input">The data to evaluate the runtime expression against</param>
    /// <param name="arguments">A key/value mapping of the arguments used during evaluation, if any</param>
    /// <param name="expectedType">The expected type of the mutated object</param>
    /// <returns>The mutated object</returns>
    public static object? Mutate(this IExpressionEvaluator evaluator, object mutation, object input, IDictionary<string, object>? arguments = null, Type? expectedType = null)
    {
        if (mutation == null) throw new ArgumentNullException(nameof(mutation));
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (expectedType == null) expectedType= typeof(object);
        if (mutation is string mutationExpression) return evaluator.Evaluate(mutationExpression, input, arguments, expectedType);
        else if(mutation is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String) return evaluator.Evaluate(Serializer.Json.Deserialize<string>(jsonElement)!, input, arguments, expectedType);
        var json = Serializer.Json.Serialize(mutation);
        foreach (Match match in Regex.Matches(json, @"""\$\{.+?\}""", RegexOptions.Compiled))
        {
            var expression = match.Value[3..^2].Trim();
            var evaluationResult = evaluator.Evaluate(expression, input, arguments);
            var value = Serializer.Json.Serialize(evaluationResult);
            if (string.IsNullOrEmpty(value)) value = "null";
            json = json.Replace(match.Value, value);
        }
        return Serializer.Json.Deserialize(json, expectedType);
    }

    /// <summary>
    /// Mutates the specified object by resolving all the runtime expressions it declares
    /// </summary>
    /// <typeparam name="TResult">The expected type of the mutated object</typeparam>
    /// <param name="evaluator">The extended <see cref="IExpressionEvaluator"/></param>
    /// <param name="mutation">The mutation to perform, that is an object that declares - at any depth - runtime expressions to resolve against the specified data and arguments</param>
    /// <param name="input">The data to evaluate the runtime expression against</param>
    /// <param name="arguments">A key/value mapping of the arguments used during evaluation, if any</param>
    /// <returns>The mutated object</returns>
    public static TResult? Mutate<TResult>(this IExpressionEvaluator evaluator, object mutation, object input, IDictionary<string, object>? arguments = null)
    {
        return (TResult?)evaluator.Mutate(mutation, input, arguments, typeof(TResult));
    }

}