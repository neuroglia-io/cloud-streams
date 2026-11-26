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
    public virtual CloudStreamsAccessLevel GetAccessLevel(ClaimsPrincipal user) => this.GetAccessLevel(user, null);

    /// <summary>
    /// Gets the level of access granted to the specified user, in the whole cluster or in the specified namespace
    /// </summary>
    /// <param name="user">The user to get the level of access of</param>
    /// <param name="namespace">The namespace to get the level of access in, if any. If not set, only the roles granted for the whole cluster count</param>
    /// <returns>The level of access granted to the specified user</returns>
    /// <remarks>
    /// The roles of <see cref="Roles"/> grant access to every namespace. Roles matching <see cref="DashboardRoleOptions.Namespaced"/> grant access to the namespace they name
    /// only, and the user is granted the highest of both (see #215)
    /// </remarks>
    public virtual CloudStreamsAccessLevel GetAccessLevel(ClaimsPrincipal user, string? @namespace)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!this.Enabled) return CloudStreamsAccessLevel.Administer;
        if (user.Identity?.IsAuthenticated != true) return CloudStreamsAccessLevel.None;
#pragma warning disable CS0618 // Type or member is obsolete
        var admins = Clean(this.Roles?.Admins).Concat(Clean(this.RequiredRoles)).ToList();
#pragma warning restore CS0618 // Type or member is obsolete
        var operators = Clean(this.Roles?.Operators).ToList();
        var readers = Clean(this.Roles?.Readers).ToList();
        var template = NamespacedRoleTemplate.TryParse(this.Roles?.Namespaced);
        if (admins.Count == 0 && operators.Count == 0 && readers.Count == 0 && template == null) return CloudStreamsAccessLevel.Administer;
        var level = CloudStreamsAccessLevel.None;
        if (admins.Any(user.IsInRole)) level = CloudStreamsAccessLevel.Administer;
        else if (operators.Any(user.IsInRole)) level = CloudStreamsAccessLevel.Operate;
        else if (readers.Any(user.IsInRole)) level = CloudStreamsAccessLevel.Read;
        if (level == CloudStreamsAccessLevel.Administer || template == null || string.IsNullOrWhiteSpace(@namespace)) return level;
        var grants = this.GetNamespaceGrants(user, template);
        return grants.TryGetValue(@namespace, out var namespaced) && namespaced > level ? namespaced : level;
    }

    /// <summary>
    /// Gets the namespaces the specified user may access at the specified level, or null if the user may access all of them
    /// </summary>
    /// <param name="user">The user to get the namespaces of</param>
    /// <param name="level">The minimum level of access</param>
    /// <returns>A new <see cref="IReadOnlySet{T}"/> containing the namespaces the user may access, which is empty if none, or null if all</returns>
    public virtual IReadOnlySet<string>? GetAccessibleNamespaces(ClaimsPrincipal user, CloudStreamsAccessLevel level)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (this.GetAccessLevel(user, null) >= level) return null;
        if (user.Identity?.IsAuthenticated != true) return new HashSet<string>();
        var template = NamespacedRoleTemplate.TryParse(this.Roles?.Namespaced);
        if (template == null) return new HashSet<string>();
        return this.GetNamespaceGrants(user, template).Where(g => g.Value >= level).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the levels of access granted to the specified user by namespaced roles, per namespace
    /// </summary>
    /// <param name="user">The user to get the grants of</param>
    /// <returns>A new <see cref="IReadOnlyDictionary{TKey, TValue}"/> containing the highest level of access granted per namespace, which is empty if none</returns>
    public virtual IReadOnlyDictionary<string, CloudStreamsAccessLevel> GetNamespaceAccessLevels(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!this.Enabled || user.Identity?.IsAuthenticated != true) return new Dictionary<string, CloudStreamsAccessLevel>();
        var template = NamespacedRoleTemplate.TryParse(this.Roles?.Namespaced);
        return template == null ? new Dictionary<string, CloudStreamsAccessLevel>() : this.GetNamespaceGrants(user, template);
    }

    /// <summary>
    /// Gets the levels of access granted to the specified user per namespace
    /// </summary>
    /// <param name="user">The user to get the grants of</param>
    /// <param name="template">The template of namespaced roles</param>
    /// <returns>A new <see cref="IReadOnlyDictionary{TKey, TValue}"/> containing the highest level of access granted per namespace</returns>
    protected virtual IReadOnlyDictionary<string, CloudStreamsAccessLevel> GetNamespaceGrants(ClaimsPrincipal user, NamespacedRoleTemplate template)
    {
        var grants = new Dictionary<string, CloudStreamsAccessLevel>(StringComparer.Ordinal);
        foreach (var identity in user.Identities.Where(i => i.IsAuthenticated))
        {
            foreach (var claim in identity.FindAll(identity.RoleClaimType))
            {
                if (!template.TryMatch(claim.Value, out var @namespace, out var level)) continue;
                if (!grants.TryGetValue(@namespace, out var existing) || level > existing) grants[@namespace] = level;
            }
        }
        return grants;
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

    /// <summary>
    /// Gets/sets the template of the roles that grant access to a single namespace, if any, e.g. <c>cloud-streams:{namespace}:{level}</c>
    /// </summary>
    /// <remarks>
    /// The template must contain the <c>{namespace}</c> and <c>{level}</c> placeholders. The level is one of <c>reader</c>, <c>operator</c> and <c>admin</c>, which grant in
    /// the namespace what <see cref="Readers"/>, <see cref="Operators"/> and <see cref="Admins"/> grant in every namespace. For example, with the template above, the
    /// role <c>cloud-streams:orders:admin</c> grants the administration of the namespace <c>orders</c> (see #215)
    /// </remarks>
    public virtual string? Namespaced { get; set; }

}

