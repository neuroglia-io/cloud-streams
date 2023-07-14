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

using CloudStreams.Core.Infrastructure.Services;
using CloudStreams.Gateway.Api.Client.Services;
using CloudStreams.Gateway.Api.Hubs;
using Hylo;
using Microsoft.AspNetCore.SignalR;
using System.Reactive.Linq;

namespace CloudStreams.Gateway.Api.Services;

/// <summary>
/// Represents a service used to dispatch ingested cloud events to all <see cref="ICloudEventHub"/>s
/// </summary>
public class CloudEventHubDispatcher
    : BackgroundService
{

    /// <summary>
    /// Initializes a new <see cref="CloudEventHubDispatcher"/>
    /// </summary>
    /// <param name="eventStoreProvider">The service used to provide an <see cref="IEventStore"/> implementation</param>
    /// <param name="hubContext">The current <see cref="ICloudEventHubClient"/>'s <see cref="IHubContext{THub, T}"/></param>
    public CloudEventHubDispatcher(IEventStoreProvider eventStoreProvider, IHubContext<CloudEventHub, ICloudEventHubClient> hubContext)
    {
        this.EventStoreProvider = eventStoreProvider;
        this.HubContext = hubContext;
    }

    /// <summary>
    /// Gets the service used to provide an <see cref="IEventStore"/> implementation
    /// </summary>
    protected IEventStoreProvider EventStoreProvider { get; }

    /// <summary>
    /// Gets the current <see cref="ICloudEventHubClient"/>'s <see cref="IHubContext{THub, T}"/>
    /// </summary>
    protected IHubContext<CloudEventHub, ICloudEventHubClient> HubContext { get; }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        (await this.EventStoreProvider.GetEventStore().SubscribeAsync(cancellationToken: stoppingToken).ConfigureAwait(false))
            .Select(e => e.ToCloudEvent())
            .SubscribeAsync(e => this.HubContext.Clients.All.StreamEvent(e, stoppingToken), cancellationToken: stoppingToken);
    }

}
