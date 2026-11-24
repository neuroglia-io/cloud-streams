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
/// <param name="Access">The level of access granted to the user: 'none', 'read', 'operate' or 'administer'</param>
public record UserInfo(bool Enabled, bool Authenticated, string? Name, string? Access);

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
    /// <returns>A boolean indicating whether the current user may resume and replay subscriptions</returns>
    public async Task<bool> CanOperateAsync() => await this.GetLevelAsync().ConfigureAwait(false) >= 2;

    /// <summary>
    /// Determines whether the current user may create, edit and delete resources
    /// </summary>
    /// <returns>A boolean indicating whether the current user may create, edit and delete resources</returns>
    public async Task<bool> CanAdministerAsync() => await this.GetLevelAsync().ConfigureAwait(false) >= 3;

    async Task<int> GetLevelAsync() => (await this.GetUserAsync().ConfigureAwait(false))?.Access?.ToLowerInvariant() switch
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
