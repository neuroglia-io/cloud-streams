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

using CloudStreams.Core.Transports.Authentication;
using CloudStreams.Core.Transports.Secrets;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json.Nodes;

namespace CloudStreams.UnitTests.Cases.Transports;

/// <summary>
/// Covers how secrets are read from their providers, and how the credentials of endpoints are resolved, including OAuth2 and OpenID Connect tokens (see #218)
/// </summary>
public sealed class CredentialTests
    : IDisposable
{

    readonly string _directory = Directory.CreateTempSubdirectory("cloud-streams-secrets-").FullName;
    readonly List<string> _variables = [];

    public void Dispose()
    {
        Directory.Delete(this._directory, true);
        foreach (var variable in this._variables) Environment.SetEnvironmentVariable(variable, null);
    }

    void SetVariable(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        this._variables.Add(name);
    }

    void WriteSecret(string name, IReadOnlyDictionary<string, string> values)
    {
        var directory = Directory.CreateDirectory(Path.Combine(this._directory, name)).FullName;
        foreach (var (key, value) in values) File.WriteAllText(Path.Combine(directory, key), value + "\n");
    }

    static IOptionsMonitor<SecretOptions> Options(Action<SecretOptions>? configure = null)
    {
        var options = new SecretOptions();
        configure?.Invoke(options);
        var monitor = Substitute.For<IOptionsMonitor<SecretOptions>>();
        monitor.CurrentValue.Returns(options);
        return monitor;
    }

    SecretStore Store(Action<SecretOptions>? configure = null, params ISecretProvider[] extra)
    {
        var options = Options(o =>
        {
            o.File.Directory = this._directory;
            configure?.Invoke(o);
        });
        return new([new EnvironmentSecretProvider(), new FileSecretProvider(options), .. extra], options, TimeProvider.System);
    }

    sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Requests.Add((request, request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            return await respond(request);
        }
    }

    static IHttpClientFactory Factory(HttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler, false));
        return factory;
    }

    static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Secrets_Should_Be_Read_From_Environment_Variables_Regardless_Of_Case_And_Separators()
    {
        // arrange
        var name = $"orders-{Guid.NewGuid():N}"[..20];
        SetVariable($"CLOUDSTREAMS_SECRETS__{name.ToUpperInvariant().Replace('-', '_')}__CLIENT_ID", "cloud-streams");
        SetVariable($"CLOUDSTREAMS_SECRETS__{name.ToUpperInvariant().Replace('-', '_')}__PASSWORD", "s3cr3t");

        // act
        var secret = await this.Store().GetAsync(new(name));

        // assert
        Assert.Equal("cloud-streams", secret.Get("client.id"));
        Assert.Equal("s3cr3t", secret.Get("password"));
    }

    [Fact]
    public async Task Secrets_Should_Be_Read_From_Files_Without_Their_Final_Line_Break()
    {
        // arrange
        WriteSecret("orders-webhook", new Dictionary<string, string>() { ["username"] = "cloud-streams", ["password"] = "s3cr3t" });

        // act
        var secret = await this.Store().GetAsync(new(SecretProviders.File, "orders-webhook"));

        // assert
        Assert.Equal("s3cr3t", secret.Get("password"));
    }

    [Fact]
    public async Task A_Secret_Should_Not_Be_Read_Outside_The_Directory_Of_Secrets()
    {
        // act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => this.Store().GetAsync(new(SecretProviders.File, "../../etc")));

        // assert
        Assert.Contains("outside the directory", ex.Message);
    }

    [Fact]
    public async Task A_Secret_Of_A_Provider_That_Is_Not_Enabled_Should_Not_Be_Read()
    {
        // act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => this.Store().GetAsync(new(SecretProviders.Vault, "orders")));

        // assert
        Assert.Contains("is not enabled", ex.Message);
    }

    [Fact]
    public async Task A_Missing_Secret_Should_Be_Reported_With_The_Providers_Asked()
    {
        // act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => this.Store().GetAsync(new("does-not-exist")));

        // assert
        Assert.Contains("env, file", ex.Message);
    }

    [Fact]
    public async Task Credentials_Should_Take_The_Values_Of_Their_Secret_Over_Their_Own()
    {
        // arrange
        WriteSecret("orders-webhook", new Dictionary<string, string>() { ["password"] = "s3cr3t" });
        var resolver = new CredentialResolver(this.Store(), Substitute.For<IOAuth2TokenClient>());

        // act
        var credentials = await resolver.ResolveAsync(new() { Basic = new() { Use = new("orders-webhook"), Username = "cloud-streams", Password = "ignored" } });
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://orders.example.com");
        await credentials!.AuthenticateAsync(request);

        // assert
        Assert.Equal("cloud-streams", credentials.Username);
        Assert.Equal("s3cr3t", credentials.Password);
        Assert.Equal($"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes("cloud-streams:s3cr3t"))}", request.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task Api_Keys_And_Bearer_Tokens_Should_Be_Sent_In_Their_Headers()
    {
        // arrange
        var resolver = new CredentialResolver(this.Store(), Substitute.For<IOAuth2TokenClient>());
        using var withKey = new HttpRequestMessage(HttpMethod.Post, "https://orders.example.com");
        using var withToken = new HttpRequestMessage(HttpMethod.Post, "https://orders.example.com");

        // act
        await (await resolver.ResolveAsync(new() { ApiKey = new() { Key = "key", Header = "X-Orders-Key" } }))!.AuthenticateAsync(withKey);
        await (await resolver.ResolveAsync(new() { Bearer = new() { Token = "token" } }))!.AuthenticateAsync(withToken);

        // assert
        Assert.Equal("key", withKey.Headers.GetValues("X-Orders-Key").Single());
        Assert.Equal("Bearer token", withToken.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task OAuth2_Tokens_Should_Be_Requested_With_The_Client_Credentials_And_Cached()
    {
        // arrange
        var handler = new StubHandler(_ => Task.FromResult(Json("""{ "access_token": "token-1", "expires_in": 3600 }""")));
        var client = new OAuth2TokenClient(Factory(handler), TimeProvider.System);
        var resolver = new CredentialResolver(this.Store(), client);
        var policy = new AuthenticationPolicy() { OAuth2 = new() { Authority = new("https://login.example.com/realm"), Client = new() { Id = "cloud-streams", Secret = "s3cr3t" }, Scopes = ["events", "orders"] } };

        // act
        var credentials = await resolver.ResolveAsync(policy);
        var first = await credentials!.GetTokenAsync();
        var second = await credentials.GetTokenAsync();

        // assert
        Assert.Equal("token-1", first.Value);
        Assert.Same(first, second);
        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal(new Uri("https://login.example.com/realm/oauth2/token"), request.RequestUri);
        Assert.Contains("grant_type=client_credentials", body);
        Assert.Contains("client_id=cloud-streams", body);
        Assert.Contains("client_secret=s3cr3t", body);
        Assert.Contains("scope=events+orders", body);
    }

    [Fact]
    public async Task OpenID_Connect_Should_Discover_The_Token_Endpoint()
    {
        // arrange
        var handler = new StubHandler(request => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("openid-configuration")
            ? Json("""{ "token_endpoint": "https://login.example.com/realm/protocol/openid-connect/token" }""")
            : Json("""{ "access_token": "token-1", "expires_in": 60 }""")));
        var client = new OAuth2TokenClient(Factory(handler), TimeProvider.System);

        // act
        var token = await client.GetTokenAsync(new() { Discover = true, Authority = new("https://login.example.com/realm"), ClientId = "cloud-streams", ClientSecret = "s3cr3t", ClientAuthentication = OAuth2ClientAuthenticationMethod.ClientSecretBasic });

        // assert
        Assert.Equal("token-1", token.Value);
        Assert.Equal(new Uri("https://login.example.com/realm/.well-known/openid-configuration"), handler.Requests[0].Request.RequestUri);
        Assert.Equal(new Uri("https://login.example.com/realm/protocol/openid-connect/token"), handler.Requests[1].Request.RequestUri);
        Assert.Equal("Basic", handler.Requests[1].Request.Headers.Authorization!.Scheme);
        Assert.DoesNotContain("client_secret", handler.Requests[1].Body);
    }

    [Fact]
    public async Task A_Refused_Token_Request_Should_Be_Reported_With_The_Server_Reason()
    {
        // arrange
        var handler = new StubHandler(_ => Task.FromResult(Json("""{ "error": "invalid_client", "error_description": "Invalid client credentials" }""", HttpStatusCode.Unauthorized)));
        var client = new OAuth2TokenClient(Factory(handler), TimeProvider.System);

        // act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetTokenAsync(new() { Authority = new("https://login.example.com"), ClientId = "cloud-streams", ClientSecret = "wrong" }));

        // assert
        Assert.Contains("Invalid client credentials", ex.Message);
    }

    [Fact]
    public async Task Vault_Secrets_Should_Be_Read_From_The_Kv_V2_Engine_After_Logging_In_With_An_AppRole()
    {
        // arrange
        var handler = new StubHandler(request => Task.FromResult(request.Method == HttpMethod.Post
            ? Json("""{ "auth": { "client_token": "vault-token", "lease_duration": 3600 } }""")
            : Json("""{ "data": { "data": { "username": "cloud-streams", "password": "s3cr3t" }, "metadata": { "version": 3 } } }""")));
        var options = Options(o => o.Vault = new() { Address = new("https://vault:8200"), Prefix = "cloud-streams", Authentication = new() { Method = "approle", RoleId = "role", SecretId = "secret" } });
        var provider = new VaultSecretProvider(Factory(handler), options, TimeProvider.System);

        // act
        var secret = await provider.GetAsync("orders-kafka");
        await provider.GetAsync("orders-kafka");

        // assert
        Assert.Equal("s3cr3t", secret!.Get("password"));
        Assert.Equal(new Uri("https://vault:8200/v1/auth/approle/login"), handler.Requests[0].Request.RequestUri);
        Assert.Equal(new Uri("https://vault:8200/v1/secret/data/cloud-streams/orders-kafka"), handler.Requests[1].Request.RequestUri);
        Assert.Equal("vault-token", handler.Requests[1].Request.Headers.GetValues("X-Vault-Token").Single());
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task Kubernetes_Secrets_Should_Only_Be_Read_If_They_Are_Labelled_For_Cloud_Streams()
    {
        // arrange
        var serviceAccount = Directory.CreateDirectory(Path.Combine(this._directory, "serviceaccount")).FullName;
        File.WriteAllText(Path.Combine(serviceAccount, "token"), "service-account-token");
        File.WriteAllText(Path.Combine(serviceAccount, "namespace"), "cloud-streams");
        var handler = new StubHandler(request => Task.FromResult(Json(new JsonObject()
        {
            ["metadata"] = new JsonObject() { ["labels"] = request.RequestUri!.AbsolutePath.EndsWith("/labelled") ? new JsonObject() { ["cloud-streams.io/secret"] = "true" } : new JsonObject() },
            ["data"] = new JsonObject() { ["password"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("s3cr3t")) }
        }.ToJsonString())));
        var options = Options(o => o.Kubernetes = new() { Server = new("https://kubernetes:6443/"), ServiceAccountDirectory = serviceAccount });
        var provider = new KubernetesSecretProvider(Factory(handler), options);

        // act
        var labelled = await provider.GetAsync("labelled");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync("unlabelled"));

        // assert
        Assert.Equal("s3cr3t", labelled!.Get("password"));
        Assert.Equal(new Uri("https://kubernetes:6443/api/v1/namespaces/cloud-streams/secrets/labelled"), handler.Requests[0].Request.RequestUri);
        Assert.Equal("Bearer service-account-token", handler.Requests[0].Request.Headers.Authorization!.ToString());
        Assert.Contains("cloud-streams.io/secret=true", ex.Message);
    }

}
