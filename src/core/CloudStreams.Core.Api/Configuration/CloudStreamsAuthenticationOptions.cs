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

using System.Security.Claims;

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
        if (oidcConfigured && this.Oidc.RequireAudience && string.IsNullOrWhiteSpace(this.Oidc.Audience))
        {
            yield return $"'{ConfigurationSection}:Oidc:Audience' is required when '{ConfigurationSection}:Oidc:RequireAudience' is true";
        }
        if (this.Ingestion.Enabled && !oidcConfigured && this.Ingestion.GetApiKeys().Count < 1)
        {
            yield return $"Either '{ConfigurationSection}:Oidc:Authority', at least one '{ConfigurationSection}:Ingestion:ApiKeys' or at least one '{ConfigurationSection}:Ingestion:Publishers' is required when '{ConfigurationSection}:Ingestion:Enabled' is true";
        }
        var publishers = this.Ingestion.Publishers ?? [];
        for (var i = 0; i < publishers.Count; i++)
        {
            var publisher = publishers[i];
            var path = $"{ConfigurationSection}:Ingestion:Publishers:{i}";
            if (string.IsNullOrWhiteSpace(publisher.Name)) yield return $"'{path}:Name' is required";
            if (string.IsNullOrWhiteSpace(publisher.ApiKey)) yield return $"'{path}:ApiKey' is required";
            // a publisher bound to no source could publish nothing: far more likely a mistake than an intent, and it must never be mistaken for an unrestricted key
            if ((publisher.Sources ?? []).Count(s => !string.IsNullOrWhiteSpace(s)) < 1) yield return $"'{path}:Sources' must contain at least one source pattern";
        }
        var duplicateKeys = this.Ingestion.GetApiKeys().GroupBy(k => k.Key, StringComparer.Ordinal).Count(g => g.Count() > 1);
        if (duplicateKeys > 0) yield return $"{duplicateKeys} API key(s) are configured more than once in '{ConfigurationSection}:Ingestion:ApiKeys' and '{ConfigurationSection}:Ingestion:Publishers': each key must identify a single publisher";
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
    /// Gets/sets the address of the authority's discovery document, if it must be retrieved from another address than the <see cref="Authority"/>'s. Useful when the
    /// application reaches the authority through an internal address, such as a container's name, while users reach it through a public one
    /// </summary>
    public virtual string? MetadataAddress { get; set; }

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
    /// Gets/sets a boolean indicating whether or not an <see cref="Audience"/> is required, in which case the application refuses to start without one. Defaults to
    /// false, in which case a warning is logged at startup when no audience is configured (see #145)
    /// </summary>
    public virtual bool RequireAudience { get; set; }

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
    /// Gets/sets the roles that grant users access to the dashboard and to the management API, and what they may do with it
    /// </summary>
    public virtual DashboardRoleOptions Roles { get; set; } = new();

    /// <summary>
    /// Gets/sets the roles, at least one of which users must have to access the dashboard and the management API, if any
    /// </summary>
    /// <remarks>Deprecated: equivalent to <see cref="DashboardRoleOptions.Admins"/>, which it is added to</remarks>
    [Obsolete("Use 'Roles:Admins' instead")]
    public virtual List<string> RequiredRoles { get; set; } = [];

    /// <summary>
    /// Gets the level of access granted to the specified user
    /// </summary>
    /// <param name="user">The user to get the level of access of</param>
    /// <returns>The level of access granted to the specified user</returns>
    /// <remarks>
    /// When authentication is disabled, or when no role is configured, every user is an administrator, as before roles were introduced. Otherwise each level includes
    /// the ones below it, and authenticated users who have none of the configured roles have no access at all (see #147)
    /// </remarks>
    public virtual CloudStreamsAccessLevel GetAccessLevel(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!this.Enabled) return CloudStreamsAccessLevel.Administer;
        if (user.Identity?.IsAuthenticated != true) return CloudStreamsAccessLevel.None;
#pragma warning disable CS0618 // Type or member is obsolete
        var admins = Clean(this.Roles?.Admins).Concat(Clean(this.RequiredRoles)).ToList();
#pragma warning restore CS0618 // Type or member is obsolete
        var operators = Clean(this.Roles?.Operators).ToList();
        var readers = Clean(this.Roles?.Readers).ToList();
        if (admins.Count == 0 && operators.Count == 0 && readers.Count == 0) return CloudStreamsAccessLevel.Administer;
        if (admins.Any(user.IsInRole)) return CloudStreamsAccessLevel.Administer;
        if (operators.Any(user.IsInRole)) return CloudStreamsAccessLevel.Operate;
        if (readers.Any(user.IsInRole)) return CloudStreamsAccessLevel.Read;
        return CloudStreamsAccessLevel.None;
    }

    static IEnumerable<string> Clean(IEnumerable<string>? roles) => (roles ?? []).Where(r => !string.IsNullOrWhiteSpace(r));

}

