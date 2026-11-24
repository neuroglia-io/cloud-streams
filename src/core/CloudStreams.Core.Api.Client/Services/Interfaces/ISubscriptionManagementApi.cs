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

namespace CloudStreams.Core.Api.Client.Services;

/// <summary>
/// Defines the fundamentals of the API used to manage <see cref="Subscription"/>s
/// </summary>
public interface ISubscriptionManagementApi
    : IResourceManagementApi<Subscription>
{

    /// <summary>
    /// Resumes the specified faulted <see cref="Subscription"/>, by clearing its fault so that the cloud event that caused it is retried
    /// </summary>
    /// <param name="name">The name of the <see cref="Subscription"/> to resume</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The resumed <see cref="Subscription"/></returns>
    Task<Subscription> ResumeAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replays the specified <see cref="Subscription"/>, by delivering every cloud event matching it again, from the beginning of the stream
    /// </summary>
    /// <param name="name">The name of the <see cref="Subscription"/> to replay</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The replayed <see cref="Subscription"/></returns>
    Task<Subscription> ReplayAsync(string name, CancellationToken cancellationToken = default);

}
