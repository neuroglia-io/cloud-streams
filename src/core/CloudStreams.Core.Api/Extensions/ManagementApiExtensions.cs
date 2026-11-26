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

using CloudStreams.Core.Api.Configuration;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace CloudStreams.Core.Api;

/// <summary>
/// Defines extensions used to control where the management API is served
/// </summary>
public static partial class ManagementApiExtensions
{

    /// <summary>
    /// Restricts the management API to the port configured in <see cref="ManagementApiOptions"/>, or disables it, leaving the public endpoints available on every port
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/> to configure</param>
    /// <returns>The configured <see cref="IApplicationBuilder"/></returns>
    /// <remarks>Must be added before the middlewares that serve the dashboard and the endpoints, so that requests to the management API are refused first (see #144)</remarks>
    public static IApplicationBuilder UseManagementApiIsolation(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<IOptions<ManagementApiOptions>>().Value;
        if (options.Enabled && !options.Port.HasValue) return app;
        return app.Use(async (context, next) =>
        {
            if (IsPublic(context.Request.Path) || (options.Enabled && context.Connection.LocalPort == options.Port)) await next(context).ConfigureAwait(false);
            else context.Response.StatusCode = StatusCodes.Status404NotFound;
        });
    }

    /// <summary>
    /// Determines whether the specified path is one of a public endpoint, which is to say one that publishers, subscribers or orchestrators must be able to reach
    /// </summary>
    /// <param name="path">The path to check</param>
    /// <returns>A boolean indicating whether or not the specified path is public</returns>
    public static bool IsPublic(PathString path) =>
        path.StartsWithSegments("/api/events/pub", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/healthz", StringComparison.OrdinalIgnoreCase)
        || AcknowledgementPath().IsMatch(path.Value ?? string.Empty);

    [GeneratedRegex("^/api/resources/v1/namespaces/[^/]+/subscriptions/[^/]+/(ack|nack)$", RegexOptions.IgnoreCase)]
    private static partial Regex AcknowledgementPath();

}