/// <summary>
/// Represents a parsed template of the roles that grant access to a single namespace
/// </summary>
public sealed class NamespacedRoleTemplate
{

    /// <summary>
    /// Gets the placeholder of the namespace
    /// </summary>
    public const string NamespacePlaceholder = "{namespace}";

    /// <summary>
    /// Gets the placeholder of the level of access
    /// </summary>
    public const string LevelPlaceholder = "{level}";

    /// <summary>
    /// Gets the names of the levels of access, as used in namespaced roles
    /// </summary>
    public static IReadOnlyDictionary<string, CloudStreamsAccessLevel> Levels { get; } = new Dictionary<string, CloudStreamsAccessLevel>(StringComparer.Ordinal)
    {
        ["reader"] = CloudStreamsAccessLevel.Read,
        ["operator"] = CloudStreamsAccessLevel.Operate,
        ["admin"] = CloudStreamsAccessLevel.Administer
    };

    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, NamespacedRoleTemplate?> Cache = new(StringComparer.Ordinal);

    readonly System.Text.RegularExpressions.Regex _regex;

    NamespacedRoleTemplate(string template)
    {
        this.Template = template;
        var pattern = System.Text.RegularExpressions.Regex.Escape(template)
            .Replace(System.Text.RegularExpressions.Regex.Escape(NamespacePlaceholder), "(?<namespace>[a-z0-9]([-a-z0-9]*[a-z0-9])?)")
            .Replace(System.Text.RegularExpressions.Regex.Escape(LevelPlaceholder), $"(?<level>{string.Join('|', Levels.Keys)})");
        this._regex = new($"^{pattern}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Gets the template
    /// </summary>
    public string Template { get; }

    /// <summary>
    /// Parses the specified template, if it is valid
    /// </summary>
    /// <param name="template">The template to parse</param>
    /// <returns>The parsed template, or null if the template is not set</returns>
    public static NamespacedRoleTemplate? TryParse(string? template)
    {
        if (string.IsNullOrWhiteSpace(template) || !IsValid(template)) return null;
        return Cache.GetOrAdd(template, t => new NamespacedRoleTemplate(t));
    }

    /// <summary>
    /// Determines whether or not the specified template is valid
    /// </summary>
    /// <param name="template">The template to check</param>
    /// <returns>A boolean indicating whether or not the template is valid</returns>
    public static bool IsValid(string template) =>
        !string.IsNullOrWhiteSpace(template)
        && CountOf(template, NamespacePlaceholder) == 1
        && CountOf(template, LevelPlaceholder) == 1;

    static int CountOf(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    /// <summary>
    /// Attempts to match the specified role against the template
    /// </summary>
    /// <param name="role">The role to match</param>
    /// <param name="namespace">The namespace the role grants access to</param>
    /// <param name="level">The level of access the role grants</param>
    /// <returns>A boolean indicating whether or not the role matches the template</returns>
    public bool TryMatch(string role, out string @namespace, out CloudStreamsAccessLevel level)
    {
        @namespace = null!;
        level = CloudStreamsAccessLevel.None;
        if (string.IsNullOrWhiteSpace(role)) return false;
        var match = this._regex.Match(role);
        if (!match.Success) return false;
        @namespace = match.Groups["namespace"].Value;
        level = Levels[match.Groups["level"].Value];
        return true;
    }

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
    /// Gets/sets the type of the claim that lists the namespaces a bearer token may publish cloud events to, if any
    /// </summary>
    /// <remarks>
    /// When set, bearer tokens may only publish to the gateways of the namespaces the claim lists, as an array or as a space-separated string, and tokens without the
    /// claim may publish to none. When not set, which is the default, bearer tokens may publish to the gateways of every namespace (see #215)
    /// </remarks>
    public virtual string? NamespaceClaimType { get; set; }

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

    /// <summary>
    /// Gets/sets the namespaces the publisher may publish cloud events to, through their gateways. The publisher may publish to every namespace if not set (see #215)
    /// </summary>
    public virtual List<string> Namespaces { get; set; } = [];

}
