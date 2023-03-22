using System.Text;
using System.Text.RegularExpressions;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents the default implementation of the <see cref="IAuthorizationManager"/> interface
/// </summary>
public class AuthorizationManager
    : IAuthorizationManager
{

    /// <inheritdoc/>
    public virtual Task<Response> EvaluateAsync(CloudEvent e, CloudEventAuthorizationPolicy policy, CancellationToken cancellationToken = default)
    {
        if (e == null) throw new ArgumentNullException(nameof(e));
        if (policy == null) throw new ArgumentNullException(nameof(policy));
        return Task.Run(() =>
        {
            if (policy.Rules == null || !policy.Rules.Any()) return Response.Ok();
            switch (policy.DecisionStrategy)
            {
                case RuleBasedDecisionStrategy.Consensus:
                    var results = policy.Rules.Select(r => this.Evaluate(e, r));
                    var succeeded = results.Count(r => r);
                    var failed = results.Count(r => !r);
                    if (succeeded <= failed) return Response.Forbidden();
                    return Response.Ok();
                case RuleBasedDecisionStrategy.Minority:
                    results = policy.Rules.Select(r => this.Evaluate(e, r));
                    succeeded = results.Count(r => r);
                    failed = results.Count(r => !r);
                    if (succeeded <= 0) return Response.Forbidden();
                    return Response.Ok();
                case RuleBasedDecisionStrategy.Unanimous:
                    if (!policy.Rules.All(r => this.Evaluate(e, r))) return Response.Forbidden();
                    return Response.Ok();
                default:
                    return Response.ValidationFailed($"The specified {nameof(RuleBasedDecisionStrategy)} '{policy.DecisionStrategy}' is not supported");
            }
        });
    }

    /// <summary>
    /// Evaluates a <see cref="CloudEvent"/> against the specified <see cref="CloudEventAuthorizationRule"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to evaluate</param>
    /// <param name="rule">The <see cref="CloudEventAuthorizationRule"/> against which to evaluate the <see cref="CloudEvent"/></param>
    /// <returns>A boolean indicating whether or not the <see cref="CloudEvent"/> is authorized by the specified <see cref="CloudEventAuthorizationRule"/></returns>
    /// <exception cref="ArgumentNullException"></exception>
    protected virtual bool Evaluate(CloudEvent e, CloudEventAuthorizationRule rule)
    {
        if (e == null) throw new ArgumentNullException(nameof(e));
        if (rule == null) throw new ArgumentNullException(nameof(rule));
        var match = rule.Effect switch
        {
            AuthorizationPolicyEffect.Forbid => false,
            AuthorizationPolicyEffect.Authorize => true,
            _ => throw new NotSupportedException($"The specified {nameof(AuthorizationPolicyEffect)} '{rule.Effect}' is not supported")
        };
        var mismatch = !match;
        switch (rule.Type)
        {
            case CloudEventAuthorizationRuleType.Attribute:
                if(!e.TryGetAttribute(rule.AttributeName!, out var value) || value == null) return mismatch;
                if (!string.IsNullOrWhiteSpace(rule.AttributeValue) && !Regex.IsMatch(value.ToString()!, rule.AttributeValue)) return mismatch;
                break;
            case CloudEventAuthorizationRuleType.Payload:
                var payloadSize = Encoding.UTF8.GetBytes(Serializer.Json.Serialize(e.Data)).Length;
                if (payloadSize > rule.MaxSize) return mismatch;
                break;
            case CloudEventAuthorizationRuleType.Temporary:
                if (e.Time < rule.From) return mismatch;
                if (e.Time > rule.To) return mismatch;
                break;
            case CloudEventAuthorizationRuleType.TimeOfDay:
                if (e.Time!.Value.TimeOfDay < rule.From!.Value.TimeOfDay) return mismatch;
                if (e.Time!.Value.TimeOfDay > rule.To!.Value.TimeOfDay) return mismatch;
                break;
            default:
                throw new NotSupportedException($"The specified {nameof(CloudEventAuthorizationRuleType)} '{rule.Type}' is not supported");
        }
        return match;
    }

}