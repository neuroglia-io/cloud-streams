using CloudStreams.Core;
using CloudStreams.Core.Resources;
using Neuroglia.Serialization;

namespace CloudStreams.Gateway.Services;

/// <summary>
/// Represents the default implementation of the <see cref="ICloudEventAuthorizationManager"/> interface
/// </summary>
/// <param name="serializer">The service used to serialize/deserialize objects to/from JSON</param>
public class CloudEventAuthorizationManager(IJsonSerializer serializer)
    : ICloudEventAuthorizationManager
{

    /// <summary>
    /// Gets the service used to serialize/deserialize objects to/from JSON
    /// </summary>
    protected IJsonSerializer Serializer { get; } = serializer;

    /// <inheritdoc/>
    public virtual Task<OperationResult> EvaluateAsync(CloudEvent e, CloudEventAuthorizationPolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(policy);
        return Task.Run(() =>
        {
            if (policy.Rules == null || policy.Rules.Count == 0) return new OperationResult((int)HttpStatusCode.OK);
            switch (policy.DecisionStrategy)
            {
                case RuleBasedDecisionStrategy.Consensus:
                    var results = policy.Rules.Select(r => this.Evaluate(e, r));
                    var succeeded = results.Count(r => r);
                    var failed = results.Count(r => !r);
                    if (succeeded <= failed) return new((int)HttpStatusCode.Forbidden);
                    return new OperationResult((int)HttpStatusCode.OK);
                case RuleBasedDecisionStrategy.Minority:
                    results = policy.Rules.Select(r => this.Evaluate(e, r));
                    succeeded = results.Count(r => r);
                    failed = results.Count(r => !r);
                    if (succeeded <= 0) return new((int)HttpStatusCode.Forbidden);
                    return new OperationResult((int)HttpStatusCode.OK);
                case RuleBasedDecisionStrategy.Unanimous:
                    if (!policy.Rules.All(r => this.Evaluate(e, r))) return new((int)HttpStatusCode.Forbidden);
                    return new OperationResult((int)HttpStatusCode.OK);
                default:
                    return new OperationResult((int)HttpStatusCode.BadRequest);
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
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(rule);
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
                var payloadSize = this.Serializer.SerializeToByteArray(e.Data)!.Length;
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
