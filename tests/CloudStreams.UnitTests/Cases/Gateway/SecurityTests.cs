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

using CloudStreams.Core.Application.Configuration;
using CloudStreams.Gateway.Application.Configuration;
using CloudStreams.Gateway.Application.Services;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Neuroglia.Data.Expressions.Services;
using System.Diagnostics;
using GatewayResource = CloudStreams.Core.Resources.Gateway;

namespace CloudStreams.UnitTests.Cases.Gateway;

/// <summary>
/// Covers the fixes of the security epic (#140)
/// </summary>
public class SecurityTests
{

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    static CloudEvent Event(DateTimeOffset? time = null) => new()
    {
        Id = "1",
        SpecVersion = CloudEventSpecVersion.V1.Version,
        Source = new("https://cloud-streams.io/tests"),
        Type = "com.cloud-streams.test.v1",
        Time = time
    };

    static CloudEventAuthorizationPolicy Policy(CloudEventAuthorizationRule rule) => new() { DecisionStrategy = RuleBasedDecisionStrategy.Unanimous, Rules = [rule] };

    // ---------- #143 ----------

    [Fact]
    public async Task A_Temporary_Rule_Should_Be_Evaluated_Against_The_Time_Of_Evaluation_Rather_Than_The_Event_Time()
    {
        // arrange - the window is over, but the event claims (or omits) a time within it
        var manager = new CloudEventAuthorizationManager(Neuroglia.Serialization.Json.JsonSerializer.Default, new FixedTimeProvider(DateTimeOffset.Parse("2026-06-01T00:00:00Z")));
        var policy = Policy(new() { Type = CloudEventAuthorizationRuleType.Temporary, Effect = AuthorizationPolicyEffect.Authorize, From = DateTimeOffset.Parse("2026-01-01T00:00:00Z"), To = DateTimeOffset.Parse("2026-02-01T00:00:00Z") });

        // act
        var withoutTime = await manager.EvaluateAsync(Event(), policy);
        var withForgedTime = await manager.EvaluateAsync(Event(DateTimeOffset.Parse("2026-01-15T00:00:00Z")), policy);

        // assert
        Assert.Equal((int)HttpStatusCode.Forbidden, withoutTime.Status);
        Assert.Equal((int)HttpStatusCode.Forbidden, withForgedTime.Status);
    }

    [Theory]
    [InlineData("2026-06-01T10:00:00Z", "08:00:00", "18:00:00", true)]
    [InlineData("2026-06-01T20:00:00Z", "08:00:00", "18:00:00", false)]
    [InlineData("2026-06-01T23:00:00Z", "22:00:00", "06:00:00", true)]
    [InlineData("2026-06-01T03:00:00Z", "22:00:00", "06:00:00", true)]
    [InlineData("2026-06-01T12:00:00Z", "22:00:00", "06:00:00", false)]
    public async Task A_Time_Of_Day_Rule_Should_Be_Evaluated_Against_The_Time_Of_Evaluation_And_Never_Throw(string now, string from, string to, bool authorized)
    {
        // arrange
        var manager = new CloudEventAuthorizationManager(Neuroglia.Serialization.Json.JsonSerializer.Default, new FixedTimeProvider(DateTimeOffset.Parse(now)));
        var policy = Policy(new()
        {
            Type = CloudEventAuthorizationRuleType.TimeOfDay,
            Effect = AuthorizationPolicyEffect.Authorize,
            From = DateTimeOffset.Parse($"2026-01-01T{from}Z"),
            To = DateTimeOffset.Parse($"2026-01-01T{to}Z")
        });

        // act - the event has no time, which used to throw
        var result = await manager.EvaluateAsync(Event(), policy);

        // assert
        Assert.Equal(authorized ? (int)HttpStatusCode.OK : (int)HttpStatusCode.Forbidden, result.Status);
    }

    // ---------- #142 ----------

    [Fact]
    public async Task Events_Should_Be_Refused_Until_The_Gateway_Configuration_Has_Been_Loaded()
    {
        // arrange
        var admission = new CloudEventAdmissionControl(Substitute.For<IServiceProvider>(), NullLogger<CloudEventAdmissionControl>.Instance, Options.Create(new GatewayOptions() { Name = "gateway" }), Neuroglia.Serialization.Json.JsonSerializer.Default,
            Substitute.For<IGatewayMetrics>(), Substitute.For<ICloudEventAuthorizationManager>(), Substitute.For<IJsonSchemaGenerator>(), Substitute.For<IJsonSchemaRegistry>(), Substitute.For<IExpressionEvaluator>(), Array.Empty<IValidator<CloudEvent>>());

        // act
        var result = await admission.EvaluateAsync(Event());

        // assert
        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, result.Status);
        Assert.Null(result.Data);
    }

    // ---------- #146 ----------

    [Fact]
    public void A_Pathological_Pattern_Should_Time_Out_As_A_Mismatch()
    {
        // arrange
        var input = new string('a', 64) + "!";
        var stopwatch = Stopwatch.StartNew();

        // act
        var matches = CloudStreamsRegex.IsMatch(input, "^(a+)+$");

        // assert
        Assert.False(matches);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"The match should have timed out, took {stopwatch.Elapsed}");
    }

    [Fact]
    public void Patterns_Should_Be_Compiled_Once()
    {
        Assert.Same(CloudStreamsRegex.Get("^order\\.created$"), CloudStreamsRegex.Get("^order\\.created$"));
    }

    // ---------- #141 ----------

    static MemoryJsonSchemaRegistry Registry(JsonSchemaRegistryOptions? options = null) => new(Options.Create(options ?? new()), Neuroglia.Serialization.Json.JsonSerializer.Default);

    [Theory]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.20.0.1", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("::ffff:10.0.0.1", false)]
    [InlineData("8.8.8.8", true)]
    [InlineData("2001:4860:4860::8888", true)]
    public void Only_Public_Addresses_Should_Be_Allowed(string address, bool expected)
    {
        Assert.Equal(expected, MemoryJsonSchemaRegistry.IsPublicAddress(System.Net.IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("https://schemas.example.com/a.json", true)]
    [InlineData("https://sub.example.org/a.json", true)]
    [InlineData("https://example.org/a.json", false)]
    [InlineData("https://evil.com/a.json", false)]
    [InlineData("file:///etc/passwd", false)]
    public void Only_Allowed_Hosts_Should_Be_Fetched_From(string uri, bool expected)
    {
        // arrange
        using var registry = Registry(new() { AllowedHosts = ["schemas.example.com", "*.example.org"] });

        // act & assert
        Assert.Equal(expected, registry.IsAllowed(new Uri(uri)));
    }

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://127.0.0.1:1/schema.json")]
    [InlineData("http://localhost:1/schema.json")]
    public async Task Fetching_A_Schema_From_A_Private_Address_Should_Fail(string uri)
    {
        // arrange
        using var registry = Registry();

        // act & assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => registry.GetAsync(new Uri(uri)));
        Assert.Contains("private", exception.Message);
    }

    [Fact]
    public async Task Fetching_A_Schema_Over_Another_Scheme_Should_Be_Refused()
    {
        // arrange
        using var registry = Registry();

        // act & assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => registry.GetAsync(new Uri("file:///etc/passwd")));
    }

}
