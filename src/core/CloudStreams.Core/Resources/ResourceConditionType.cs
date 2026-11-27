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
/// Enumerates the types of the conditions the operator reports on the gateways and brokers it deploys (see #279)
/// </summary>
public static class ResourceConditionType
{

    /// <summary>
    /// Indicates whether the workload runs the requested number of available replicas, of the resource's current generation
    /// </summary>
    public const string Ready = "Ready";
    /// <summary>
    /// Indicates whether the workload is being rolled out, or scaled
    /// </summary>
    public const string Progressing = "Progressing";
    /// <summary>
    /// Indicates whether the workload could not be deployed, or has failed to roll out
    /// </summary>
    public const string Degraded = "Degraded";

}
