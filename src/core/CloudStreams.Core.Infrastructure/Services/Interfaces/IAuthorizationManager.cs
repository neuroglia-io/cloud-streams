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

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Defines the fundamentals of a service used to manage authorization
/// </summary>
public interface IAuthorizationManager
{

    /// <summary>
    /// Evaluates a <see cref="CloudEvent"/> against the specified policy
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to evaluate</param>
    /// <param name="policy">The <see cref="CloudEventAuthorizationPolicy"/> to evaluate the event against</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="OperationResult"/>, used to describe the result of the evaluation</returns>
    Task<OperationResult> EvaluateAsync(CloudEvent e, CloudEventAuthorizationPolicy policy, CancellationToken cancellationToken = default);

}
