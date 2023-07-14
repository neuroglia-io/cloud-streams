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

using Hylo.Infrastructure;
using Hylo.Infrastructure.Services;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents the <see href="https://eventstore.com/">EventStore</see> implementation of the <see cref="IEventStore"/> interface
/// </summary>
[Plugin(typeof(IEventStoreProvider), typeof(ESEventStoreProviderPluginBootstrapper))]
public class ESEventStoreProvider
    : IEventStoreProvider, IDisposable, IAsyncDisposable
{

    private bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="ESEventStoreProvider"/>
    /// </summary>
    /// <param name="services">The current <see cref="IServiceProvider"/></param>
    public ESEventStoreProvider(IServiceProvider services)
    {
        this.Services = services;
    }

    /// <summary>
    /// Gets the current <see cref="IServiceProvider"/>
    /// </summary>
    protected IServiceProvider Services { get; }

    /// <inheritdoc/>
    public virtual IEventStore GetEventStore() => this.Services.GetRequiredService<ESEventStore>();

    /// <summary>
    /// Disposes of the <see cref="ESEventStoreProvider"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="ESEventStoreProvider"/> is being disposed of</param>
    /// <returns>A new awaitable <see cref="ValueTask"/></returns>
    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (!disposing || this._disposed) return;
        this._disposed = true;
        await ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.DisposeAsync(true).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of the <see cref="ESEventStoreProvider"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="ESEventStoreProvider"/> is being disposed of</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing || this._disposed) return;
        this._disposed = true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

}
