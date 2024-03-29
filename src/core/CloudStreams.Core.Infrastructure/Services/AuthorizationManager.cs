﻿// Copyright © 2023-Present The Cloud Streams Authors
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

using System.Net;
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
    public virtual Task<OperationResult> EvaluateAsync(CloudEvent e, CloudEventAuthorizationPolicy policy, CancellationToken cancellationToken = default)
    {
        if (e == null) throw new ArgumentNullException(nameof(e));
        if (policy == null) throw new ArgumentNullException(nameof(policy));
        return Task.Run(() =>
        {
            if (policy.Rules == null || !policy.Rules.Any()) return OperationResult.Ok();
            switch (policy.DecisionStrategy)
            {
                case RuleBasedDecisionStrategy.Consensus:
                    var results = policy.Rules.Select(r => this.Evaluate(e, r));
                    var succeeded = results.Count(r => r);
                    var failed = results.Count(r => !r);
                    if (succeeded <= failed) return new((int)HttpStatusCode.Forbidden); // TODO: fix me: ApiResponse.Forbidden();
                    return OperationResult.Ok();
                case RuleBasedDecisionStrategy.Minority:
                    results = policy.Rules.Select(r => this.Evaluate(e, r));
                    succeeded = results.Count(r => r);
                    failed = results.Count(r => !r);
                    if (succeeded <= 0) return new((int)HttpStatusCode.Forbidden); // TODO: fix me: ApiResponse.Forbidden();
                    return OperationResult.Ok();
                case RuleBasedDecisionStrategy.Unanimous:
                    if (!policy.Rules.All(r => this.Evaluate(e, r))) return new((int)HttpStatusCode.Forbidden); // TODO: fix me: ApiResponse.Forbidden();
                    return OperationResult.Ok();
                default:
                    return new((int)HttpStatusCode.BadRequest); // TODO: fix me: ApiResponse.ValidationFailed($"The specified {nameof(RuleBasedDecisionStrategy)} '{policy.DecisionStrategy}' is not supported");
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
                var payloadSize = Encoding.UTF8.GetBytes(Hylo.Serializer.Json.Serialize(e.Data)).Length;
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