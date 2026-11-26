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

using CloudStreams.Core.Resources;
using CloudStreams.Core.Transports.Secrets;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace CloudStreams.Core.Transports.Authentication;

/// <summary>
/// Represents an access token, and when it expires
/// </summary>
/// <param name="Value">The token</param>
/// <param name="ExpiresAt">When the token expires</param>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>
/// Represents the credentials used to authenticate to an endpoint, resolved from its <see cref="AuthenticationPolicy"/> and the secret it uses, if any
/// </summary>
/// <remarks>Tokens are obtained when needed, so that those of OAuth2 and OpenID Connect are renewed before they expire</remarks>
public sealed class TransportCredentials
{

    readonly Func<CancellationToken, Task<AccessToken>>? _tokenFactory;

    TransportCredentials(string scheme, string fingerprint, Func<CancellationToken, Task<AccessToken>>? tokenFactory = null)
    {
        this.Scheme = scheme;
        this.Fingerprint = fingerprint;
        this._tokenFactory = tokenFactory;
    }

    /// <summary>
    /// Gets the scheme of the credentials. See <see cref="AuthenticationScheme"/>
    /// </summary>
    public string Scheme { get; }

    /// <summary>
    /// Gets a hash of the credentials, which identifies them without revealing them, e.g. to tell whether a connection must be opened anew
    /// </summary>
    public string Fingerprint { get; }

    /// <summary>
    /// Gets the user name, with the <c>Basic</c> scheme
    /// </summary>
    public string? Username { get; private init; }

    /// <summary>
    /// Gets the password, with the <c>Basic</c> scheme
    /// </summary>
    public string? Password { get; private init; }

    /// <summary>
    /// Gets the API key, with the API key scheme
    /// </summary>
    public string? ApiKey { get; private init; }

    /// <summary>
    /// Gets the name of the header that conveys the API key, with the API key scheme
    /// </summary>
    public string ApiKeyHeader { get; private init; } = ApiKeyAuthenticationPolicy.DefaultHeader;

    /// <summary>
    /// Gets a boolean indicating whether the credentials are a token: a bearer token, or one obtained with OAuth2 or OpenID Connect
    /// </summary>
    public bool IsToken => this._tokenFactory != null;

    /// <summary>
    /// Gets the current token, obtaining a new one if needed
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The current token</returns>
    /// <exception cref="InvalidOperationException">The credentials are not a token, or no token could be obtained</exception>
    public Task<AccessToken> GetTokenAsync(CancellationToken cancellationToken = default) => this._tokenFactory?.Invoke(cancellationToken) ?? throw new InvalidOperationException($"'{this.Scheme}' credentials are not a token");

    /// <summary>
    /// Adds the credentials to the specified HTTP request
    /// </summary>
    /// <param name="request">The request to authenticate</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public async Task AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (this.IsToken) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", (await this.GetTokenAsync(cancellationToken).ConfigureAwait(false)).Value);
        else if (this.Scheme == AuthenticationScheme.Basic) request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{this.Username}:{this.Password}")));
        else if (this.Scheme == AuthenticationScheme.ApiKey) request.Headers.TryAddWithoutValidation(this.ApiKeyHeader, this.ApiKey);
    }

    /// <summary>
    /// Creates new <c>Basic</c> credentials
    /// </summary>
    public static TransportCredentials Basic(string username, string password) => new(AuthenticationScheme.Basic, Hash(AuthenticationScheme.Basic, username, password)) { Username = username, Password = password };

    /// <summary>
    /// Creates new credentials that are a fixed bearer token
    /// </summary>
    public static TransportCredentials Bearer(string token) => new(AuthenticationScheme.Bearer, Hash(AuthenticationScheme.Bearer, token), _ => Task.FromResult(new AccessToken(token, DateTimeOffset.MaxValue)));

    /// <summary>
    /// Creates new API key credentials
    /// </summary>
    public static TransportCredentials ApiKeyCredentials(string key, string? header) => new(AuthenticationScheme.ApiKey, Hash(AuthenticationScheme.ApiKey, key, header ?? string.Empty)) { ApiKey = key, ApiKeyHeader = string.IsNullOrWhiteSpace(header) ? ApiKeyAuthenticationPolicy.DefaultHeader : header };

    /// <summary>
    /// Creates new credentials whose tokens are obtained when needed, with OAuth2 or OpenID Connect
    /// </summary>
    /// <param name="scheme">The scheme of the credentials</param>
    /// <param name="identity">Values that identify the client, which the fingerprint is computed from</param>
    /// <param name="tokenFactory">The function that gets the current token</param>
    public static TransportCredentials Token(string scheme, IEnumerable<string?> identity, Func<CancellationToken, Task<AccessToken>> tokenFactory) => new(scheme, Hash([scheme, .. identity]), tokenFactory);

    static string Hash(params string?[] values) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', values.Select(v => v ?? string.Empty)))))[..32];

}

