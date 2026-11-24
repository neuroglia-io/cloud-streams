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

using CloudStreams.Gateway.Application.Services;

namespace CloudStreams.UnitTests.Cases.Gateway;

/// <summary>
/// Covers the rules and decision strategies of the <see cref="CloudEventAuthorizationManager"/> (#204)
/// </summary>
public class CloudEventAuthorizationManagerTests
{

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-15T23:30:00Z");

    static CloudEventAuthorizationManager Manager() => new(Neuroglia.Serialization.Json.JsonSerializer.Default, new FixedTimeProvider(Now));

    static CloudEvent Event(object? data = null, string type = "com.example.orders.created.v1") => new()
    {
        Id = "1",
        SpecVersion = CloudEventSpecVersion.V1.Version,
        Source = new("https://orders.example.com"),
        Type = type,
        Subject = "order-42",
        Data = data
    };

    static CloudEventAuthorizationRule Allow(string attribute, string? value = null) => new() { Type = CloudEventAuthorizationRuleType.Attribute, Effect = AuthorizationPolicyEffect.Authorize, AttributeName = attribute, AttributeValue = value };

    static CloudEventAuthorizationRule Deny(string attribute, string? value = null) => new() { Type = CloudEventAuthorizationRuleType.Attribute, Effect = AuthorizationPolicyEffect.Forbid, AttributeName = attribute, AttributeValue = value };

    static async Task<int> EvaluateAsync(CloudEvent e, string strategy, params CloudEventAuthorizationRule[] rules) =>
        (await Manager().EvaluateAsync(e, new() { DecisionStrategy = strategy, Rules = [.. rules] })).Status;

    [Fact]
    public async Task A_Policy_Without_Rules_Should_Authorize()
    {
        Assert.Equal(200, await EvaluateAsync(Event(), RuleBasedDecisionStrategy.Unanimous));
    }

    // ---------- attribute rules ----------

    [Theory]
    [InlineData("type", "^com\\.example\\.orders\\.", 200)]
    [InlineData("type", "^com\\.example\\.billing\\.", 403)]
    [InlineData("subject", null, 200)]
    [InlineData("missingattribute", null, 403)]
    public async Task An_Attribute_Rule_Should_Authorize_Only_Events_Whose_Attribute_Matches(string attribute, string? pattern, int expected)
    {
        Assert.Equal(expected, await EvaluateAsync(Event(), RuleBasedDecisionStrategy.Unanimous, Allow(attribute, pattern)));
    }

    [Theory]
    [InlineData("^com\\.example\\.orders\\.", 403)]
    [InlineData("^com\\.example\\.billing\\.", 200)]
    public async Task A_Forbidding_Attribute_Rule_Should_Invert_The_Decision(string pattern, int expected)
    {
        Assert.Equal(expected, await EvaluateAsync(Event(), RuleBasedDecisionStrategy.Unanimous, Deny("type", pattern)));
    }

    // ---------- payload rules ----------

    [Theory]
    [InlineData(1024, 200)]
    [InlineData(8, 403)]
    public async Task A_Payload_Rule_Should_Limit_The_Size_Of_The_Data(long maxSize, int expected)
    {
        // arrange
        var rule = new CloudEventAuthorizationRule() { Type = CloudEventAuthorizationRuleType.Payload, Effect = AuthorizationPolicyEffect.Authorize, MaxSize = maxSize };

        // act & assert
        Assert.Equal(expected, await EvaluateAsync(Event(new { orderId = 42, items = new[] { "a", "b" } }), RuleBasedDecisionStrategy.Unanimous, rule));
    }

    // ---------- time-based rules ----------

    [Theory]
    [InlineData("2026-06-01T00:00:00Z", "2026-07-01T00:00:00Z", 200)]
    [InlineData("2026-07-01T00:00:00Z", null, 403)]
    [InlineData(null, "2026-06-01T00:00:00Z", 403)]
    [InlineData(null, "2026-07-01T00:00:00Z", 200)]
    public async Task A_Temporary_Rule_Should_Only_Authorize_Within_Its_Window(string? from, string? to, int expected)
    {
        // arrange
        var rule = new CloudEventAuthorizationRule()
        {
            Type = CloudEventAuthorizationRuleType.Temporary,
            Effect = AuthorizationPolicyEffect.Authorize,
            From = from == null ? null : DateTimeOffset.Parse(from),
            To = to == null ? null : DateTimeOffset.Parse(to)
        };

        // act & assert
        Assert.Equal(expected, await EvaluateAsync(Event(), RuleBasedDecisionStrategy.Unanimous, rule));
    }

    [Theory]
    [InlineData("2026-01-01T22:00:00Z", "2026-01-01T06:00:00Z", 200)] // spans midnight, and it is 23:30
    [InlineData("2026-01-01T08:00:00Z", "2026-01-01T18:00:00Z", 403)] // office hours
    [InlineData("2026-01-01T23:00:00+02:00", "2026-01-01T23:59:00+02:00", 403)] // 23:30 UTC is 01:30 at +02:00
    [InlineData("2026-01-01T01:00:00+02:00", "2026-01-01T02:00:00+02:00", 200)]
    public async Task A_Time_Of_Day_Rule_Should_Compare_The_Time_Of_Day_In_The_Offset_Of_Its_Bounds(string from, string to, int expected)
    {
        // arrange
        var rule = new CloudEventAuthorizationRule() { Type = CloudEventAuthorizationRuleType.TimeOfDay, Effect = AuthorizationPolicyEffect.Authorize, From = DateTimeOffset.Parse(from), To = DateTimeOffset.Parse(to) };

        // act & assert
        Assert.Equal(expected, await EvaluateAsync(Event(), RuleBasedDecisionStrategy.Unanimous, rule));
    }

    // ---------- decision strategies ----------

    public static TheoryData<string, int, int, int> Strategies => new()
    {
        // strategy, rules that authorize, rules that forbid, expected status
        { RuleBasedDecisionStrategy.Unanimous, 3, 0, 200 },
        { RuleBasedDecisionStrategy.Unanimous, 2, 1, 403 },
        { RuleBasedDecisionStrategy.Consensus, 2, 1, 200 },
        { RuleBasedDecisionStrategy.Consensus, 1, 1, 403 },
        { RuleBasedDecisionStrategy.Consensus, 1, 2, 403 },
        { RuleBasedDecisionStrategy.Minority, 1, 2, 200 },
        { RuleBasedDecisionStrategy.Minority, 0, 3, 403 },
    };

    [Theory]
    [MemberData(nameof(Strategies))]
    public async Task Decision_Strategies_Should_Weigh_The_Rules_As_Documented(string strategy, int authorizing, int forbidding, int expected)
    {
        // arrange: rules on the 'type' attribute, which the event has, and on a missing attribute, which it does not
        var rules = Enumerable.Repeat(Allow("type"), authorizing).Concat(Enumerable.Repeat(Allow("missingattribute"), forbidding)).ToArray();

        // act & assert
        Assert.Equal(expected, await EvaluateAsync(Event(), strategy, rules));
    }

}
