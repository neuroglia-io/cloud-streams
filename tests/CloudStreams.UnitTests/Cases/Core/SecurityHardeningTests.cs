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
using CloudStreams.Core.Api.Configuration;
using CloudStreams.Core.Api.Services;
using CloudStreams.Core.Application.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the hardening features of the security epic (#144, #145, #148)
/// </summary>
public class SecurityHardeningTests
{

    // ---------- #148 ----------

    static OutboundRequestPolicy Policy(OutboundRequestOptions? options = null) => new(Options.Create(options ?? new()));

    [Theory]
    [InlineData("169.254.169.254", false)]
    [InlineData("fe80::1", false)]
    [InlineData("10.0.0.12", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("8.8.8.8", true)]
    public void By_Default_Only_Link_Local_Addresses_Should_Be_Denied(string address, bool allowed)
    {
        Assert.Equal(allowed, Policy().IsAllowed(System.Net.IPAddress.Parse(address)));
    }

    [Fact]
    public void Denied_Networks_And_Private_Networks_Should_Be_Enforced()
    {
        // arrange
        var policy = Policy(new() { DeniedNetworks = ["10.1.0.0/16"] });
        var strict = Policy(new() { DenyPrivateNetworks = true });

        // assert
        Assert.False(policy.IsAllowed(System.Net.IPAddress.Parse("10.1.2.3")));
        Assert.True(policy.IsAllowed(System.Net.IPAddress.Parse("10.2.2.3")));
        Assert.False(strict.IsAllowed(System.Net.IPAddress.Parse("10.2.2.3")));
        Assert.True(strict.IsAllowed(System.Net.IPAddress.Parse("8.8.8.8")));
    }

    [Theory]
    [InlineData("subscriber.example.com", true)]
    [InlineData("svc.cluster.local", false)]
    [InlineData("orders.default.svc.cluster.local", true)]
    [InlineData("evil.com", false)]
    public void Only_Allowed_Hosts_Should_Be_Reached_When_Some_Are_Configured(string host, bool allowed)
    {
        Assert.Equal(allowed, Policy(new() { AllowedHosts = ["subscriber.example.com", "*.svc.cluster.local"] }).IsAllowed(host));
    }

    [Fact]
    public async Task Requests_To_A_Denied_Address_Should_Fail_When_Connecting()
    {
        // arrange
        using var client = new HttpClient(new SocketsHttpHandler() { ConnectCallback = Policy().ConnectAsync });

        // act & assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://169.254.169.254/latest/meta-data/"));
        Assert.Contains("not allowed", exception.Message);
    }

    // ---------- #144 ----------

    [Theory]
    [InlineData("/api/events/pub", true)]
    [InlineData("/healthz", true)]
    [InlineData("/healthz/live", true)]
    [InlineData("/api/resources/v1/subscriptions/orders/ack", true)]
    [InlineData("/api/resources/v1/subscriptions/orders/nack", true)]
    [InlineData("/api/resources/v1/subscriptions", false)]
    [InlineData("/api/resources/v1/subscriptions/orders", false)]
    [InlineData("/mcp", false)]
    [InlineData("/", false)]
    public void Only_Ingestion_Acknowledgements_And_Health_Checks_Should_Be_Public(string path, bool expected)
    {
        Assert.Equal(expected, ManagementApiExtensions.IsPublic(path));
    }

    static async Task<(WebApplication App, HttpClient Client)> CreateAsync(Dictionary<string, string?> settings)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.Configure<ManagementApiOptions>(builder.Configuration.GetSection(ManagementApiOptions.ConfigurationSection));
        var app = builder.Build();
        app.UseManagementApiIsolation();
        app.Run(context => context.Response.WriteAsync("served"));
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    [Fact]
    public async Task A_Disabled_Management_Api_Should_Only_Leave_The_Public_Endpoints()
    {
        // arrange
        var (app, client) = await CreateAsync(new() { ["ManagementApi:Enabled"] = "false" });
        await using var _ = app;

        // act & assert
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await client.GetAsync("/api/resources/v1/subscriptions")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await client.GetAsync("/")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.PostAsync("/api/events/pub", null)).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync("/healthz")).StatusCode);
    }

    [Fact]
    public async Task A_Management_Api_Bound_To_Another_Port_Should_Not_Be_Served_On_This_One()
    {
        // arrange - the test server reports no local port, so it is never the management port
        var (app, client) = await CreateAsync(new() { ["ManagementApi:Port"] = "8081" });
        await using var _ = app;

        // act & assert
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await client.GetAsync("/api/resources/v1/subscriptions")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.PostAsync("/api/resources/v1/subscriptions/orders/ack", null)).StatusCode);
    }

    [Fact]
    public async Task By_Default_The_Management_Api_Should_Be_Served()
    {
        // arrange
        var (app, client) = await CreateAsync([]);
        await using var _ = app;

        // act & assert
        Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync("/api/resources/v1/subscriptions")).StatusCode);
    }

    // ---------- #144 and #145 ----------

    static IReadOnlyList<string> Warnings(CloudStreamsAuthenticationOptions authentication, ManagementApiOptions? managementApi = null, Dictionary<string, string?>? settings = null) =>
        [.. new SecurityConfigurationAuditor(NullLogger<SecurityConfigurationAuditor>.Instance, authentication, Options.Create(managementApi ?? new()), new ConfigurationBuilder().AddInMemoryCollection(settings ?? []).Build()).GetWarnings()];

    [Fact]
    public void An_Unauthenticated_Management_Api_Should_Be_Warned_About()
    {
        Assert.Contains(Warnings(new()), w => w.Contains("without authentication"));
        Assert.DoesNotContain(Warnings(new(), new() { Enabled = false }), w => w.Contains("without authentication"));
    }

    [Fact]
    public void Bearer_Tokens_Without_Audience_Should_Be_Warned_About()
    {
        // arrange
        var withoutAudience = new CloudStreamsAuthenticationOptions() { Oidc = new() { Authority = "https://idp.example.com" } };
        var withAudience = new CloudStreamsAuthenticationOptions() { Oidc = new() { Authority = "https://idp.example.com", Audience = "cloud-streams" } };

        // assert
        Assert.Contains(Warnings(withoutAudience), w => w.Contains("audience"));
        Assert.DoesNotContain(Warnings(withAudience), w => w.Contains("audience"));
    }

    [Fact]
    public void A_Required_Audience_Should_Be_Enforced_At_Startup()
    {
        // arrange
        var options = new CloudStreamsAuthenticationOptions() { Oidc = new() { Authority = "https://idp.example.com", RequireAudience = true } };

        // act & assert
        Assert.Contains(options.Validate(), e => e.Contains("Audience"));
    }

}
