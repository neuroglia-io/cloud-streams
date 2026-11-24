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

using CloudStreams.Gateway.Application.Configuration;
using CloudStreams.Gateway.Application.Services;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Neuroglia.Data.Expressions.Services;
using Neuroglia.Serialization;

namespace CloudStreams.UnitTests.Cases.Gateway;

public class CloudEventAdmissionControlTests
{

    const string GatewayName = "gateway-a";

    static TestAdmissionControl Create(SplitHorizonPolicy splitHorizon, IGatewayMetrics? metrics = null)
    {
        var gateway = new global::CloudStreams.Core.Resources.Gateway(new ResourceMetadata(GatewayName), new GatewaySpec() { SplitHorizon = splitHorizon });
        var monitor = Substitute.For<IResourceMonitor<global::CloudStreams.Core.Resources.Gateway>>();
        monitor.Resource.Returns(gateway);
        return new TestAdmissionControl(metrics ?? Substitute.For<IGatewayMetrics>(), monitor);
    }

    static CloudEvent CreateEvent(string origins) => new()
    {
        Id = Guid.NewGuid().ToString(),
        SpecVersion = CloudEventSpecVersion.V1.Version,
        Source = new("https://cloud-streams.io/tests"),
        Type = "com.cloud-streams.test.v1",
        ExtensionAttributes = new Dictionary<string, object>() { { SplitHorizonPolicy.DefaultAttributeName, origins } }
    };

    [Fact]
    public async Task An_Event_Whose_Chain_Exceeds_The_Maximum_Length_Should_Be_Rejected()
    {
        // arrange
        var admission = Create(new() { TrackingMode = HorizonTrackingMode.Chain, MaxChainLength = 3, LoopAction = LoopAction.Reject });

        // act
        var result = await admission.EvaluateAsync(CreateEvent("gateway-b gateway-c gateway-d gateway-e"));

        // assert
        Assert.Equal((int)HttpStatusCode.Conflict, result.Status);
    }

    [Fact]
    public async Task A_Loop_Should_Be_Rejected_Even_When_The_Chain_Exceeds_The_Maximum_Length()
    {
        // arrange
        var admission = Create(new() { TrackingMode = HorizonTrackingMode.Chain, MaxChainLength = 1, EvaluationMode = HorizonEvaluationMode.Contains, LoopAction = LoopAction.Reject });

        // act
        var result = await admission.EvaluateAsync(CreateEvent($"gateway-b {GatewayName}"));

        // assert
        Assert.Equal((int)HttpStatusCode.Conflict, result.Status);
    }

    [Fact]
    public async Task An_Event_Whose_Chain_Is_Within_The_Maximum_Length_Should_Be_Admitted()
    {
        // arrange
        var admission = Create(new() { TrackingMode = HorizonTrackingMode.Chain, MaxChainLength = 3, LoopAction = LoopAction.Reject });

        // act
        var result = await admission.EvaluateAsync(CreateEvent("gateway-b gateway-c"));

        // assert
        Assert.Equal((int)HttpStatusCode.OK, result.Status);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task The_Maximum_Chain_Length_Should_Be_Ignored_Unless_Chains_Are_Tracked()
    {
        // arrange
        var admission = Create(new() { TrackingMode = HorizonTrackingMode.Single, MaxChainLength = 1, LoopAction = LoopAction.Reject });

        // act
        var result = await admission.EvaluateAsync(CreateEvent("gateway-b gateway-c"));

        // assert
        Assert.Equal((int)HttpStatusCode.OK, result.Status);
    }

    [Fact]
    public async Task An_Ignored_Loop_Should_Be_Accepted_Without_Data_And_Counted_As_Ignored()
    {
        // arrange
        var metrics = Substitute.For<IGatewayMetrics>();
        var admission = Create(new() { EvaluationMode = HorizonEvaluationMode.Last, LoopAction = LoopAction.Ignore }, metrics);

        // act
        var result = await admission.EvaluateAsync(CreateEvent(GatewayName));

        // assert
        Assert.Equal((int)HttpStatusCode.Accepted, result.Status);
        Assert.Null(result.Data);
        metrics.Received(1).IncrementTotalIgnoredEvents();
        metrics.DidNotReceive().IncrementTotalRejectedEvents();
    }

    sealed class TestAdmissionControl
        : CloudEventAdmissionControl
    {

        public TestAdmissionControl(IGatewayMetrics metrics, IResourceMonitor<global::CloudStreams.Core.Resources.Gateway> configuration)
            : base(Substitute.For<IServiceProvider>(), NullLogger<CloudEventAdmissionControl>.Instance, Options.Create(new GatewayOptions() { Name = GatewayName }), Neuroglia.Serialization.Json.JsonSerializer.Default,
                  metrics, new CloudEventAuthorizationManager(Neuroglia.Serialization.Json.JsonSerializer.Default), Substitute.For<IJsonSchemaGenerator>(), Substitute.For<IJsonSchemaRegistry>(), Substitute.For<IExpressionEvaluator>(), Array.Empty<IValidator<CloudEvent>>())
        {
            this.Configuration = configuration;
        }

    }

}
