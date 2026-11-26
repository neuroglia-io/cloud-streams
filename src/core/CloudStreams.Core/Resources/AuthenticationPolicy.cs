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

namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents how to authenticate to an <see cref="Endpoint"/>, after the authentication policies of the Serverless Workflow specification: exactly one of its
/// properties must be set (see #218)
/// </summary>
/// <remarks>
/// <para>Each policy either holds its values, which anyone allowed to read the resource can see, or names the secret that holds them with <c>use</c>. Secrets are
/// read by the broker or the gateway from the providers it is configured with, such as files, environment variables, Kubernetes or Vault.</para>
/// <para><see cref="ApiKey"/> is an extension to the specification, since Cloud Streams gateways authenticate publishers with API keys.</para>
/// </remarks>
[DataContract]
public record AuthenticationPolicy
{

    /// <summary>
    /// Gets/sets the configuration of the <c>Basic</c> authentication, if any
    /// </summary>
    [DataMember(Order = 1, Name = "basic"), JsonPropertyOrder(1), JsonPropertyName("basic"), YamlMember(Order = 1, Alias = "basic")]
    public virtual BasicAuthenticationPolicy? Basic { get; set; }

    /// <summary>
    /// Gets/sets the configuration of the <c>Bearer</c> authentication, if any
    /// </summary>
    [DataMember(Order = 2, Name = "bearer"), JsonPropertyOrder(2), JsonPropertyName("bearer"), YamlMember(Order = 2, Alias = "bearer")]
    public virtual BearerAuthenticationPolicy? Bearer { get; set; }

    /// <summary>
    /// Gets/sets the configuration of the API key authentication, if any
    /// </summary>
    [DataMember(Order = 3, Name = "apiKey"), JsonPropertyOrder(3), JsonPropertyName("apiKey"), YamlMember(Order = 3, Alias = "apiKey")]
    public virtual ApiKeyAuthenticationPolicy? ApiKey { get; set; }

    /// <summary>
    /// Gets/sets the configuration of the OAuth2 authentication, if any
    /// </summary>
    [DataMember(Order = 4, Name = "oauth2"), JsonPropertyOrder(4), JsonPropertyName("oauth2"), YamlMember(Order = 4, Alias = "oauth2")]
    public virtual OAuth2AuthenticationPolicy? OAuth2 { get; set; }

    /// <summary>
    /// Gets/sets the configuration of the OpenID Connect authentication, if any
    /// </summary>
    [DataMember(Order = 5, Name = "oidc"), JsonPropertyOrder(5), JsonPropertyName("oidc"), YamlMember(Order = 5, Alias = "oidc")]
    public virtual OidcAuthenticationPolicy? Oidc { get; set; }

    /// <summary>
    /// Gets the scheme of the policy, if exactly one is configured. See <see cref="AuthenticationScheme"/>
    /// </summary>
    [IgnoreDataMember, JsonIgnore, YamlIgnore]
    public virtual string? Scheme
    {
        get
        {
            var schemes = new List<string>();
            if (this.Basic != null) schemes.Add(AuthenticationScheme.Basic);
            if (this.Bearer != null) schemes.Add(AuthenticationScheme.Bearer);
            if (this.ApiKey != null) schemes.Add(AuthenticationScheme.ApiKey);
            if (this.OAuth2 != null) schemes.Add(AuthenticationScheme.OAuth2);
            if (this.Oidc != null) schemes.Add(AuthenticationScheme.Oidc);
            return schemes.Count == 1 ? schemes[0] : null;
        }
    }

    /// <summary>
    /// Gets the configured policy, if exactly one is
    /// </summary>
    [IgnoreDataMember, JsonIgnore, YamlIgnore]
    public virtual SecretBasedAuthenticationPolicy? Policy => this.Scheme switch
    {
        AuthenticationScheme.Basic => this.Basic,
        AuthenticationScheme.Bearer => this.Bearer,
        AuthenticationScheme.ApiKey => this.ApiKey,
        AuthenticationScheme.OAuth2 => this.OAuth2,
        AuthenticationScheme.Oidc => this.Oidc,
        _ => null
    };

}

/// <summary>
/// Enumerates the supported authentication schemes
/// </summary>
public static class AuthenticationScheme
{

    /// <summary>
    /// Gets the <c>Basic</c> scheme, a user name and a password
    /// </summary>
    public const string Basic = "basic";
    /// <summary>
    /// Gets the <c>Bearer</c> scheme, a token
    /// </summary>
    public const string Bearer = "bearer";
    /// <summary>
    /// Gets the API key scheme, a key sent in a header
    /// </summary>
    public const string ApiKey = "apiKey";
    /// <summary>
    /// Gets the OAuth2 scheme, a token obtained from an authorization server
    /// </summary>
    public const string OAuth2 = "oauth2";
    /// <summary>
    /// Gets the OpenID Connect scheme, a token obtained from an OpenID Connect provider, whose endpoints are discovered
    /// </summary>
    public const string Oidc = "oidc";

}

/// <summary>
/// Represents the base class of authentication policies, whose values may be held by a secret
/// </summary>
[DataContract]
public abstract record SecretBasedAuthenticationPolicy
{

