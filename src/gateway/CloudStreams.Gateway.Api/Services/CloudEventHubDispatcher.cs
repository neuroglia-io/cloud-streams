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
    /// <remarks>
    /// When the observed stream faults or completes, the dispatcher resubscribes with exponential backoff, from the position after the last dispatched event, so
    /// that a transient failure of the event store does not stop streaming until the gateway restarts (see #182)
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ulong? nextOffset = null;
        var consecutiveFailures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            IDisposable? subscription = null;
            try
            {
                var completion = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
                var stream = await this.EventStore.ObserveAsync(nextOffset.HasValue ? (long)nextOffset.Value : StreamPosition.EndOfStream, stoppingToken).ConfigureAwait(false);
                subscription = stream.SubscribeAsync(
                    async record =>
                    {
                        nextOffset = record.Sequence + 1;
                        consecutiveFailures = 0;
                        var e = record.ToCloudEvent(default);
                        // each client belongs to a single group: one of the groups of the cloud event, if it is to receive it (see #221)
                        try { await this.HubContext.Clients.Groups(CloudEventHub.GetGroupNames(e)).StreamEvent(e, stoppingToken).ConfigureAwait(false); }
                        catch (Exception ex) when (ex is not OperationCanceledException) { this.Logger.LogWarning(ex, "Failed to dispatch the cloud event at offset '{offset}' to hub clients", record.Sequence); }
                    },
                    onErrorAsync: ex =>
                    {
                        completion.TrySetResult(ex);
                        return Task.CompletedTask;
                    },
                    onCompletedAsync: () =>
                    {
                        completion.TrySetResult(null);
                        return Task.CompletedTask;
                    });
                Exception? error;
                using (stoppingToken.Register(() => completion.TrySetCanceled(stoppingToken))) error = await completion.Task.ConfigureAwait(false);
                if (error == null) this.Logger.LogWarning("The observed cloud event stream completed unexpectedly");
                else this.Logger.LogWarning(error, "An error occurred while observing the cloud event stream");
            }
            catch (StreamNotFoundException)
            {
                var delay = TimeSpan.FromSeconds(5);
                this.Logger.LogDebug("Failed to observe the cloud event stream because the first cloud event is yet to be published. Retrying in {delay}...", delay);
                try { await Task.Delay(delay, stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to observe the cloud event stream");
            }
            finally
            {
                subscription?.Dispose();
            }
            consecutiveFailures++;
            var backoff = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, consecutiveFailures - 1), 60));
            this.Logger.LogInformation("Resubscribing to the cloud event stream in {delay}...", backoff);
            try { await Task.Delay(backoff, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

}
