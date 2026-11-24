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

using Json.Schema;
using BrokerResource = CloudStreams.Core.Resources.Broker;
using Duration = CloudStreams.Core.Resources.Duration;
using Neuroglia.Serialization.Yaml;
using System.Text.Json.Nodes;
using JsonSerializer = Neuroglia.Serialization.Json.JsonSerializer;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Guards against drift between the resource model and the embedded resource definitions (see #171)
/// </summary>
public class ResourceDefinitionTests
{

    static Subscription SampleSubscription() => new()
    {
        Metadata = new() { Name = "sample", Labels = new Dictionary<string, string>() { { "app", "tests" } } },
        Spec = new()
        {
            Partition = new(CloudEventPartitionType.ByCorrelationId, "correlation"),
            Filter = new() { Type = CloudEventFilterType.Attributes, Attributes = new Dictionary<string, string>() { { "type", "com\\.cloud-streams\\..*" } } },
            Mutation = new() { Type = CloudEventMutationType.Expression, Expression = new Dictionary<string, object>() { { "subject", "${ .subject }" } } },
            Stream = new() { Offset = 0 },
            Subscriber = new()
            {
                Uri = new("https://subscriber.cloud-streams.io/events"),
                RateLimit = 10,
                Timeout = Duration.FromSeconds(30),
                Ack = new() { Timeout = Duration.FromMinutes(5) },
                RetryPolicy = new()
                {
                    StatusCodes = [429, 503],
                    BackoffDuration = new() { Type = RetryBackoffDurationType.Exponential, Period = Duration.FromSeconds(1), Exponent = 2, MaxDelay = Duration.FromMinutes(10) },
                    MaxAttempts = 10,
                    CircuitBreaker = new(3, Duration.FromSeconds(30))
                }
            }
        },
        Status = new()
        {
            Phase = SubscriptionStatusPhase.Active,
            ObservedGeneration = 1,
            Stream = new() { AckedOffset = 42 },
            Subscriber = new()
            {
                State = SubscriberState.Reachable,
                Reason = "reason",
                PendingAck = new() { Offset = 42, Token = "token", ExpiresAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z") },
                LastAcknowledgedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z")
            }
        }
    };

    static BrokerResource SampleBroker() => new(new ResourceMetadata("sample"), new BrokerSpec()
    {
        Selector = new Dictionary<string, string>() { { "app", "tests" } },
        Dispatch = new()
        {
            Timeout = Duration.FromSeconds(100),
            RetryPolicy = new() { BackoffDuration = RetryBackoffDuration.Constant(Duration.FromSeconds(5)), MaxAttempts = 5 },
            Sequencing = new() { Strategy = CloudEventSequencingStrategy.Attribute, AttributeName = "sequence", AttributeConflictResolution = CloudEventAttributeConflictResolution.Overwrite }
        },
        Service = new(new Uri("http://broker.cloud-streams.io"), new(new HttpRequestConfiguration("GET", "/healthz"), Duration.FromSeconds(15)))
    })
    {
        Status = new() { HealthStatus = "healthy", LastHealthCheckAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"), ObservedGeneration = 1 }
    };

    static Gateway SampleGateway() => new(new ResourceMetadata("sample"), new GatewaySpec()
    {
        Authorization = new() { DecisionStrategy = RuleBasedDecisionStrategy.Unanimous, Rules = [new() { Type = CloudEventAuthorizationRuleType.Attribute, Effect = AuthorizationPolicyEffect.Authorize, AttributeName = "type", AttributeValue = ".*" }] },
        Validation = new() { SkipValidation = true, ValidationStrategy = CloudEventValidationStrategy.Fail, DataSchema = new() { Required = false, AutoGenerate = true } },
        SplitHorizon = new() { TrackingMode = HorizonTrackingMode.Chain, EvaluationMode = HorizonEvaluationMode.Contains, LoopAction = LoopAction.Reject, MaxChainLength = 3, AllowedOrigins = ["gateway-a"], ExcludedOrigins = ["gateway-b"] },
        Service = new(new Uri("http://gateway.cloud-streams.io"), new(new HttpRequestConfiguration("GET", "/healthz"), Duration.FromSeconds(15)))
    })
    {
        Status = new() { HealthStatus = "healthy", LastHealthCheckAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z") }
    };

    static JsonSchema SchemaOf(IResource resource) => resource switch
    {
        Subscription => new SubscriptionResourceDefinition().Spec.Versions.First().Schema.OpenAPIV3Schema!,
        BrokerResource => new BrokerResourceDefinition().Spec.Versions.First().Schema.OpenAPIV3Schema!,
        Gateway => new GatewayResourceDefinition().Spec.Versions.First().Schema.OpenAPIV3Schema!,
        _ => throw new NotSupportedException()
    };

    static void AssertValid(IResource resource, JsonNode node)
    {
        var result = SchemaOf(resource).Evaluate(node, new() { OutputFormat = OutputFormat.List });
        var errors = result.Details.Where(d => d.Errors != null).SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"));
        Assert.True(result.IsValid, $"The {resource.GetType().Name} is not valid against its definition:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}{Environment.NewLine}{node.ToJsonString()}");
    }

    public static TheoryData<string> Samples => ["subscription", "broker", "gateway"];

    static IResource Sample(string kind) => kind switch
    {
        "subscription" => SampleSubscription(),
        "broker" => SampleBroker(),
        "gateway" => SampleGateway(),
        _ => throw new NotSupportedException()
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void A_Fully_Populated_Resource_Should_Be_Valid_Against_Its_Definition(string kind)
    {
        // arrange
        var resource = Sample(kind);

        // act
        var node = JsonNode.Parse(JsonSerializer.Default.SerializeToText(resource, resource.GetType()))!;

        // assert
        AssertValid(resource, node);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void A_Resource_Should_Round_Trip_Through_Json_And_Yaml(string kind)
    {
        // arrange
        var resource = Sample(kind);
        var json = JsonSerializer.Default.SerializeToText(resource, resource.GetType());

        // act
        var fromJson = JsonSerializer.Default.Deserialize(json, resource.GetType())!;
        var yaml = YamlSerializer.Default.SerializeToText(resource, resource.GetType());
        var fromYaml = YamlSerializer.Default.Deserialize(yaml, resource.GetType())!;

        // assert
        Assert.Equal(json, JsonSerializer.Default.SerializeToText(fromJson, resource.GetType()));
        Assert.Equal(json, JsonSerializer.Default.SerializeToText(fromYaml, resource.GetType()));
    }

    [Fact]
    public void The_Sample_Subscription_Should_Deserialize_And_Be_Valid()
    {
        // arrange
        var yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "deployments", "docker-compose", "config", "subscriptions", "all.yaml"));

        // act
        var subscription = YamlSerializer.Default.Deserialize<Subscription>(yaml)!;

        // assert
        Assert.Equal(TimeSpan.FromSeconds(3), subscription.Spec.Subscriber.RetryPolicy!.BackoffDuration.Period.ToTimeSpan());
        Assert.Equal(TimeSpan.FromSeconds(5), subscription.Spec.Subscriber.RetryPolicy.CircuitBreaker!.BreakDuration.ToTimeSpan());
        AssertValid(subscription, JsonSerializer.Default.SerializeToNode(subscription)!);
    }

}
