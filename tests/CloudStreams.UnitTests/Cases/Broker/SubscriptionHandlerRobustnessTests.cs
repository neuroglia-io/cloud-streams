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

using FluentValidation;
using Neuroglia.Data.Expressions.Services;
using System.Diagnostics;
using System.Text;

namespace CloudStreams.UnitTests.Cases.Broker;

/// <summary>
/// Covers the fixes of the broker delivery epic (#151): filtering, mutation, validation, rate limiting, fault reporting and disposal
/// </summary>
public class SubscriptionHandlerRobustnessTests
{

    static readonly Uri WebhookUri = new("http://localhost:9999/mutate");

    static RetryBackoffDuration NoBackoff => RetryBackoffDuration.Constant(Duration.FromSeconds(0));

    static IExpressionEvaluator EvaluatorReturning(object? result)
    {
        var evaluator = Substitute.For<IExpressionEvaluator>();
        evaluator.EvaluateAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<IDictionary<string, object>?>(), Arg.Any<Type?>(), Arg.Any<CancellationToken>()).Returns(result);
        return evaluator;
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task Attribute_Filters_Whose_Value_Is_A_Runtime_Expression_Should_Be_Evaluated(bool condition, int expectedDeliveries)
    {
        // arrange
        var evaluator = EvaluatorReturning(condition);
        await using var context = await BrokerTestContext.CreateAsync(expressionEvaluator: evaluator,
            configureSubscription: s => s.Spec.Filter = new() { Type = CloudEventFilterType.Attributes, Attributes = new Dictionary<string, string>() { { CloudEventAttributes.Type, "${ . == \"com.cloud-streams.test.v1\" }" } } });

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41));

        // assert
        Assert.Equal(expectedDeliveries, context.Http.AttemptCount);
        await evaluator.Received().EvaluateAsync("${ . == \"com.cloud-streams.test.v1\" }", "com.cloud-streams.test.v1", Arg.Any<IDictionary<string, object>?>(), Arg.Any<Type?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("com\\.cloud-streams\\..*", 1)]
    [InlineData("^org\\.", 0)]
    public async Task Attribute_Filters_Whose_Value_Is_Not_A_Runtime_Expression_Should_Be_Matched_As_Regular_Expressions(string pattern, int expectedDeliveries)
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(
            configureSubscription: s => s.Spec.Filter = new() { Type = CloudEventFilterType.Attributes, Attributes = new Dictionary<string, string>() { { CloudEventAttributes.Type, pattern } } });

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41));

        // assert
        Assert.Equal(expectedDeliveries, context.Http.AttemptCount);
    }

    [Fact]
    public async Task Mutated_Events_Should_Be_Validated_Once_Per_Validator()
    {
        // arrange
        var validations = 0;
        var validator = new InlineValidator<CloudEvent>();
        validator.RuleFor(e => e.Id).Must(_ => { Interlocked.Increment(ref validations); return true; });
        var evaluator = Substitute.For<IExpressionEvaluator>();
        evaluator.EvaluateAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<IDictionary<string, object>?>(), Arg.Any<Type?>(), Arg.Any<CancellationToken>()).Returns(call => call.ArgAt<object>(1));
        await using var context = await BrokerTestContext.CreateAsync(expressionEvaluator: evaluator, cloudEventValidators: [validator],
            configureSubscription: s => s.Spec.Mutation = new() { Type = CloudEventMutationType.Expression, Expression = "${ . }" });

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41));

        // assert
        Assert.Equal(1, validations);
        Assert.Equal(1, context.Http.AttemptCount);
    }

    [Fact]
    public async Task Invalid_Mutated_Events_Should_Fault_The_Subscription_With_The_Validation_Errors()
    {
        // arrange
        var validator = new InlineValidator<CloudEvent>();
        validator.RuleFor(e => e.Subject).NotEmpty().WithMessage("subject is required");
        var evaluator = Substitute.For<IExpressionEvaluator>();
        evaluator.EvaluateAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<IDictionary<string, object>?>(), Arg.Any<Type?>(), Arg.Any<CancellationToken>()).Returns(call => call.ArgAt<object>(1));
        await using var context = await BrokerTestContext.CreateAsync(expressionEvaluator: evaluator, cloudEventValidators: [validator],
            configureSubscription: s => s.Spec.Mutation = new() { Type = CloudEventMutationType.Expression, Expression = "${ . }" });

        // act
        await context.Handler.HandleCloudEventAsync(BrokerTestContext.CreateRecord(41));

        // assert
        Assert.Equal(0, context.Http.AttemptCount);
        var fault = Assert.Single(context.Patches.ValuesWrittenTo("/status/stream/fault"));
        Assert.Contains("subject is required", fault.ToJsonString());
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("application/cloudevents+json")]
    public async Task Webhook_Mutations_Should_Accept_Both_Json_Media_Types(string mediaType)
    {
        // arrange
        var mutated = new CloudEvent() { Id = "mutated", Source = new("https://cloud-streams.io/tests"), Type = "com.cloud-streams.test.mutated.v1", SpecVersion = CloudEventSpecVersion.V1.Version };
        var json = Neuroglia.Serialization.Json.JsonSerializer.Default.SerializeToText(mutated);
        var http = new StubHttpMessageHandler((request, _, _) => Task.FromResult(request.RequestUri == WebhookUri
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, mediaType) }
            : new HttpResponseMessage(HttpStatusCode.OK)));
        await using var context = await BrokerTestContext.CreateAsync(http, serializer: Neuroglia.Serialization.Json.JsonSerializer.Default,
            configureSubscription: s => s.Spec.Mutation = new() { Type = CloudEventMutationType.Webhook, Webhook = new() { ServiceUri = WebhookUri } });

        // act
        await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41));

        // assert
        Assert.Equal([WebhookUri, context.Subscription.Spec.Subscriber.Uri], context.Http.Attempts.Select(a => a.RequestUri));
        Assert.Contains(42ul, context.Patches.OffsetsWrittenTo());
    }

    [Fact]
    public async Task Webhook_Mutations_Should_Be_Bound_By_The_Dispatch_Timeout_And_Retried()
    {
        // arrange
        var http = new StubHttpMessageHandler(async (request, _, cancellationToken) =>
        {
            if (request.RequestUri == WebhookUri) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await using var context = await BrokerTestContext.CreateAsync(http, serializer: Neuroglia.Serialization.Json.JsonSerializer.Default,
            configureSubscription: s =>
            {
                s.Spec.Mutation = new() { Type = CloudEventMutationType.Webhook, Webhook = new() { ServiceUri = WebhookUri } };
                s.Spec.Subscriber.Timeout = Duration.FromMilliseconds(100);
                s.Spec.Subscriber.RetryPolicy = new() { MaxAttempts = 1, BackoffDuration = NoBackoff };
            });

        // act
        var dispatch = context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41));
        var completed = await Task.WhenAny(dispatch, Task.Delay(TimeSpan.FromSeconds(10)));

        // assert
        Assert.Same(dispatch, completed);
        Assert.Equal(2, context.Http.Attempts.Count(a => a.RequestUri == WebhookUri));
        Assert.Equal(2, context.Metrics.TotalDeliveryFailures);
        Assert.Contains(context.Patches.ValuesWrittenTo("/status/stream/fault"), fault => fault.ToJsonString().Contains("mutation webhook"));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    public async Task A_Rate_Limit_That_Is_Not_Strictly_Positive_Should_Be_Ignored(double rateLimit)
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(configureSubscription: s => s.Spec.Subscriber.RateLimit = rateLimit);

        // act
        await context.Handler.HandleCloudEventAsync(BrokerTestContext.CreateRecord(41));
        await context.Handler.HandleCloudEventAsync(BrokerTestContext.CreateRecord(42));

        // assert
        Assert.Equal(2, context.Http.AttemptCount);
        Assert.False(context.Patches.Touched("/status/stream/fault"), context.Patches.Describe());
    }

    [Fact]
    public async Task The_Rate_Limit_Should_Space_Deliveries_By_Its_Inverse()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(configureSubscription: s => s.Spec.Subscriber.RateLimit = 10);

        // act
        for (ulong offset = 41; offset < 44; offset++) await context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(offset));

        // assert
        Assert.Equal(3, context.Http.AttemptCount);
        Assert.All(context.Http.IntervalsBetweenAttempts.Skip(1), interval => Assert.True(interval >= TimeSpan.FromMilliseconds(90), $"Deliveries should be at least 100ms apart, got {interval}"));
    }

    [Fact]
    public async Task Reporting_A_Fault_Should_Never_Throw_When_The_Subscription_No_Longer_Exists()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(failPatch: _ => new ProblemDetailsException(new(ProblemTypes.NotFound, "Not Found", (int)HttpStatusCode.NotFound)));

        // act
        var exception = await Record.ExceptionAsync(() => context.Handler.ReportErrorAsync(new InvalidOperationException("boom")));

        // assert
        Assert.Null(exception);
        Assert.Equal(1, context.Patches.Count);
    }

    [Fact]
    public async Task Reporting_A_Fault_Should_Be_Retried_When_Recording_It_Fails()
    {
        // arrange
        var failures = 0;
        await using var context = await BrokerTestContext.CreateAsync(failPatch: _ => Interlocked.Increment(ref failures) == 1 ? new InvalidOperationException("redis is down") : null);

        // act
        var exception = await Record.ExceptionAsync(() => context.Handler.ReportErrorAsync(new InvalidOperationException("boom")));

        // assert
        Assert.Null(exception);
        Assert.Equal(2, context.Patches.Count);
    }

    [Fact]
    public async Task A_Disposed_Handler_Should_Stop_Observing_Subscription_Updates()
    {
        // arrange
        var context = await BrokerTestContext.CreateAsync();
        Assert.True(context.SubscriptionEvents.HasObservers);

        // act
        await context.Handler.DisposeAsync();
        var updated = context.Subscription.Clone()!;
        updated.Metadata.Generation = 2;
        updated.Spec.Stream = new() { Offset = 0 };
        context.PublishSubscriptionUpdate(updated);
        await Task.Delay(200);

        // assert
        Assert.False(context.SubscriptionEvents.HasObservers);
        Assert.Equal(0, context.Http.AttemptCount);
        await context.DisposeAsync();
    }

    [Fact]
    public async Task A_Disposed_Handler_Should_Stop_Catching_Up()
    {
        // arrange
        var http = new StubHttpMessageHandler(async (_, _, cancellationToken) =>
        {
            await Task.Delay(20, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var context = await BrokerTestContext.CreateAsync(http, streamLength: 1000, configureSubscription: s => s.Spec.Stream = new() { Offset = 0 });
        _ = context.Handler.CatchUpMissedEventsAsync();
        await Task.Delay(100);

        // act
        var stopwatch = Stopwatch.StartNew();
        await context.Handler.DisposeAsync();
        var attemptsAtDisposal = context.Http.AttemptCount;
        await Task.Delay(200);

        // assert
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Disposal should not wait for the catch-up to complete, took {stopwatch.Elapsed}");
        Assert.InRange(context.Http.AttemptCount, attemptsAtDisposal, attemptsAtDisposal + 1);
        await context.DisposeAsync();
    }

    [Fact]
    public async Task A_New_Subscription_Should_Not_Receive_Events_Published_Before_It_Was_Created()
    {
        // arrange & act
        await using var context = await BrokerTestContext.CreateAsync(streamLength: 3, waitForStreamInitialization: false,
            configureSubscription: s =>
            {
                s.Spec.Stream = null;
                s.Status = null;
            });
        await context.Handler.WaitForStreamInitializationAsync();
        await Task.Delay(300);

        // assert
        Assert.Equal(0, context.Http.AttemptCount);
    }

    [Fact]
    public async Task A_New_Subscription_Tracking_The_End_Of_The_Stream_Should_Not_Receive_Events_Published_Before_It_Was_Created()
    {
        // arrange & act
        await using var context = await BrokerTestContext.CreateAsync(streamLength: 3, waitForStreamInitialization: false,
            configureSubscription: s =>
            {
                s.Spec.Stream = new() { Offset = StreamPosition.EndOfStream };
                s.Status = null;
            });
        await context.Handler.WaitForStreamInitializationAsync();
        await Task.Delay(300);

        // assert
        Assert.Equal(0, context.Http.AttemptCount);
    }

}
