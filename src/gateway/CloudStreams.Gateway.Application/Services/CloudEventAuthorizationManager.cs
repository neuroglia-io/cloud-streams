// Copyright © 2024-Present The Cloud Streams Authors
//
// Licensed under the Apache License, Version 2.0 (the "License"),
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using CloudStreams.Core;
using CloudStreams.Core.Resources;
using Neuroglia.Serialization;

namespace CloudStreams.Gateway.Application.Services;

/// <summary>
/// Represents the default implementation of the <see cref="ICloudEventAuthorizationManager"/> interface
/// </summary>
/// <param name="serializer">The service used to serialize/deserialize objects to/from JSON</param>
public class CloudEventAuthorizationManager(IJsonSerializer serializer, TimeProvider? timeProvider = null)
    : ICloudEventAuthorizationManager
{

    /// <summary>
    /// Gets the service used to serialize/deserialize objects to/from JSON
    /// </summary>
    protected IJsonSerializer Serializer { get; } = serializer;

    /// <summary>
    /// Gets the service used to get the current time
    /// </summary>
    protected TimeProvider TimeProvider { get; } = timeProvider ?? TimeProvider.System;

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
                    // materialized once: counting over a lazy projection would evaluate every rule once per count
                    var results = policy.Rules.Select(r => this.Evaluate(e, r)).ToList();
                    var succeeded = results.Count(r => r);
                    var failed = results.Count - succeeded;
                    if (succeeded <= failed) return new((int)HttpStatusCode.Forbidden);
                    return new OperationResult((int)HttpStatusCode.OK);
                case RuleBasedDecisionStrategy.Minority:
                    if (!policy.Rules.Any(r => this.Evaluate(e, r))) return new((int)HttpStatusCode.Forbidden);
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
                if (!string.IsNullOrWhiteSpace(rule.AttributeValue) && !CloudStreamsRegex.IsMatch((value is Uri uri ? uri.OriginalString : value.ToString()) ?? string.Empty, rule.AttributeValue)) return mismatch;
                break;
            case CloudEventAuthorizationRuleType.Payload:
                var payloadSize = this.Serializer.SerializeToByteArray(e.Data)!.Length;
                if (payloadSize > rule.MaxSize) return mismatch;
                break;
            // time-based rules are evaluated against the time at which the gateway evaluates the event, which publishers cannot forge, rather than against the
            // event's optional, publisher-supplied 'time' attribute (see #143)
            case CloudEventAuthorizationRuleType.Temporary:
                var now = this.TimeProvider.GetUtcNow();
                if (rule.From.HasValue && now < rule.From.Value) return mismatch;
                if (rule.To.HasValue && now > rule.To.Value) return mismatch;
                break;
            case CloudEventAuthorizationRuleType.TimeOfDay:
                if (!IsWithinTimeOfDay(this.TimeProvider.GetUtcNow(), rule.From, rule.To)) return mismatch;
                break;
            default:
                throw new NotSupportedException($"The specified {nameof(CloudEventAuthorizationRuleType)} '{rule.Type}' is not supported");
        }
        return match;
    }

    /// <summary>
    /// Determines whether the specified date and time falls within the specified time of day range
    /// </summary>
    /// <param name="now">The date and time to check</param>
    /// <param name="from">The start of the range, whose offset is the time zone the range is expressed in, if any</param>
    /// <param name="to">The end of the range, if any</param>
    /// <returns>A boolean indicating whether or not the specified date and time falls within the range</returns>
    /// <remarks>A range whose end precedes its start spans midnight, e.g. from 22:00 to 06:00</remarks>
    protected static bool IsWithinTimeOfDay(DateTimeOffset now, DateTimeOffset? from, DateTimeOffset? to)
    {
        if (!from.HasValue && !to.HasValue) return true;
        var timeOfDay = now.ToOffset(from?.Offset ?? to!.Value.Offset).TimeOfDay;
        var start = from?.TimeOfDay ?? TimeSpan.Zero;
        var end = to?.TimeOfDay ?? TimeSpan.FromDays(1);
        return start <= end ? timeOfDay >= start && timeOfDay <= end : timeOfDay >= start || timeOfDay <= end;
    }

}
