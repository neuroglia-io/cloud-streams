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

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Defines the fundamentals of a service used to evaluate cloud events against the policies of a gateway, without ingesting them (see #262)
/// </summary>
/// <remarks>Only gateways implement it: the standalone API has no admission pipeline</remarks>
public interface IGatewayAdmissionTester
{

    /// <summary>
    /// Evaluates the specified cloud event against the policies of the specified gateway, which may not be saved yet
    /// </summary>
    /// <param name="gateway">The gateway whose policies to evaluate the cloud event against</param>
    /// <param name="e">The cloud event to evaluate</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>What would become of the cloud event, and why</returns>
    Task<GatewayAdmissionTestResult> TestAsync(Gateway gateway, CloudEvent e, CancellationToken cancellationToken = default);

}
