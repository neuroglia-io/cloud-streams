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

using CloudStreams.Core.Application.Validation;
using BrokerResource = CloudStreams.Core.Resources.Broker;
using Duration = CloudStreams.Core.Resources.Duration;
using GatewayResource = CloudStreams.Core.Resources.Gateway;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the validation of resource specs on admission (#170)
/// </summary>
public class ResourceValidationTests
{

    static Subscription Subscription(Action<SubscriptionSpec>? configure = null)
    {
        var subscription = new Subscription()
        {
            Metadata = new() { Name = "orders" },
            Spec = new()
            {
                Subscriber = new() { Uri = new("https://subscriber.example.com/events"), RateLimit = 10 },
                Partition = new(CloudEventPartitionType.BySource, "https://orders.example.com"),
                Filter = new() { Type = CloudEventFilterType.Attributes, Attributes = new Dictionary<string, string>() { { "type", "^order\\." }, { "subject", "${ . != null }" } } },
                Stream = new() { Offset = 0 }
            }
        };
        configure?.Invoke(subscription.Spec);
        return subscription;
    }

    [Fact]
    public void A_Valid_Subscription_Should_Pass()
    {
        Assert.True(new SubscriptionValidator().Validate(Subscription()).IsValid);
    }

    public static TheoryData<string, Action<SubscriptionSpec>> InvalidSubscriptions => new()
    {
        { "spec.subscriber.uri", s => s.Subscriber.Uri = new("ftp://subscriber.example.com") },
        { "spec.subscriber.rateLimit", s => s.Subscriber.RateLimit = 0 },
        { "spec.subscriber.retryPolicy", s => s.Subscriber.RetryPolicy = new() { BackoffDuration = new() { Type = RetryBackoffDurationType.Exponential, Period = Duration.FromSeconds(1) } } },
        { "spec.subscriber.retryPolicy", s => s.Subscriber.RetryPolicy = new() { BackoffDuration = RetryBackoffDuration.Constant(Duration.FromSeconds(1)), MaxAttempts = -2 } },
        { "spec.subscriber.retryPolicy", s => s.Subscriber.RetryPolicy = new() { BackoffDuration = RetryBackoffDuration.Constant(Duration.FromSeconds(1)), StatusCodes = [42] } },
        { "spec.filter.attributes", s => s.Filter!.Attributes = new Dictionary<string, string>() { { "type", "order.(created" } } },
        { "spec.filter.expression", s => s.Filter = new() { Type = CloudEventFilterType.Expression } },
        { "spec.mutation.webhook", s => s.Mutation = new() { Type = CloudEventMutationType.Webhook } },
        { "spec.partition.id", s => s.Partition = new() { Type = CloudEventPartitionType.ByType, Id = "" } },
        { "spec.stream.offset", s => s.Stream = new() { Offset = -2 } },
    };

    [Theory]
    [MemberData(nameof(InvalidSubscriptions))]
    public void An_Invalid_Subscription_Should_Fail(string property, Action<SubscriptionSpec> invalidate)
    {
        // act
        var result = new SubscriptionValidator().Validate(Subscription(invalidate));

        // assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.StartsWith(property, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_Broker_With_An_Incomplete_Backoff_Should_Fail()
    {
        // arrange
        var broker = new BrokerResource(new ResourceMetadata("broker"), new BrokerSpec()
        {
            Dispatch = new() { RetryPolicy = new() { BackoffDuration = new() { Type = RetryBackoffDurationType.Exponential, Period = Duration.FromSeconds(1) } } }
        });

        // act & assert
        Assert.False(new BrokerValidator().Validate(broker).IsValid);
    }

    [Fact]
    public void A_Gateway_With_Invalid_Rules_And_Patterns_Should_Fail()
    {
        // arrange
        var gateway = new GatewayResource(new ResourceMetadata("gateway"), new GatewaySpec()
        {
            Authorization = new() { DecisionStrategy = RuleBasedDecisionStrategy.Unanimous, Rules = [new() { Type = CloudEventAuthorizationRuleType.Attribute, Effect = AuthorizationPolicyEffect.Authorize, AttributeName = "type", AttributeValue = "(" }] },
            Events = [new("*", "order.(")],
            SplitHorizon = new() { OriginAttribute = "Origin-Header" }
        });

        // act
        var result = new GatewayValidator().Validate(gateway);

        // assert
        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void An_Empty_Gateway_Should_Pass()
    {
        Assert.True(new GatewayValidator().Validate(new GatewayResource(new ResourceMetadata("gateway"), new GatewaySpec())).IsValid);
    }

    [Fact]
    public void A_Gateway_With_A_Valid_Ingestion_Policy_Should_Pass()
    {
        var gateway = new GatewayResource(new ResourceMetadata("gateway"), new GatewaySpec()
        {
            Ingestion = new()
            {
                MaxRequestBodySize = 1024,
                MaxBatchSize = 100,
                Deduplication = new() { Window = Duration.FromMinutes(10) },
                RateLimits = [new() { Partition = CloudEventRateLimitPartitioning.Source, Limit = 100, Period = Duration.FromSeconds(1) }]
            }
        });
        Assert.True(new GatewayValidator().Validate(gateway).IsValid);
    }

    [Fact]
    public void A_Gateway_With_An_Invalid_Ingestion_Policy_Should_Fail()
    {
        // arrange
        var gateway = new GatewayResource(new ResourceMetadata("gateway"), new GatewaySpec()
        {
            Ingestion = new()
            {
                MaxRequestBodySize = 0,
                MaxBatchSize = -1,
                Deduplication = new() { Window = Duration.Zero },
                RateLimits = [new() { Partition = "tenant", Limit = 0, Period = Duration.Zero }]
            }
        });

        // act
        var result = new GatewayValidator().Validate(gateway);

        // assert
        Assert.False(result.IsValid);
        Assert.Equal(6, result.Errors.Count);
    }

    static AdmissionReviewRequest Request(IResource resource, Operation operation = Operation.Create, string? subResource = null) =>
        new(Guid.NewGuid().ToString(), operation, new ResourceReference(new ResourceDefinitionReference("cloud-streams.io", "v1", "subscriptions"), "orders", null), subResource!, new Patch(PatchType.JsonMergePatch, new { }), resource, resource, new Neuroglia.Security.UserInfo("tests", "tests", []), false);

    [Fact]
    public async Task Admission_Should_Reject_An_Invalid_Subscription_With_Its_Errors()
    {
        // act
        var response = await new CloudStreamsResourceValidator(Neuroglia.Serialization.Json.JsonSerializer.Default, []).ValidateAsync(Request(Subscription(s => s.Subscriber.RateLimit = 0)));

        // assert
        Assert.False(response.Allowed);
        Assert.Contains(response.Problem!.Errors!, e => e.Key == "spec.subscriber.rateLimit");
    }

    [Fact]
    public async Task Admission_Should_Accept_A_Valid_Subscription_And_Ignore_Status_Updates()
    {
        // arrange
        var validator = new CloudStreamsResourceValidator(Neuroglia.Serialization.Json.JsonSerializer.Default, []);

        // act & assert
        Assert.True((await validator.ValidateAsync(Request(Subscription()))).Allowed);
        Assert.True((await validator.ValidateAsync(Request(Subscription(s => s.Subscriber.RateLimit = 0), Operation.Patch, "status"))).Allowed);
        Assert.False(validator.AppliesTo(Operation.Delete, "cloud-streams.io", "v1", "subscriptions"));
        Assert.True(validator.AppliesTo(Operation.Patch, "cloud-streams.io", "v1", "gateways"));
    }

}
