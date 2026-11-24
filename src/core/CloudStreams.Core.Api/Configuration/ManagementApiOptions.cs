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
/// Represents the options used to configure where the management API, which includes the dashboard, its hubs and the MCP endpoint, is served
/// </summary>
/// <remarks>
/// Gateways serve both the public ingestion endpoint and the management API. To keep the latter off the public network (see #144), it can either be disabled on
/// ingestion-only replicas, or served on a dedicated port that is not exposed publicly. The public endpoints (ingestion, acknowledgements and health checks) remain
/// available on every port
/// </remarks>
public class ManagementApiOptions
{

    /// <summary>
    /// Gets the name of the configuration section the options are bound to
    /// </summary>
    public const string ConfigurationSection = "ManagementApi";

    /// <summary>
    /// Gets/sets a boolean indicating whether or not the management API is served. Defaults to true
    /// </summary>
    public virtual bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets/sets the port the management API is exclusively served on, if any. The application must listen on that port, e.g. with <c>ASPNETCORE_HTTP_PORTS=8080;8081</c>
    /// </summary>
    public virtual int? Port { get; set; }

}
