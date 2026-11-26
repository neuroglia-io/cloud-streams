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
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the independent authentication of the dashboard and of the ingestion endpoint requested by issue #130
/// </summary>
public class AuthenticationTests
{

    const string Authority = "https://idp.cloud-streams.test/realms/test";
    const string Audience = "cloud-streams";
    static readonly SymmetricSecurityKey SigningKey = new(RandomNumberGenerator.GetBytes(32));

    static async Task<(WebApplication App, HttpClient Client)> CreateAsync(Dictionary<string, string?> settings)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthenticationTests).Assembly);
        builder.Services.AddCloudStreamsAuthentication(builder.Configuration);
        // validate tokens against a local key rather than against the metadata of a real authority
        builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Configuration = new OpenIdConnectConfiguration() { Issuer = Authority };
            options.Configuration.SigningKeys.Add(SigningKey);
            options.ConfigurationManager = new Microsoft.IdentityModel.Protocols.StaticConfigurationManager<OpenIdConnectConfiguration>(options.Configuration);
        });
        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapCloudStreamsAuthenticationEndpoints();
        app.MapFallback(() => "dashboard").RequireAuthorization(CloudStreamsAuthenticationDefaults.DashboardPagePolicy);
        await app.StartAsync();
        var client = app.GetTestClient();
        return (app, client);
    }

    static string CreateToken(string audience = Audience, params string[] roles) => CreateToken(new Dictionary<string, object>() { { "roles", roles } }, audience);

    static string CreateToken(Dictionary<string, object> claims, string audience = Audience) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor()
    {
        Issuer = Authority,
        Audience = audience,
        Expires = DateTime.UtcNow.AddMinutes(5),
        Claims = new Dictionary<string, object>(claims) { { "preferred_username", "jane" } },
        SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)
    });

    static HttpRequestMessage Request(HttpMethod method, string uri, string? token = null, string? apiKey = null)
    {
        var request = new HttpRequestMessage(method, uri);
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (apiKey != null) request.Headers.Add("X-Api-Key", apiKey);
        return request;
    }

    static async Task<HttpStatusCode> SendAsync(HttpClient client, HttpMethod method, string uri, string? token = null)
    {
        using var request = Request(method, uri, token);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    static async Task<string> GetAllowedSourcesAsync(HttpClient client, string? token = null, string? apiKey = null)
    {
        using var request = Request(HttpMethod.Post, "api/test/events/pub", token, apiKey);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    static Dictionary<string, string?> Settings(bool dashboard, bool ingestion, params string[] apiKeys)
    {
        var settings = new Dictionary<string, string?>()
        {
            ["Authentication:Oidc:Authority"] = Authority,
            ["Authentication:Oidc:ClientId"] = "cloud-streams-dashboard",
            ["Authentication:Oidc:Audience"] = Audience,
            ["Authentication:Dashboard:Enabled"] = dashboard.ToString(),
            ["Authentication:Ingestion:Enabled"] = ingestion.ToString()
        };
        for (var i = 0; i < apiKeys.Length; i++) settings[$"Authentication:Ingestion:ApiKeys:{i}"] = apiKeys[i];
        return settings;
    }

    [Fact]
    public async Task Everything_Should_Be_Anonymous_By_Default()
    {
        // arrange
        var (app, client) = await CreateAsync([]);
        await using var _ = app;

        // act & assert
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/test/resources")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("api/test/events/pub", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task Dashboard_Authentication_Should_Protect_The_Api_And_Pages_But_Not_Ingestion()
    {
        // arrange
        var (app, client) = await CreateAsync(Settings(dashboard: true, ingestion: false));
        await using var _ = app;

        // act
        var api = await client.GetAsync("api/test/resources");
        var page = await client.GetAsync("/subscriptions");
        var ingestion = await client.PostAsync("api/test/events/pub", null);

        // assert - API calls get a plain 401, pages are redirected to the sign-in endpoint, and publishers are not affected
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.StartsWith("http://localhost/auth/login?returnUrl=", page.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.OK, ingestion.StatusCode);
    }

    [Fact]
    public async Task Signing_Out_Should_Not_Be_Possible_With_A_Get_Request()
    {
        // arrange
        var (app, client) = await CreateAsync(Settings(dashboard: true, ingestion: false));
        await using var _ = app;

        // act - a link or an image on another site can only issue GET requests (see #150)
        var response = await client.GetAsync("auth/logout");

        // assert - the request is handled like any other page, which sends anonymous users to sign in, rather than signing anyone out
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("http://localhost/auth/login?returnUrl=", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Dashboard_Api_Should_Accept_Bearer_Tokens_Issued_For_The_Configured_Audience()
    {
        // arrange
        var (app, client) = await CreateAsync(Settings(dashboard: true, ingestion: false));
        await using var _ = app;

        // act
        using var valid = new HttpRequestMessage(HttpMethod.Get, "api/test/resources") { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", CreateToken()) } };
        using var wrongAudience = new HttpRequestMessage(HttpMethod.Get, "api/test/resources") { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("another-api")) } };
        var validResponse = await client.SendAsync(valid);
        var wrongAudienceResponse = await client.SendAsync(wrongAudience);

        // assert
        Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);
        Assert.Equal("jane", await validResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, wrongAudienceResponse.StatusCode);
    }

    [Fact]
    public async Task Required_Roles_Should_Be_Enforced()
    {
        // arrange
        var settings = Settings(dashboard: true, ingestion: false);
        settings["Authentication:Dashboard:RequiredRoles:0"] = "cloud-streams-admin";
        var (app, client) = await CreateAsync(settings);
        await using var _ = app;

        // act
        using var withoutRole = new HttpRequestMessage(HttpMethod.Get, "api/test/resources") { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(Audience, "viewer")) } };
        using var withRole = new HttpRequestMessage(HttpMethod.Get, "api/test/resources") { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(Audience, "cloud-streams-admin")) } };

        // assert
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(withoutRole)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(withRole)).StatusCode);
    }

    [Fact]
    public async Task Ingestion_Authentication_Should_Accept_Api_Keys_And_Bearer_Tokens_Independently_Of_The_Dashboard()
    {
        // arrange
        var (app, client) = await CreateAsync(Settings(dashboard: false, ingestion: true, "s3cr3t"));
        await using var _ = app;

        // act
        var anonymous = await client.PostAsync("api/test/events/pub", null);
        using var wrongKey = new HttpRequestMessage(HttpMethod.Post, "api/test/events/pub") { Headers = { { "X-Api-Key", "nope" } } };
        using var rightKey = new HttpRequestMessage(HttpMethod.Post, "api/test/events/pub") { Headers = { { "X-Api-Key", "s3cr3t" } } };
        using var bearer = new HttpRequestMessage(HttpMethod.Post, "api/test/events/pub") { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", CreateToken()) } };

        // assert
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(wrongKey)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(rightKey)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(bearer)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/test/resources")).StatusCode);
    }

    [Fact]
    public async Task User_Endpoint_Should_Describe_The_Current_User()
    {
        // arrange
        var (app, client) = await CreateAsync(Settings(dashboard: true, ingestion: false));
        await using var _ = app;

        // act
        var json = await client.GetStringAsync("api/auth/user");

        // assert
        Assert.Contains("\"enabled\":true", json);
        Assert.Contains("\"authenticated\":false", json);
    }

    [Fact]
    public async Task Roles_Should_Grant_Increasing_Levels_Of_Access()
    {
        // arrange
        var settings = Settings(dashboard: true, ingestion: false);
        settings["Authentication:Dashboard:Roles:Readers:0"] = "cs-reader";
        settings["Authentication:Dashboard:Roles:Operators:0"] = "cs-operator";
        settings["Authentication:Dashboard:Roles:Admins:0"] = "cs-admin";
        var (app, client) = await CreateAsync(settings);
        await using var _ = app;
        var cases = new (string[] Roles, HttpStatusCode Read, HttpStatusCode Operate, HttpStatusCode Administer)[]
        {
            ([], HttpStatusCode.Forbidden, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden),
            (["someone-else"], HttpStatusCode.Forbidden, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden),
            (["cs-reader"], HttpStatusCode.OK, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden),
            (["cs-operator"], HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.Forbidden),
            (["cs-admin"], HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK),
            (["cs-reader", "cs-admin"], HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK)
        };

        foreach (var (roles, read, operate, administer) in cases)
        {
            // act
            var token = CreateToken(Audience, roles);
            var actual = (
                Read: await SendAsync(client, HttpMethod.Get, "api/test/resources", token),
                Operate: await SendAsync(client, HttpMethod.Post, "api/test/resources/orders/operate", token),
                Administer: await SendAsync(client, HttpMethod.Post, "api/test/resources", token),
                Delete: await SendAsync(client, HttpMethod.Delete, "api/test/resources/orders", token));

            // assert
            Assert.Equal((read, operate, administer, administer), actual);
        }
    }

    [Fact]
    public async Task Without_Roles_Every_Authenticated_User_Should_Be_An_Administrator()
    {
        // arrange
        var (app, client) = await CreateAsync(Settings(dashboard: true, ingestion: false));
        await using var _ = app;
        var token = CreateToken();

        // act & assert
        Assert.Equal(HttpStatusCode.OK, await SendAsync(client, HttpMethod.Get, "api/test/resources", token));
        Assert.Equal(HttpStatusCode.OK, await SendAsync(client, HttpMethod.Post, "api/test/resources/orders/operate", token));
        Assert.Equal(HttpStatusCode.OK, await SendAsync(client, HttpMethod.Post, "api/test/resources", token));
        Assert.Equal(HttpStatusCode.Unauthorized, await SendAsync(client, HttpMethod.Post, "api/test/resources"));
    }

    [Fact]
    public async Task Required_Roles_Should_Still_Grant_Administration()
    {
        // arrange - the deprecated setting is merged into the admin roles, so that upgrading never opens the API to users who could not access it
        var settings = Settings(dashboard: true, ingestion: false);
        settings["Authentication:Dashboard:RequiredRoles:0"] = "cloud-streams-admin";
        settings["Authentication:Dashboard:Roles:Readers:0"] = "viewer";
        var (app, client) = await CreateAsync(settings);
        await using var _ = app;

        // act & assert
        Assert.Equal(HttpStatusCode.OK, await SendAsync(client, HttpMethod.Get, "api/test/resources", CreateToken(Audience, "viewer")));
        Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(client, HttpMethod.Post, "api/test/resources", CreateToken(Audience, "viewer")));
        Assert.Equal(HttpStatusCode.OK, await SendAsync(client, HttpMethod.Post, "api/test/resources", CreateToken(Audience, "cloud-streams-admin")));
    }

    [Fact]
    public async Task Without_Authentication_Every_Endpoint_Should_Remain_Open()
    {
        // arrange
        var (app, client) = await CreateAsync([]);
        await using var _ = app;

        // act & assert
        Assert.Equal(HttpStatusCode.OK, await SendAsync(client, HttpMethod.Post, "api/test/resources/orders/operate"));
        Assert.Equal(HttpStatusCode.OK, await SendAsync(client, HttpMethod.Delete, "api/test/resources/orders"));
        Assert.Contains("\"access\":\"administer\"", await client.GetStringAsync("api/auth/user"));
    }

    [Fact]
    public async Task User_Endpoint_Should_Report_No_Access_To_Anonymous_Users()
    {
        // arrange
        var settings = Settings(dashboard: true, ingestion: false);
        settings["Authentication:Dashboard:Roles:Readers:0"] = "cs-reader";
        var (app, client) = await CreateAsync(settings);
        await using var _ = app;

        // act
        var json = await client.GetStringAsync("api/auth/user");

        // assert
        Assert.Contains("\"access\":\"none\"", json);
    }

    [Fact]
    public async Task Api_Keys_Of_Publishers_Should_Be_Bound_To_Their_Sources()
    {
        // arrange
        var settings = Settings(dashboard: false, ingestion: true, "unbound-key");
        settings["Authentication:Ingestion:Publishers:0:Name"] = "orders";
        settings["Authentication:Ingestion:Publishers:0:ApiKey"] = "orders-key";
        settings["Authentication:Ingestion:Publishers:0:Sources:0"] = "https://orders.example.com/*";
        settings["Authentication:Ingestion:Publishers:0:Sources:1"] = "urn:orders:*";
        var (app, client) = await CreateAsync(settings);
        await using var _ = app;

        // act
        var bound = await GetAllowedSourcesAsync(client, apiKey: "orders-key");
        var unbound = await GetAllowedSourcesAsync(client, apiKey: "unbound-key");
        using var wrongKey = Request(HttpMethod.Post, "api/test/events/pub", apiKey: "orders-key-");

        // assert
        Assert.Equal("{\"name\":\"orders\",\"sources\":[\"https://orders.example.com/*\",\"urn:orders:*\"]}", bound);
        Assert.Equal("{\"name\":\"api-key\",\"sources\":null}", unbound);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(wrongKey)).StatusCode);
    }

    [Fact]
    public async Task Bearer_Tokens_Should_Be_Bound_To_The_Sources_Of_The_Configured_Claim()
    {
        // arrange
        var settings = Settings(dashboard: false, ingestion: true);
        settings["Authentication:Ingestion:SourceClaimType"] = "cloud_streams_sources";
        var (app, client) = await CreateAsync(settings);
        await using var _ = app;

        // act
        var array = await GetAllowedSourcesAsync(client, token: CreateToken(new() { { "cloud_streams_sources", new[] { "https://orders.example.com/*", "urn:orders:*" } } }));
        var spaceSeparated = await GetAllowedSourcesAsync(client, token: CreateToken(new() { { "cloud_streams_sources", "https://orders.example.com/* urn:orders:*" } }));
        var missing = await GetAllowedSourcesAsync(client, token: CreateToken());

        // assert - a token without the claim may publish nothing, rather than anything
        Assert.Contains("\"sources\":[\"https://orders.example.com/*\",\"urn:orders:*\"]", array);
        Assert.Contains("\"sources\":[\"https://orders.example.com/*\",\"urn:orders:*\"]", spaceSeparated);
        Assert.Contains("\"sources\":[]", missing);
    }

    [Fact]
    public async Task A_Publisher_Presenting_Several_Credentials_Should_Be_Allowed_What_Any_Of_Them_Allows()
    {
        // arrange
        var settings = Settings(dashboard: false, ingestion: true, "unbound-key");
        settings["Authentication:Ingestion:SourceClaimType"] = "cloud_streams_sources";
        settings["Authentication:Ingestion:Publishers:0:Name"] = "orders";
        settings["Authentication:Ingestion:Publishers:0:ApiKey"] = "orders-key";
        settings["Authentication:Ingestion:Publishers:0:Sources:0"] = "https://orders.example.com/*";
        var (app, client) = await CreateAsync(settings);
        await using var _ = app;
        var billingToken = CreateToken(new() { { "cloud_streams_sources", new[] { "https://billing.example.com/*" } } });

        // act
        var bound = await GetAllowedSourcesAsync(client, token: billingToken, apiKey: "orders-key");
        var unbound = await GetAllowedSourcesAsync(client, token: billingToken, apiKey: "unbound-key");

        // assert
        var sources = System.Text.Json.JsonDocument.Parse(bound).RootElement.GetProperty("sources").EnumerateArray().Select(s => s.GetString()).Order().ToList();
        Assert.Equal(["https://billing.example.com/*", "https://orders.example.com/*"], sources);
        Assert.Contains("\"sources\":null", unbound);
    }

    [Fact]
    public async Task Bearer_Tokens_Should_Not_Be_Bound_Unless_A_Source_Claim_Is_Configured()
    {
        // arrange
        var (app, client) = await CreateAsync(Settings(dashboard: false, ingestion: true));
        await using var _ = app;

        // act
        var sources = await GetAllowedSourcesAsync(client, token: CreateToken(new() { { "cloud_streams_sources", new[] { "urn:orders:*" } } }));

        // assert
        Assert.Contains("\"sources\":null", sources);
    }

    [Theory]
    [InlineData("Name", "orders", "Sources:0", "", "Publishers:0:Sources")]
    [InlineData("Name", "", "Sources:0", "urn:orders:*", "Publishers:0:Name")]
    [InlineData("Name", "orders", "ApiKey", "", "Publishers:0:ApiKey")]
    [InlineData("Name", "orders", "ApiKey", "unbound-key", "configured more than once")]
    public void Invalid_Publishers_Should_Fail_Fast(string key1, string value1, string key2, string value2, string expectedError)
    {
        // arrange
        var settings = Settings(dashboard: false, ingestion: true, "unbound-key");
        settings["Authentication:Ingestion:Publishers:0:Name"] = "orders";
        settings["Authentication:Ingestion:Publishers:0:ApiKey"] = "orders-key";
        settings["Authentication:Ingestion:Publishers:0:Sources:0"] = "urn:orders:*";
        settings[$"Authentication:Ingestion:Publishers:0:{key1}"] = value1;
        settings[$"Authentication:Ingestion:Publishers:0:{key2}"] = value2;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        // act
        var exception = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddCloudStreamsAuthentication(configuration));

        // assert
        Assert.Contains(expectedError, exception.Message);
    }

    [Fact]
    public async Task Every_Endpoint_Of_The_Management_Api_Should_Require_The_Expected_Policy()
    {
        // arrange - the actual controllers, rather than test doubles
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(Settings(dashboard: true, ingestion: false));
        builder.Services.AddControllers().AddApplicationPart(typeof(CloudStreams.Core.Api.Controllers.SubscriptionsController).Assembly);
        builder.Services.AddCloudStreamsAuthentication(builder.Configuration);
        await using var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();

        // act
        var endpoints = app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Select(e => (
                Route: $"{string.Join(',', e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])} {e.RoutePattern.RawText}",
                Anonymous: e.Metadata.GetMetadata<IAllowAnonymous>() != null,
                Policies: e.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).ToList()))
            .ToList();

        // assert
        Assert.NotEmpty(endpoints);
        foreach (var (route, anonymous, policies) in endpoints)
        {
            if (route.EndsWith("/ack") || route.EndsWith("/nack"))
            {
                Assert.True(anonymous, $"'{route}' should be anonymous: the acknowledgement token is its authorization");
                continue;
            }
            Assert.False(anonymous, $"'{route}' should not be anonymous");
            var expected = route.StartsWith("GET ") || route.EndsWith("/compatibility") || route == "POST api/resources/v1/namespaces/{namespace}/subscriptions/test" ? CloudStreamsAuthenticationDefaults.ReadPolicy
                : route.EndsWith("/suspend") || route.EndsWith("/resume") || route.EndsWith("/replay") || route.EndsWith("/skip") || route.EndsWith("/redeliver") || route.EndsWith("/publish") ? CloudStreamsAuthenticationDefaults.OperatePolicy
                : CloudStreamsAuthenticationDefaults.AdministerPolicy;
            Assert.True(policies.SequenceEqual([expected]), $"'{route}' should require '{expected}', not '{string.Join(", ", policies)}'");
        }
        Assert.Contains(endpoints, e => e.Route == "PATCH api/resources/v1/namespaces/{namespace}/subscriptions/{name}/status" && e.Policies.SequenceEqual([CloudStreamsAuthenticationDefaults.AdministerPolicy]));
        Assert.Contains(endpoints, e => e.Route == "POST api/resources/v1/namespaces/{namespace}/subscriptions/{name}/replay");
        Assert.Contains(endpoints, e => e.Route == "POST api/resources/v1/namespaces/{namespace}/subscriptions/{name}/suspend");
        Assert.Contains(endpoints, e => e.Route == "POST api/resources/v1/namespaces/{namespace}/subscriptions/{name}/redeliver" && e.Policies.SequenceEqual([CloudStreamsAuthenticationDefaults.OperatePolicy]));
        Assert.Contains(endpoints, e => e.Route == "POST api/core/v1/cloud-events/publish" && e.Policies.SequenceEqual([CloudStreamsAuthenticationDefaults.OperatePolicy]));
        Assert.Contains(endpoints, e => e.Route == "GET api/core/v1/cloud-events/search" && e.Policies.SequenceEqual([CloudStreamsAuthenticationDefaults.ReadPolicy]));
        Assert.Contains(endpoints, e => e.Route == "POST api/resources/v1/namespaces/{namespace}/subscriptions/{name}/skip" && e.Policies.SequenceEqual([CloudStreamsAuthenticationDefaults.OperatePolicy]));
        Assert.Contains(endpoints, e => e.Route == "POST api/resources/v1/namespaces/{namespace}/subscriptions/test" && e.Policies.SequenceEqual([CloudStreamsAuthenticationDefaults.ReadPolicy]));
        Assert.Contains(endpoints, e => e.Route == "GET api/core/v1/cloud-events/stream/activity");
        Assert.Contains(endpoints, e => e.Route == "GET api/core/v1/cloud-events/partitions/{type}/metadata");
    }

    [Fact]
    public void Invalid_Configuration_Should_Fail_Fast()
    {
        // arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>() { ["Authentication:Dashboard:Enabled"] = "true" }).Build();

        // act
        var exception = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddCloudStreamsAuthentication(configuration));

        // assert
        Assert.Contains("Authentication:Oidc:Authority", exception.Message);
    }

}

