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

using CloudStreams.Core;

namespace CloudStreams.Gateway.Application.Services;

/// <summary>
/// Defines the fundamentals of a service that controls <see cref="CloudEvent"/> admission
/// </summary>
public interface ICloudEventAdmissionControl
{

    /// <summary>
    /// Evaluates the admission of the specified <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="CloudEvent"/> to evaluate for admission</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="OperationResult{TContent}"/> that describes the result of the evaluation</returns>
    Task<OperationResult<CloudEventDescriptor>> EvaluateAsync(CloudEvent e, CancellationToken cancellationToken = default);

}