/// <summary>
/// Defines the fundamentals of the service that resolves the credentials of endpoints
/// </summary>
public interface ICredentialResolver
{

    /// <summary>
    /// Resolves the credentials the specified policy describes, reading the secret it uses, if any
    /// </summary>
    /// <param name="authentication">The policy to resolve the credentials of</param>
    /// <param name="namespace">The namespace of the resource that defines the policy, whose secrets it may use</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The credentials, or null if there is no policy</returns>
    /// <exception cref="InvalidOperationException">The policy is invalid, or its secret cannot be read</exception>
    Task<TransportCredentials?> ResolveAsync(AuthenticationPolicy? authentication, string? @namespace, CancellationToken cancellationToken = default);

}

/// <summary>
/// Represents the default implementation of the <see cref="ICredentialResolver"/>
/// </summary>
/// <param name="secrets">The service used to read secrets</param>
/// <param name="tokens">The service used to obtain OAuth2 and OpenID Connect tokens</param>
public class CredentialResolver(ISecretStore secrets, IOAuth2TokenClient tokens)
    : ICredentialResolver
{

    /// <inheritdoc/>
    public virtual async Task<TransportCredentials?> ResolveAsync(AuthenticationPolicy? authentication, string? @namespace, CancellationToken cancellationToken = default)
    {
        if (authentication == null) return null;
        var policy = authentication.Policy ?? throw new InvalidOperationException("An authentication policy must set exactly one of basic, bearer, apiKey, oauth2 or oidc");
        var secret = policy.Use == null ? null : await secrets.GetAsync(policy.Use, @namespace, cancellationToken).ConfigureAwait(false);
        string Required(string? inline, string key) => (secret == null ? inline : secret.Get(key) ?? inline) ?? throw new InvalidOperationException(secret == null ? $"The {authentication.Scheme} authentication requires '{key}'" : $"The secret '{policy.Use}' has no '{key}', which the {authentication.Scheme} authentication requires");
        string? Optional(string? inline, string key) => secret?.Get(key) ?? inline;
        switch (policy)
        {
            case BasicAuthenticationPolicy basic:
                return TransportCredentials.Basic(Required(basic.Username, "username"), Required(basic.Password, "password"));
            case BearerAuthenticationPolicy bearer:
                return TransportCredentials.Bearer(Required(bearer.Token, "token"));
            case ApiKeyAuthenticationPolicy apiKey:
                return TransportCredentials.ApiKeyCredentials(Required(apiKey.Key, "key"), Optional(apiKey.Header, "header"));
            case OAuth2AuthenticationPolicyBase oauth2:
                static List<string>? Split(string? value) => value?.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                var request = new OAuth2TokenRequest()
                {
                    Discover = oauth2 is OidcAuthenticationPolicy,
                    Authority = new(Required(oauth2.Authority?.OriginalString, "authority")),
                    TokenEndpoint = Optional((oauth2 as OAuth2AuthenticationPolicy)?.Endpoints?.Token, "endpoints.token"),
                    Grant = Optional(oauth2.Grant, "grant") ?? OAuth2GrantType.ClientCredentials,
                    ClientId = Required(oauth2.Client?.Id, "client.id"),
                    ClientSecret = Optional(oauth2.Client?.Secret, "client.secret"),
                    ClientAuthentication = Optional(oauth2.Client?.Authentication, "client.authentication") ?? OAuth2ClientAuthenticationMethod.ClientSecretPost,
                    Scopes = Split(secret?.Get("scopes")) ?? oauth2.Scopes,
                    Audiences = Split(secret?.Get("audiences")) ?? oauth2.Audiences,
                    Username = Optional(oauth2.Username, "username"),
                    Password = Optional(oauth2.Password, "password")
                };
                return TransportCredentials.Token(authentication.Scheme!, [request.Authority.OriginalString, request.TokenEndpoint, request.Grant, request.ClientId, request.ClientSecret, request.Username, request.Password,
                    string.Join(' ', request.Scopes ?? []), string.Join(' ', request.Audiences ?? [])], token => tokens.GetTokenAsync(request, token));
            default:
                throw new NotSupportedException($"The '{authentication.Scheme}' authentication is not supported");
        }
    }

}