    /// <summary>
    /// Gets/sets the reference to the secret that holds the policy's values, if any. Values the secret holds take precedence over those of the policy, which may hold
    /// the others, such as the authority and the scopes of an OAuth2 policy
    /// </summary>
    /// <remarks>The secret's keys are the names of the policy's properties, e.g. <c>username</c> and <c>password</c>, or <c>client.id</c> and <c>client.secret</c></remarks>
    [DataMember(Order = 0, Name = "use"), JsonPropertyOrder(0), JsonPropertyName("use"), YamlMember(Order = 0, Alias = "use")]
    public virtual SecretReference? Use { get; set; }

}

/// <summary>
/// Represents the configuration of the <c>Basic</c> authentication
/// </summary>
[DataContract]
public record BasicAuthenticationPolicy
    : SecretBasedAuthenticationPolicy
{

    /// <summary>
    /// Gets/sets the user name
    /// </summary>
    [DataMember(Order = 1, Name = "username"), JsonPropertyOrder(1), JsonPropertyName("username"), YamlMember(Order = 1, Alias = "username")]
    public virtual string? Username { get; set; }

    /// <summary>
    /// Gets/sets the password. Not recommended: anyone allowed to read the resource can see it. Use a secret instead
    /// </summary>
    [DataMember(Order = 2, Name = "password"), JsonPropertyOrder(2), JsonPropertyName("password"), YamlMember(Order = 2, Alias = "password")]
    public virtual string? Password { get; set; }

}

/// <summary>
/// Represents the configuration of the <c>Bearer</c> authentication
/// </summary>
[DataContract]
public record BearerAuthenticationPolicy
    : SecretBasedAuthenticationPolicy
{

    /// <summary>
    /// Gets/sets the token. Not recommended: anyone allowed to read the resource can see it. Use a secret instead
    /// </summary>
    [DataMember(Order = 1, Name = "token"), JsonPropertyOrder(1), JsonPropertyName("token"), YamlMember(Order = 1, Alias = "token")]
    public virtual string? Token { get; set; }

}

/// <summary>
/// Represents the configuration of the API key authentication, an extension to the Serverless Workflow specification
/// </summary>
[DataContract]
public record ApiKeyAuthenticationPolicy
    : SecretBasedAuthenticationPolicy
{

    /// <summary>
    /// Gets the name of the header that conveys the key by default
    /// </summary>
    public const string DefaultHeader = "X-Api-Key";

    /// <summary>
    /// Gets/sets the key. Not recommended: anyone allowed to read the resource can see it. Use a secret instead
    /// </summary>
    [DataMember(Order = 1, Name = "key"), JsonPropertyOrder(1), JsonPropertyName("key"), YamlMember(Order = 1, Alias = "key")]
    public virtual string? Key { get; set; }

    /// <summary>
    /// Gets/sets the name of the header that conveys the key. Defaults to <c>X-Api-Key</c>
    /// </summary>
    [DataMember(Order = 2, Name = "header"), JsonPropertyOrder(2), JsonPropertyName("header"), YamlMember(Order = 2, Alias = "header")]
    public virtual string? Header { get; set; }

}

/// <summary>
/// Represents the base of the OAuth2 and OpenID Connect authentication policies, which obtain a token from an authorization server
/// </summary>
[DataContract]
public abstract record OAuth2AuthenticationPolicyBase
    : SecretBasedAuthenticationPolicy
{

    /// <summary>
    /// Gets/sets the address of the authorization server
    /// </summary>
    [DataMember(Order = 1, Name = "authority"), JsonPropertyOrder(1), JsonPropertyName("authority"), YamlMember(Order = 1, Alias = "authority")]
    public virtual Uri? Authority { get; set; }

    /// <summary>
    /// Gets/sets the grant used to obtain tokens. See <see cref="OAuth2GrantType"/>. Defaults to <c>client_credentials</c>
    /// </summary>
    [DataMember(Order = 2, Name = "grant"), JsonPropertyOrder(2), JsonPropertyName("grant"), YamlMember(Order = 2, Alias = "grant")]
    public virtual string? Grant { get; set; }

    /// <summary>
    /// Gets/sets the client that obtains tokens
    /// </summary>
    [DataMember(Order = 3, Name = "client"), JsonPropertyOrder(3), JsonPropertyName("client"), YamlMember(Order = 3, Alias = "client")]
    public virtual OAuth2Client? Client { get; set; }

    /// <summary>
    /// Gets/sets the scopes to request, if any
    /// </summary>
    [DataMember(Order = 4, Name = "scopes"), JsonPropertyOrder(4), JsonPropertyName("scopes"), YamlMember(Order = 4, Alias = "scopes")]
    public virtual List<string>? Scopes { get; set; }

    /// <summary>
    /// Gets/sets the audiences to request, if any
    /// </summary>
    [DataMember(Order = 5, Name = "audiences"), JsonPropertyOrder(5), JsonPropertyName("audiences"), YamlMember(Order = 5, Alias = "audiences")]
    public virtual List<string>? Audiences { get; set; }

    /// <summary>
    /// Gets/sets the user name, with the <c>password</c> grant
    /// </summary>
    [DataMember(Order = 6, Name = "username"), JsonPropertyOrder(6), JsonPropertyName("username"), YamlMember(Order = 6, Alias = "username")]
    public virtual string? Username { get; set; }

    /// <summary>
    /// Gets/sets the password, with the <c>password</c> grant. Not recommended: anyone allowed to read the resource can see it. Use a secret instead
    /// </summary>
    [DataMember(Order = 7, Name = "password"), JsonPropertyOrder(7), JsonPropertyName("password"), YamlMember(Order = 7, Alias = "password")]
    public virtual string? Password { get; set; }

}

