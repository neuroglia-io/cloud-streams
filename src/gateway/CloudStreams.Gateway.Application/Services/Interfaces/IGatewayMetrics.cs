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

namespace CloudStreams.Gateway.Application.Services;

/// <summary>
/// Defines the fundamentals of a service used to manage Cloud Streams Gateway related metrics
/// </summary>
public interface IGatewayMetrics
    : IDisposable
{

    /// <summary>
    /// Increments the total count of cloud events rejected after evaluation of defined authorization policies
    /// </summary>
    void IncrementTotalRejectedEvents();

    /// <summary>
    /// Increments the total count of invalid cloud events received by the gateway
    /// </summary>
    void IncrementTotalInvalidEvents();

    /// <summary>
    /// Increments the total count of cloud events ingested by the gateway
    /// </summary>
    void IncrementTotalIngestedEvents();

}
