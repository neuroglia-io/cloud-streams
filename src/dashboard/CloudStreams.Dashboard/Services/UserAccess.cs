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

using System.Net.Http.Json;
using System.Text.Json;

namespace CloudStreams.Dashboard.Services;

/// <summary>
/// Describes the current user, as reported by the server
/// </summary>
/// <param name="Enabled">A boolean indicating whether or not users must sign in</param>
/// <param name="Authenticated">A boolean indicating whether or not the user is signed in</param>
/// <param name="Name">The user's name, if any</param>
/// <param name="Access">The level of access granted to the user in every namespace: 'none', 'read', 'operate' or 'administer'</param>
/// <param name="Namespaces">The levels of access granted to the user in specific namespaces, if any (see #215)</param>
public record UserInfo(bool Enabled, bool Authenticated, string? Name, string? Access, Dictionary<string, string>? Namespaces = null);

/// <summary>
/// Represents the service used to determine what the current user may do, so that the dashboard only offers actions the server will accept
/// </summary>
/// <remarks>
/// The server enforces the user's access regardless (see #147). Should it not report any, for example because it is an older version, every action is offered, as before
/// </remarks>
/// <param name="httpClient">The service used to perform HTTP requests</param>
public class UserAccess(HttpClient httpClient)
{

    Task<UserInfo?>? _user;

    /// <summary>
    /// Gets the current user, as reported by the server, if any
    /// </summary>
    /// <returns>The current user, or null if the server did not describe it</returns>
    public Task<UserInfo?> GetUserAsync() => this._user ??= this.LoadAsync();

    /// <summary>
    /// Determines whether the current user may resume and replay subscriptions
    /// </summary>
    /// <param name="namespace">The namespace to check, if any. The whole cluster is checked if not set</param>
    /// <returns>A boolean indicating whether the current user may resume and replay subscriptions</returns>
    public async Task<bool> CanOperateAsync(string? @namespace = null) => await this.GetLevelAsync(@namespace).ConfigureAwait(false) >= 2;

    /// <summary>
    /// Determines whether the current user may create, edit and delete resources
    /// </summary>
    /// <param name="namespace">The namespace to check, if any. The whole cluster is checked if not set</param>
    /// <returns>A boolean indicating whether the current user may create, edit and delete resources</returns>
    public async Task<bool> CanAdministerAsync(string? @namespace = null) => await this.GetLevelAsync(@namespace).ConfigureAwait(false) >= 3;

    /// <summary>
    /// Determines whether the current user may read the whole cluster, such as the cloud events of every namespace
    /// </summary>
    /// <returns>A boolean indicating whether the current user may read the whole cluster</returns>
    public async Task<bool> CanReadClusterAsync() => await this.GetLevelAsync(null).ConfigureAwait(false) >= 1;

    /// <summary>
    /// Determines whether the current user may resume and replay the subscriptions of at least one namespace
    /// </summary>
    /// <returns>A boolean indicating whether the current user may operate in at least one namespace</returns>
    public async Task<bool> CanOperateAnyAsync() => await this.GetHighestLevelAsync().ConfigureAwait(false) >= 2;

    /// <summary>
    /// Determines whether the current user may create, edit and delete the resources of at least one namespace
    /// </summary>
    /// <returns>A boolean indicating whether the current user may administer at least one namespace</returns>
    public async Task<bool> CanAdministerAnyAsync() => await this.GetHighestLevelAsync().ConfigureAwait(false) >= 3;

    async Task<int> GetHighestLevelAsync()
    {
        var user = await this.GetUserAsync().ConfigureAwait(false);
        var level = ToLevel(user?.Access);
        return user?.Namespaces == null || user.Namespaces.Count == 0 ? level : Math.Max(level, user.Namespaces.Values.Max(ToLevel));
    }

    async Task<int> GetLevelAsync(string? @namespace)
    {
        var user = await this.GetUserAsync().ConfigureAwait(false);
        var level = ToLevel(user?.Access);
        if (string.IsNullOrWhiteSpace(@namespace) || user?.Namespaces == null || !user.Namespaces.TryGetValue(@namespace, out var granted)) return level;
        return Math.Max(level, ToLevel(granted));
    }

    static int ToLevel(string? access) => access?.ToLowerInvariant() switch
    {
        "none" => 0,
        "read" => 1,
        "operate" => 2,
        _ => 3
    };

    async Task<UserInfo?> LoadAsync()
    {
        try { return await httpClient.GetFromJsonAsync<UserInfo>("api/auth/user", new JsonSerializerOptions(JsonSerializerDefaults.Web)).ConfigureAwait(false); }
        catch { return null; } // an older or misconfigured server must not break the dashboard
    }

}
