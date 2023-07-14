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

using CloudStreams.Core.Data;

namespace CloudStreams.Core.Api.Client.Services;

/// <summary>
/// Represents the service used to handle a resource watch event subscription
/// </summary>
/// <typeparam name="TResource">The type of resources to watch</typeparam>
public class ResourceWatch<TResource>
    : IObservable<IResourceWatchEvent<TResource>>, IAsyncDisposable
    where TResource : class, IResource, new()
{

    private bool _Disposed;

    /// <summary>
    /// Initializes a new <see cref="ResourceWatch{TResource}"/>
    /// </summary>
    /// <param name="resourceWatchEventHub">The service used to interact with the <see cref="IResourceEventWatchHub"/></param>
    /// <param name="resourceNamespace">The namespace watched resources belong to, if any</param>
    /// <param name="stream">The <see cref="IObservable{T}"/> used to watch resources of the specified type</param>
    public ResourceWatch(ResourceWatchEventHubClient resourceWatchEventHub, string? resourceNamespace, IObservable<IResourceWatchEvent<TResource>> stream)
    {
        this.ResourceWatchEventHub = resourceWatchEventHub ?? throw new ArgumentNullException(nameof(resourceWatchEventHub));
        this.ResourceNamespace = resourceNamespace;
        this.Stream = stream;
    }

    /// <summary>
    /// Gets the service used to interact with the <see cref="IResourceEventWatchHub"/>
    /// </summary>
    protected ResourceWatchEventHubClient ResourceWatchEventHub { get; }

    /// <summary>
    /// Gets the namespace watched resources belong to, if any
    /// </summary>
    protected virtual string? ResourceNamespace { get; }

    /// <summary>
    /// Gets the <see cref="IObservable{T}"/> used to watch resources of the specified type
    /// </summary>
    protected IObservable<IResourceWatchEvent<TResource>> Stream { get; }

    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<IResourceWatchEvent<TResource>> observer) => this.Stream.Subscribe(observer);

    /// <summary>
    /// Disposes of the <see cref="ResourceWatch{TResource}"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="ResourceWatch{TResource}"/> is being disposed of</param>
    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (!this._Disposed)
        {
            if (disposing)
            {
                await this.ResourceWatchEventHub.StopWatchingAsync<TResource>();
            }
            this._Disposed = true;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.DisposeAsync(disposing: true).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

}