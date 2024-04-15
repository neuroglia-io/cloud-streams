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

using CloudStreams.Core.Application;
using CloudStreams.Core.Application.Services;
using CloudStreams.Gateway.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Neuroglia.Data.Infrastructure.EventSourcing;
using Neuroglia.Reactive;
using System.Reactive.Linq;

namespace CloudStreams.Gateway.Api.Services;

/// <summary>
/// Represents a service used to dispatch ingested cloud events to all <see cref="ICloudEventHub"/>s
/// </summary>
/// <remarks>
/// Initializes a new <see cref="CloudEventHubDispatcher"/>
/// </remarks>
/// <param name="serviceProvider">The current <see cref="IServiceProvider"/></param>
/// <param name="logger">The service used to perform logging</param>
/// <param name="hubContext">The current <see cref="ICloudEventHubClient"/>'s <see cref="IHubContext{THub, T}"/></param>
public class CloudEventHubDispatcher(IServiceProvider serviceProvider, ILogger<CloudEventHubDispatcher> logger, IHubContext<CloudEventHub, ICloudEventHubClient> hubContext)
    : BackgroundService
{

    /// <summary>
    /// Gets the current <see cref="IServiceProvider"/>
    /// </summary>
    protected IServiceProvider ServiceProvider { get; } = serviceProvider;

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; } = logger;

    /// <summary>
    /// Gets the service used to source <see cref="CloudEvent"/>s
    /// </summary>
    protected ICloudEventStore EventStore => this.ServiceProvider.GetRequiredService<ICloudEventStore>();

    /// <summary>
    /// Gets the current <see cref="ICloudEventHubClient"/>'s <see cref="IHubContext{THub, T}"/>
    /// </summary>
    protected IHubContext<CloudEventHub, ICloudEventHubClient> HubContext { get; } = hubContext;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                (await this.EventStore
                    .ObserveAsync(cancellationToken: stoppingToken))
                    .Select(e => e.ToCloudEvent(default))
                    .SubscribeAsync(e => this.HubContext.Clients.All.StreamEvent(e, stoppingToken), cancellationToken: stoppingToken);
                break;
            }
            catch (StreamNotFoundException)
            {
                var delay = 5000;
                this.Logger.LogDebug("Failed to observe the cloud event stream because the first cloud event is yet to be published. Retrying in {delay} milliseconds...", delay);
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
        }

    }

}
