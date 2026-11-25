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

using Microsoft.AspNetCore.SignalR.Client;
using Neuroglia.Serialization;
using System.Text.Json;

namespace CloudStreams.Dashboard.Services;

/// <summary>
/// Represents a connection to the gateway's cloud event hub, which streams the cloud events it ingests (see #221)
/// </summary>
/// <remarks>The hub filters cloud events by source and type itself: the filter is subscribed to again whenever the connection is restored</remarks>
public sealed class CloudEventHubConnection
    : IAsyncDisposable
{

    readonly HubConnection _connection;
    readonly IJsonSerializer _serializer;
    (string? Source, string? Type) _filter;

    /// <summary>
    /// Initializes a new <see cref="CloudEventHubConnection"/>
    /// </summary>
    /// <param name="baseUri">The base uri of the gateway</param>
    /// <param name="serializer">The service used to deserialize the streamed cloud events</param>
    public CloudEventHubConnection(string baseUri, IJsonSerializer serializer)
    {
        this._serializer = serializer;
        this._connection = new HubConnectionBuilder().WithUrl($"{baseUri}api/ws/events").WithAutomaticReconnect().Build();
        this._connection.On<JsonElement>("StreamEvent", e =>
        {
            var cloudEvent = this._serializer.Deserialize<CloudEvent>(e.GetRawText());
            if (cloudEvent != null) this.Received?.Invoke(cloudEvent);
        });
        this._connection.Reconnected += async _ => await this.SubscribeAsync(this._filter.Source, this._filter.Type).ConfigureAwait(false);
        this._connection.Reconnecting += _ => this.NotifyStateChanged();
        this._connection.Closed += _ => this.NotifyStateChanged();
    }

    /// <summary>
    /// Occurs when a cloud event has been received
    /// </summary>
    public event Action<CloudEvent>? Received;

    /// <summary>
    /// Occurs when the state of the connection has changed
    /// </summary>
    public event Action? StateChanged;

    /// <summary>
    /// Gets the state of the connection
    /// </summary>
    public HubConnectionState State => this._connection.State;

    /// <summary>
    /// Starts the connection
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public async Task StartAsync()
    {
        await this._connection.StartAsync().ConfigureAwait(false);
        this.StateChanged?.Invoke();
    }

    /// <summary>
    /// Only receives the cloud events of the specified source and type
    /// </summary>
    /// <param name="source">The source of the cloud events to receive, if any</param>
    /// <param name="type">The type of the cloud events to receive, if any</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public async Task SubscribeAsync(string? source, string? type)
    {
        this._filter = (string.IsNullOrWhiteSpace(source) ? null : source.Trim(), string.IsNullOrWhiteSpace(type) ? null : type.Trim());
        if (this._connection.State == HubConnectionState.Connected) await this._connection.InvokeAsync("Subscribe", this._filter.Source, this._filter.Type).ConfigureAwait(false);
        this.StateChanged?.Invoke();
    }

    Task NotifyStateChanged()
    {
        this.StateChanged?.Invoke();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => this._connection.DisposeAsync();

}