/// <summary>
/// Represents the options used to configure the roles that grant access to the dashboard and to the management API
/// </summary>
/// <remarks>Roles are read from the claim configured by <see cref="OidcAuthenticationOptions.RoleClaimType"/></remarks>
public class DashboardRoleOptions
{

    /// <summary>
    /// Gets/sets the roles that allow users to get, list and watch resources, cloud events and partitions, including with the tools of the MCP endpoint
    /// </summary>
    public virtual List<string> Readers { get; set; } = [];

    /// <summary>
    /// Gets/sets the roles that allow users to resume and replay subscriptions, in addition to everything readers may do
    /// </summary>
    public virtual List<string> Operators { get; set; } = [];

    /// <summary>
    /// Gets/sets the roles that allow users to create, replace, patch and delete resources, and to patch their status, in addition to everything operators may do
    /// </summary>
    public virtual List<string> Admins { get; set; } = [];

}

/// <summary>
/// Enumerates the levels of access to the dashboard and to the management API, each of which includes the ones below it
/// </summary>
public enum CloudStreamsAccessLevel
{
    /// <summary>
    /// No access at all
    /// </summary>
    None = 0,
    /// <summary>
    /// Allows getting, listing and watching resources, cloud events and partitions
    /// </summary>
    Read = 1,
    /// <summary>
    /// Also allows resuming and replaying subscriptions
    /// </summary>
    Operate = 2,
    /// <summary>
    /// Also allows creating, replacing, patching and deleting resources, and patching their status
    /// </summary>
    Administer = 3
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
    /// Gets/sets the API keys publishers may authenticate with, if any. They may publish cloud events of any source: see <see cref="Publishers"/> to restrict them
    /// </summary>
    public virtual List<string> ApiKeys { get; set; } = [];

    /// <summary>
    /// Gets/sets the publishers that authenticate with an API key and may only publish cloud events of the sources they are bound to, if any
    /// </summary>
    public virtual List<IngestionPublisherOptions> Publishers { get; set; } = [];

    /// <summary>
    /// Gets/sets the type of the claim that lists the sources a bearer token may publish cloud events of, if any
    /// </summary>
    /// <remarks>
    /// When set, bearer tokens may only publish cloud events whose source matches one of the patterns the claim lists, as an array or as a space-separated string, and
    /// tokens without the claim may publish none. When not set, which is the default, bearer tokens may publish cloud events of any source
    /// </remarks>
    public virtual string? SourceClaimType { get; set; }

    /// <summary>
    /// Gets/sets the name of the HTTP header used to convey an API key. Defaults to <c>X-Api-Key</c>
    /// </summary>
    public virtual string ApiKeyHeader { get; set; } = DefaultApiKeyHeader;

    /// <summary>
    /// Gets all configured API keys, each with the publisher it identifies, if it is bound to sources
    /// </summary>
    /// <returns>A new <see cref="List{T}"/> containing the configured API keys, each with the publisher it identifies, if it is bound to sources</returns>
    public virtual List<KeyValuePair<string, IngestionPublisherOptions?>> GetApiKeys() =>
    [
        .. (this.ApiKeys ?? []).Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => new KeyValuePair<string, IngestionPublisherOptions?>(k, null)),
        .. (this.Publishers ?? []).Where(p => !string.IsNullOrWhiteSpace(p.ApiKey)).Select(p => new KeyValuePair<string, IngestionPublisherOptions?>(p.ApiKey!, p))
    ];

}

/// <summary>
/// Represents the options used to configure a publisher that may only publish cloud events of the sources it is bound to
/// </summary>
public class IngestionPublisherOptions
{

    /// <summary>
    /// Gets/sets the name of the publisher, used as the name of the authenticated user, for example in logs
    /// </summary>
    public virtual string? Name { get; set; }

    /// <summary>
    /// Gets/sets the API key the publisher authenticates with
    /// </summary>
    public virtual string? ApiKey { get; set; }

    /// <summary>
    /// Gets/sets the patterns of the sources the publisher may publish cloud events of, in which <c>*</c> matches any sequence of characters
    /// </summary>
    /// <remarks>See <see cref="CloudStreams.Core.CloudEventSourcePattern"/></remarks>
    public virtual List<string> Sources { get; set; } = [];

}
