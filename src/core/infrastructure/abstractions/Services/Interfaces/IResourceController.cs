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
/// Defines the fundamentals of a service used to control <see cref="IResource"/>s of the specified type
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/>s to control</typeparam>
public interface IResourceController<TResource>
    : IObservable<IResourceWatchEvent<TResource>>
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Gets a <see cref="List{T}"/> containing the current state of controlled <see cref="IResource"/>s
    /// </summary>
    List<TResource> Resources { get; }

    /// <summary>
    /// Waits for the <see cref="IResourceController{TResource}"/> to be initialized
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task WaitUntilInitializedAsync();

}