// Copyright © 2023-Present The Cloud Streams Authors
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

namespace CloudStreams.Core;

/// <summary>
/// Enumerates default health statuses
/// </summary>
public static class HealthStatus
{

    /// <summary>
    /// Indicates that the service is healthy
    /// </summary>
    public const string Healthy = "healthy";

    /// <summary>
    /// Indicates that the service is unhealthy
    /// </summary>
    public const string Unhealthy = "unhealthy";

    /// <summary>
    /// Indicates that the service is degraded
    /// </summary>
    public const string Degraded = "degraded";

}