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

namespace CloudStreams.Core.Api.Configuration;

/// <summary>
/// Represents the options used to configure the authentication of a Cloud Streams API
/// </summary>
/// <remarks>
/// The dashboard (and the resource API, hubs and MCP endpoint it relies on) and the cloud event ingestion endpoint are secured independently, so that
/// the ingestion endpoint can remain open to external publishers while the dashboard requires users to sign in, or vice versa (see #130).
/// Both are disabled by default, which preserves the previous behaviour of delegating authentication to the infrastructure (e.g. the ingress)
/// </remarks>
public class CloudStreamsAuthenticationOptions
{

    /// <summary>
    /// Gets the name of the configuration section the options are bound to
    /// </summary>
    public const string ConfigurationSection = "Authentication";

    /// <summary>
    /// Gets/sets the options used to configure the OpenID Connect provider used to authenticate users and to validate bearer tokens
    /// </summary>
    public virtual OidcAuthenticationOptions Oidc { get; set; } = new();

    /// <summary>
    /// Gets/sets the options used to configure the authentication of the dashboard and of the management API
    /// </summary>
    public virtual DashboardAuthenticationOptions Dashboard { get; set; } = new();

    /// <summary>
    /// Gets/sets the options used to configure the authentication of the cloud event ingestion endpoint
    /// </summary>
    public virtual IngestionAuthenticationOptions Ingestion { get; set; } = new();

    /// <summary>
    /// Validates the options
    /// </summary>
    /// <returns>A new <see cref="IEnumerable{T}"/> containing the validation errors, if any</returns>
    public virtual IEnumerable<string> Validate()
    {
        var oidcConfigured = !string.IsNullOrWhiteSpace(this.Oidc.Authority);
        if (this.Dashboard.Enabled)
        {
            if (!oidcConfigured) yield return $"'{ConfigurationSection}:Oidc:Authority' is required when '{ConfigurationSection}:Dashboard:Enabled' is true";
            if (string.IsNullOrWhiteSpace(this.Oidc.ClientId)) yield return $"'{ConfigurationSection}:Oidc:ClientId' is required when '{ConfigurationSection}:Dashboard:Enabled' is true";
        }
        if (this.Ingestion.Enabled && !oidcConfigured && (this.Ingestion.ApiKeys == null || this.Ingestion.ApiKeys.Count(k => !string.IsNullOrWhiteSpace(k)) < 1))
        {
            yield return $"Either '{ConfigurationSection}:Oidc:Authority' or at least one '{ConfigurationSection}:Ingestion:ApiKeys' is required when '{ConfigurationSection}:Ingestion:Enabled' is true";
        }
    }

}

/// <summary>
/// Represents the options used to configure an OpenID Connect provider
/// </summary>
public class OidcAuthenticationOptions
{

    /// <summary>
    /// Gets/sets the uri of the OpenID Connect authority, e.g. <c>https://keycloak.example.com/realms/cloud-streams</c>
    /// </summary>
    public virtual string? Authority { get; set; }

    /// <summary>
    /// Gets/sets the id of the client the dashboard signs users in with
    /// </summary>
    public virtual string? ClientId { get; set; }

    /// <summary>
    /// Gets/sets the secret of the client the dashboard signs users in with, if it is a confidential client. PKCE is always used
    /// </summary>
    public virtual string? ClientSecret { get; set; }

    /// <summary>
    /// Gets/sets the scopes to request when signing users in. Defaults to <c>openid</c> and <c>profile</c>
    /// </summary>
    public virtual List<string> Scopes { get; set; } = [];

    /// <summary>
    /// Gets/sets the audience bearer tokens must have been issued for. Strongly recommended: if not set, any token issued by the authority is accepted
    /// </summary>
    public virtual string? Audience { get; set; }

    /// <summary>
    /// Gets/sets a boolean indicating whether or not the authority's metadata must be retrieved over HTTPS. Defaults to true
    /// </summary>
    public virtual bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Gets/sets the type of the claim used as the user's name. Defaults to <c>preferred_username</c>
    /// </summary>
    public virtual string NameClaimType { get; set; } = "preferred_username";

    /// <summary>
    /// Gets/sets the type of the claim used to convey the user's roles, if any. Defaults to <c>roles</c>
    /// </summary>
    public virtual string RoleClaimType { get; set; } = "roles";

}

/// <summary>
/// Represents the options used to configure the authentication of the dashboard and of the management API
/// </summary>
public class DashboardAuthenticationOptions
{

    /// <summary>
    /// Gets/sets a boolean indicating whether or not users must sign in to access the dashboard and the management API. Defaults to false
    /// </summary>
    /// <remarks>
    /// When enabled, browsers sign in with the OpenID Connect authorization code flow and are then authenticated by a cookie, while other clients
    /// (e.g. scripts or monitoring tools) authenticate with bearer tokens issued by the same authority
    /// </remarks>
    public virtual bool Enabled { get; set; }

    /// <summary>
    /// Gets/sets the roles, at least one of which users must have to access the dashboard and the management API, if any
    /// </summary>
    public virtual List<string> RequiredRoles { get; set; } = [];

}

/// <summary>
/// Represents the options used to configure the authentication of the cloud event ingestion endpoint
/// </summary>
public class IngestionAuthenticationOptions
{

    /// <summary>
    /// Gets the name of the default HTTP header used to convey an API key
    /// </summary>
    public const string DefaultApiKeyHeader = "X-Api-Key";

    /// <summary>
    /// Gets/sets a boolean indicating whether or not publishers must authenticate to publish cloud events. Defaults to false
    /// </summary>
    /// <remarks>Publishers authenticate either with a bearer token issued by the configured OpenID Connect authority, or with one of the configured API keys</remarks>
    public virtual bool Enabled { get; set; }

    /// <summary>
    /// Gets/sets the API keys publishers may authenticate with, if any
    /// </summary>
    public virtual List<string> ApiKeys { get; set; } = [];

    /// <summary>
    /// Gets/sets the name of the HTTP header used to convey an API key. Defaults to <c>X-Api-Key</c>
    /// </summary>
    public virtual string ApiKeyHeader { get; set; } = DefaultApiKeyHeader;

}
