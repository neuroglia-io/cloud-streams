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
using CloudStreams.Core.Transports.Authentication;
using CloudStreams.Gateway.Api.Services;
using CloudStreams.Gateway.Application.Commands.CloudEvents;
using CloudStreams.Gateway.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Neuroglia.Mediation;
using System.Security.Claims;
using GatewayResource = CloudStreams.Core.Resources.Gateway;

namespace CloudStreams.UnitTests.Cases.Gateway;

/// <summary>
/// Covers how a gateway decides whether a publisher may publish to it, and so to its namespace (see #215)
/// </summary>
public class PublisherAuthorizationTests
{

    const string Tenant = "tenant-a";

    static GatewayResource Gateway(params IngestionPublisher[] publishers) => new(new("gateway", Tenant), new() { Ingestion = publishers.Length == 0 ? null : new() { Publishers = [.. publishers] } });

    static PublisherAuthorizer Authorizer(CloudStreamsAuthenticationOptions options, string? key = null)
    {
        var credentials = Substitute.For<ICredentialResolver>();
        credentials.ResolveAsync(Arg.Any<AuthenticationPolicy>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(key == null ? null : TransportCredentials.ApiKeyCredentials(key, null));
        return new(NullLogger<PublisherAuthorizer>.Instance, options, credentials);
    }

    static CloudStreamsAuthenticationOptions Ingestion(string? namespaceClaimType = null) => new() { Ingestion = new() { Enabled = true, NamespaceClaimType = namespaceClaimType } };

    static HttpContext Context(ClaimsIdentity? identity = null, string? apiKey = null)
    {
        var context = new DefaultHttpContext() { User = new ClaimsPrincipal(identity ?? new ClaimsIdentity()) };
        if (apiKey != null) context.Request.Headers[ApiKeyAuthenticationPolicy.DefaultHeader] = apiKey;
        return context;
    }

    static ClaimsIdentity Token(params Claim[] claims) => new([new Claim(ClaimTypes.Name, "jane"), .. claims], "Bearer", ClaimTypes.Name, "roles");

    [Fact]
    public async Task Anonymous_Publishers_Should_Be_Refused_When_Ingestion_Is_Authenticated()
    {
        var result = await Authorizer(Ingestion()).AuthorizeAsync(Context(), Gateway());
        Assert.Equal(401, result.Status);
    }

    [Fact]
    public async Task Publishers_Should_Be_Restricted_To_The_Namespaces_Of_Their_Token()
    {
        // arrange
        var authorizer = Authorizer(Ingestion(namespaceClaimType: "cloud_streams_namespaces"));

        // act
        var allowed = await authorizer.AuthorizeAsync(Context(Token(new Claim("cloud_streams_namespaces", $"other {Tenant}"))), Gateway());
        var refused = await authorizer.AuthorizeAsync(Context(Token(new Claim("cloud_streams_namespaces", "other"))), Gateway());
        var missing = await authorizer.AuthorizeAsync(Context(Token()), Gateway());

        // assert - a token without the claim may publish to no namespace, rather than to any
        Assert.Equal(200, allowed.Status);
        Assert.Equal(403, refused.Status);
        Assert.Equal(403, missing.Status);
    }

    [Fact]
    public async Task A_Publisher_Of_The_Gateway_Should_Be_Identified_By_The_Api_Key_Its_Namespace_Holds()
    {
        // arrange
        var gateway = Gateway(new IngestionPublisher() { Name = "orders", ApiKey = new() { Key = "unused" }, Sources = ["https://orders.example.com/*"] });
        var authorizer = Authorizer(Ingestion(), key: "orders-key");

        // act
        var right = await authorizer.AuthorizeAsync(Context(apiKey: "orders-key"), gateway);
        var wrong = await authorizer.AuthorizeAsync(Context(apiKey: "orders-key-"), gateway);
        var anonymous = await authorizer.AuthorizeAsync(Context(), gateway);

        // assert
        Assert.Equal(200, right.Status);
        Assert.Equal("orders", right.Publisher);
        Assert.Equal(["https://orders.example.com/*"], right.AllowedSources!);
        Assert.Equal(403, wrong.Status);
        Assert.Equal(401, anonymous.Status);
    }

    [Fact]
    public async Task A_Publisher_Of_The_Gateway_Should_Be_Identified_By_The_Subject_And_Claims_Of_Its_Token()
    {
        // arrange
        var gateway = Gateway(new IngestionPublisher() { Name = "billing", Subject = "billing-service", Claims = new Dictionary<string, string>() { { "azp", "billing" } } });
        var authorizer = Authorizer(Ingestion(namespaceClaimType: "cloud_streams_namespaces"));

        // act
        var matching = await authorizer.AuthorizeAsync(Context(Token(new Claim("sub", "billing-service"), new Claim("azp", "billing"))), gateway);
        var otherClient = await authorizer.AuthorizeAsync(Context(Token(new Claim("sub", "billing-service"), new Claim("azp", "orders"))), gateway);

        // assert - the gateway's publishers need no namespace claim: the gateway itself admits them
        Assert.Equal(200, matching.Status);
        Assert.Equal("billing", matching.Publisher);
        Assert.Equal(403, otherClient.Status);
    }

    [Fact]
    public async Task Publishing_Through_The_Api_Should_Require_The_Operator_Role_In_The_Namespace_Of_The_Gateway()
    {
        // arrange
        var options = new CloudStreamsAuthenticationOptions() { Dashboard = new() { Enabled = true, Roles = new() { Namespaced = "cloud-streams:{namespace}:{level}" } } };
        var configuration = Substitute.For<IGatewayConfiguration>();
        configuration.Gateway.Returns(Gateway());
        var mediator = Substitute.For<IMediator>();
        mediator.ExecuteAsync(Arg.Any<ConsumeEventCommand>(), Arg.Any<CancellationToken>()).Returns(new OperationResult(201));
        var publisher = new MediatorCloudEventPublisher(mediator, options, configuration);
        var e = new CloudEvent() { Id = "1", SpecVersion = "1.0", Source = new("https://orders.example.com"), Type = "order.placed" };

        // act
        var operatorOfOther = await publisher.PublishAsync(e, new ClaimsPrincipal(Token(new Claim("roles", "cloud-streams:other:operator"))));
        var reader = await publisher.PublishAsync(e, new ClaimsPrincipal(Token(new Claim("roles", $"cloud-streams:{Tenant}:reader"))));
        var @operator = await publisher.PublishAsync(e, new ClaimsPrincipal(Token(new Claim("roles", $"cloud-streams:{Tenant}:operator"))));

        // assert
        Assert.Equal(403, operatorOfOther.Status);
        Assert.Equal(403, reader.Status);
        Assert.Equal(201, @operator.Status);
        await mediator.Received(1).ExecuteAsync(Arg.Is<ConsumeEventCommand>(c => c!.Gateway.GetNamespace() == Tenant), Arg.Any<CancellationToken>());
    }

}
