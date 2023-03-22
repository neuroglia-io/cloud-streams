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

using CloudStreams.Core.Data.Models;
using Microsoft.AspNetCore.SignalR.Client;
using System.Reactive.Subjects;

namespace CloudStreams.Gateway.Api.Client.Services;

/// <summary>
/// Represents the service used to listen to <see cref="CloudEvent"/>s in real-time
/// </summary>
public class CloudEventHubClient
    : IAsyncDisposable
{

    private bool _Disposed;

    /// <summary>
    /// Initializes a new <see cref="CloudEventHubClient"/>
    /// </summary>
    /// <param name="connection">The underlying <see cref="HubConnection"/></param>
    public CloudEventHubClient(HubConnection connection)
    {
        this.Connection = connection;
        this.Connection.On<CloudEvent>(nameof(ICloudEventHubClient.StreamEvent), this.Subject.OnNext);
    }

    /// <summary>
    /// Gets the underlying <see cref="HubConnection"/>
    /// </summary>
    protected HubConnection Connection { get; }

    /// <summary>
    /// Gets the <see cref="Subject{T}"/> used to monitor ingested <see cref="CloudEvent"/>s
    /// </summary>
    protected Subject<CloudEvent> Subject { get; } = new();

    /// <summary>
    /// Starts the <see cref="CloudEventHubClient"/> if it's not already running
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual Task StartAsync() => this.Connection.State == HubConnectionState.Disconnected ? this.Connection.StartAsync() : Task.CompletedTask;

    /// <inheritdoc/>
    public IObservable<CloudEvent> SelectAll() => this.Subject;

    /// <summary>
    /// Disposes of the <see cref="CloudEventHubClient"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="CloudEventHubClient"/> is being disposed of</param>
    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (this._Disposed) return;
        if (disposing)
        {
            this.Subject.Dispose();
            await this.Connection.DisposeAsync();
        }
        this._Disposed = true;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.DisposeAsync(disposing: true);
        GC.SuppressFinalize(this);
    }

}