/// <summary>
/// Represents the configuration of the OAuth2 authentication
/// </summary>
[DataContract]
public record OAuth2AuthenticationPolicy
    : OAuth2AuthenticationPolicyBase
{

    /// <summary>
    /// Gets/sets the endpoints of the authorization server, relative to its <see cref="OAuth2AuthenticationPolicyBase.Authority"/>
    /// </summary>
    [DataMember(Order = 8, Name = "endpoints"), JsonPropertyOrder(8), JsonPropertyName("endpoints"), YamlMember(Order = 8, Alias = "endpoints")]
    public virtual OAuth2Endpoints? Endpoints { get; set; }

}

/// <summary>
/// Represents the configuration of the OpenID Connect authentication, whose token endpoint is discovered from the authority's
/// <c>.well-known/openid-configuration</c>
/// </summary>
[DataContract]
public record OidcAuthenticationPolicy
    : OAuth2AuthenticationPolicyBase
{

}

/// <summary>
/// Represents the endpoints of an OAuth2 authorization server
/// </summary>
[DataContract]
public record OAuth2Endpoints
{

    /// <summary>
    /// Gets the path of the token endpoint by default
    /// </summary>
    public const string DefaultToken = "/oauth2/token";

    /// <summary>
    /// Gets/sets the path, relative to the authority, or the address of the token endpoint. Defaults to <c>/oauth2/token</c>
    /// </summary>
    [DataMember(Order = 1, Name = "token"), JsonPropertyOrder(1), JsonPropertyName("token"), YamlMember(Order = 1, Alias = "token")]
    public virtual string? Token { get; set; }

}

/// <summary>
/// Represents an OAuth2 client
/// </summary>
[DataContract]
public record OAuth2Client
{

    /// <summary>
    /// Gets/sets the id of the client
    /// </summary>
    [DataMember(Order = 1, Name = "id"), JsonPropertyOrder(1), JsonPropertyName("id"), YamlMember(Order = 1, Alias = "id")]
    public virtual string? Id { get; set; }

    /// <summary>
    /// Gets/sets the secret of the client. Not recommended: anyone allowed to read the resource can see it. Use a secret instead
    /// </summary>
    [DataMember(Order = 2, Name = "secret"), JsonPropertyOrder(2), JsonPropertyName("secret"), YamlMember(Order = 2, Alias = "secret")]
    public virtual string? Secret { get; set; }

    /// <summary>
    /// Gets/sets how the client authenticates to the token endpoint. See <see cref="OAuth2ClientAuthenticationMethod"/>. Defaults to <c>client_secret_post</c>
    /// </summary>
    [DataMember(Order = 3, Name = "authentication"), JsonPropertyOrder(3), JsonPropertyName("authentication"), YamlMember(Order = 3, Alias = "authentication")]
    public virtual string? Authentication { get; set; }

}

/// <summary>
/// Enumerates the supported OAuth2 grants
/// </summary>
public static class OAuth2GrantType
{

    /// <summary>
    /// Gets the client credentials grant, with which the client authenticates as itself
    /// </summary>
    public const string ClientCredentials = "client_credentials";
    /// <summary>
    /// Gets the resource owner password grant, with which the client authenticates as a user
    /// </summary>
    public const string Password = "password";

    /// <summary>
    /// Gets all the supported grants
    /// </summary>
    /// <returns>A new <see cref="IEnumerable{T}"/> containing the supported grants</returns>
    public static IEnumerable<string> AsEnumerable()
    {
        yield return ClientCredentials;
        yield return Password;
    }

}

/// <summary>
/// Enumerates the supported methods of authenticating an OAuth2 client to the token endpoint
/// </summary>
public static class OAuth2ClientAuthenticationMethod
{

    /// <summary>
    /// Gets the method that sends the client's credentials in the body of the request
    /// </summary>
    public const string ClientSecretPost = "client_secret_post";
    /// <summary>
    /// Gets the method that sends the client's credentials as <c>Basic</c> authorization
    /// </summary>
    public const string ClientSecretBasic = "client_secret_basic";

    /// <summary>
    /// Gets all the supported methods
    /// </summary>
    /// <returns>A new <see cref="IEnumerable{T}"/> containing the supported methods</returns>
    public static IEnumerable<string> AsEnumerable()
    {
        yield return ClientSecretPost;
        yield return ClientSecretBasic;
    }

}
