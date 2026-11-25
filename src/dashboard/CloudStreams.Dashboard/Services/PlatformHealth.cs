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

using System.Text.Json;
using System.Text.Json.Nodes;

namespace CloudStreams.Dashboard.Services;

/// <summary>
/// Represents the outcome of one of the health checks of the gateway that serves the dashboard: Redis, EventStoreDB or the partition projections
/// </summary>
/// <param name="Name">The name of the check</param>
/// <param name="Status">The status the check reported</param>
/// <param name="Description">A description of the outcome, if any</param>
public record PlatformHealthCheck(string Name, string Status, string? Description);

/// <summary>
/// Represents the health of the gateway that serves the dashboard, as reported by its readiness endpoint (see #225)
/// </summary>
/// <param name="Status">The overall status</param>
/// <param name="Checks">The outcome of each check, if detailed</param>
public record PlatformHealth(string Status, IReadOnlyList<PlatformHealthCheck> Checks)
{

    /// <summary>
    /// Gets the path of the readiness endpoint
    /// </summary>
    public const string Path = "healthz";

    /// <summary>
    /// Parses the specified response of the readiness endpoint
    /// </summary>
    /// <param name="content">The content of the response: detailed JSON, or the status alone as plain text, for anonymous users of a secured dashboard</param>
    /// <returns>The parsed <see cref="PlatformHealth"/></returns>
    public static PlatformHealth Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var trimmed = content.Trim();
        if (!trimmed.StartsWith('{')) return new(Normalize(trimmed), []);
        JsonNode? root;
        try { root = JsonNode.Parse(trimmed); }
        catch (JsonException) { return new(Normalize(null), []); }
        var checks = new List<PlatformHealthCheck>();
        if (root?["entries"] is JsonObject entries)
        {
            foreach (var (name, entry) in entries)
            {
                checks.Add(new(name, Normalize(entry?["status"]?.GetValue<string>()), entry?["description"]?.GetValue<string>()));
            }
        }
        return new(Normalize(root?["status"]?.GetValue<string>()), checks);
    }

    /// <summary>
    /// Normalizes the specified status, which ASP.NET Core capitalizes, into one of the <see cref="CloudStreams.Core.HealthStatus"/> values Cloud Streams resources use
    /// </summary>
    static string Normalize(string? status) => string.IsNullOrWhiteSpace(status) ? "unknown" : status.Trim().ToLowerInvariant();

}