/// <summary>
/// Stands for the management API, protected by the dashboard policy by convention
/// </summary>
[Route("api/test/resources")]
public class TestResourcesController
    : ControllerBase
{

    [HttpGet]
    public IActionResult Get() => this.Ok(this.User.Identity?.Name ?? "anonymous");

    /// <summary>
    /// Stands for the endpoints that create, replace or patch resources, which only administrators may call by convention
    /// </summary>
    [HttpPost]
    public IActionResult Create() => this.Ok();

    [HttpDelete("{name}")]
    public IActionResult Delete(string name) => this.Ok(name);

    /// <summary>
    /// Stands for the endpoints that suspend, resume or replay subscriptions
    /// </summary>
    [HttpPost("{name}/operate")]
    [Authorize(Policy = CloudStreamsAuthenticationDefaults.OperatePolicy)]
    public IActionResult Operate(string name) => this.Ok(name);

}

/// <summary>
/// Stands for the cloud event ingestion endpoint, which declares its own policy, authorizes publishers like it does for a gateway that declares no publisher of its own,
/// and describes the sources the publisher may publish cloud events of
/// </summary>
[Route("api/test/events")]
public class TestEventsController(CloudStreams.Core.Api.Configuration.CloudStreamsAuthenticationOptions options)
    : ControllerBase
{

    [HttpPost("pub")]
    [Authorize(Policy = CloudStreamsAuthenticationDefaults.IngestionPolicy)]
    public async Task<IActionResult> Publish()
    {
        var authorizer = new CloudStreams.Gateway.Api.Services.PublisherAuthorizer(Microsoft.Extensions.Logging.Abstractions.NullLogger<CloudStreams.Gateway.Api.Services.PublisherAuthorizer>.Instance, options, Substitute.For<CloudStreams.Core.Transports.Authentication.ICredentialResolver>());
        var authorization = await authorizer.AuthorizeAsync(this.HttpContext, new(new("default", Namespace.DefaultNamespaceName), new()));
        if (authorization.Status != (int)HttpStatusCode.OK) return this.StatusCode(authorization.Status);
        return this.Ok(new { name = authorization.Publisher, sources = authorization.AllowedSources });
    }

}
