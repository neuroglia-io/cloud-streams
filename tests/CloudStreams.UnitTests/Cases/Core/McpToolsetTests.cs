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

using CloudStreams.Core.Api;
using CloudStreams.Core.Api.Tools;
using CloudStreams.Core.Application.Commands.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using Neuroglia.Mediation;
using System.Reflection;
using System.Security.Claims;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the MCP tools, and the roles they require (see #183 and #217)
/// </summary>
public class McpToolsetTests
{

    static IHttpContextAccessor Caller() => new HttpContextAccessor() { HttpContext = new DefaultHttpContext() { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "test")) } };

    static IAuthorizationService Authorizes(params string[] policies)
    {
        var authorization = Substitute.For<IAuthorizationService>();
        authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(call => policies.Contains(call.ArgAt<string>(2)) ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        return authorization;
    }

    static readonly Subscription Subscription = new() { Metadata = new() { Name = "orders" }, Spec = new() { Subscriber = new() { Endpoint = new Uri("http://localhost") } } };

    [Fact]
    public void Every_Tool_Should_Be_Discoverable()
    {
        var tools = typeof(CloudEventToolset).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
            .SelectMany(t => t.GetMethods())
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .OfType<string>()
            .ToHashSet();
        Assert.Superset(new HashSet<string>() { "GetStreamMetadata", "GetPartitionMetadata", "ListPartitions", "ReadStream", "PublishEvent", "ListSubscriptions", "GetSubscription", "ListDeadLetters", "SuspendSubscription", "ResumeSubscription", "ReplaySubscription" }, tools);
    }

    [Fact]
    public async Task Operating_A_Subscription_Should_Require_The_Operator_Role()
    {
        // arrange
        var mediator = Substitute.For<IMediator>();
        var toolset = new SubscriptionToolset(mediator, Authorizes(CloudStreamsAuthenticationDefaults.ReadPolicy), Caller());

        // act and assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => toolset.SuspendSubscriptionAsync("orders"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => toolset.ResumeSubscriptionAsync("orders"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => toolset.ReplaySubscriptionAsync("orders"));
        await mediator.DidNotReceiveWithAnyArgs().ExecuteAsync(Arg.Any<IRequest<IOperationResult<Subscription>>>(), default);
    }

    [Fact]
    public async Task An_Operator_Should_Replay_A_Subscription_From_A_Point_In_Time()
    {
        // arrange
        var mediator = Substitute.For<IMediator>();
        var from = new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);
        mediator.ExecuteAsync(Arg.Any<ReplaySubscriptionCommand>(), Arg.Any<CancellationToken>()).Returns(new OperationResult<Subscription>(200, Subscription));
        var toolset = new SubscriptionToolset(mediator, Authorizes(CloudStreamsAuthenticationDefaults.ReadPolicy, CloudStreamsAuthenticationDefaults.OperatePolicy), Caller());

        // act
        var replayed = await toolset.ReplaySubscriptionAsync("orders", from);

        // assert
        Assert.Same(Subscription, replayed);
        await mediator.Received(1).ExecuteAsync(Arg.Is<ReplaySubscriptionCommand>(c => c.Name == "orders" && c.From == from), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_Failed_Operation_Should_Describe_Why()
    {
        // arrange
        var mediator = Substitute.For<IMediator>();
        mediator.ExecuteAsync(Arg.Any<ResumeSubscriptionCommand>(), Arg.Any<CancellationToken>()).Returns(new OperationResult<Subscription>(404, errors: [new Error(ProblemTypes.NotFound, "Not Found", 404, "The subscription 'orders' does not exist")]));
        var toolset = new SubscriptionToolset(mediator, Authorizes(CloudStreamsAuthenticationDefaults.OperatePolicy), Caller());

        // act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => toolset.ResumeSubscriptionAsync("orders"));

        // assert
        Assert.Contains("does not exist", exception.Message);
    }

    static CloudEvent Event => new() { Id = "1", SpecVersion = "1.0", Source = new("https://orders.example.com"), Type = "order.placed" };

    static CloudEventToolset EventToolset(IServiceProvider services, IAuthorizationService authorization) => new(NullLogger<CloudEventToolset>.Instance, Substitute.For<IMediator>(), services, authorization, Caller());

    [Fact]
    public async Task Publishing_Should_Require_The_Operator_Role()
    {
        var publisher = Substitute.For<ICloudEventPublisher>();
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(ICloudEventPublisher)).Returns(publisher);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => EventToolset(services, Authorizes(CloudStreamsAuthenticationDefaults.ReadPolicy)).PublishEventAsync(Event));
        await publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, default!, default);
    }

    [Fact]
    public async Task Publishing_Should_Only_Be_Available_On_Gateways()
    {
        var services = Substitute.For<IServiceProvider>();
        await Assert.ThrowsAsync<NotSupportedException>(() => EventToolset(services, Authorizes(CloudStreamsAuthenticationDefaults.OperatePolicy)).PublishEventAsync(Event));
    }

    [Theory]
    [InlineData(201, "stored")]
    [InlineData(202, "ignored")]
    [InlineData(200, "duplicate")]
    public async Task Publishing_Should_Tell_What_Became_Of_The_Event(int status, string outcome)
    {
        // arrange
        var publisher = Substitute.For<ICloudEventPublisher>();
        publisher.PublishAsync(Arg.Any<CloudEvent>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>()).Returns(new OperationResult(status));
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(ICloudEventPublisher)).Returns(publisher);

        // act
        var result = await EventToolset(services, Authorizes(CloudStreamsAuthenticationDefaults.OperatePolicy)).PublishEventAsync(Event);

        // assert - the event is published as the calling user
        Assert.Equal(outcome, result);
        await publisher.Received(1).PublishAsync(Event, Arg.Is<ClaimsPrincipal>(u => u.Identity!.Name == "alice"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_Refused_Event_Should_Describe_Why()
    {
        var publisher = Substitute.For<ICloudEventPublisher>();
        publisher.PublishAsync(Arg.Any<CloudEvent>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>()).Returns(new OperationResult(403, null, new Error(new Uri("https://cloud-streams.io/problems/source-not-allowed"), "Forbidden", 403, "The publisher is not allowed to publish cloud events of source 'x'")));
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(ICloudEventPublisher)).Returns(publisher);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => EventToolset(services, Authorizes(CloudStreamsAuthenticationDefaults.OperatePolicy)).PublishEventAsync(Event));
        Assert.Contains("not allowed", exception.Message);
    }

}
