﻿// Copyright © 2024-Present The Cloud Streams Authors
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

using Microsoft.Extensions.Options;
using Neuroglia.Data.Infrastructure.ResourceOriented.Configuration;
using System.Collections.Concurrent;

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Represents a <see cref="ResourceController{TResource}"/> used to control <see cref="Broker"/>s
/// </summary>
/// <inheritdoc/>
public class BrokerResourceController(IServiceProvider serviceProvider, ILoggerFactory loggerFactory, IOptions<ResourceControllerOptions<Broker>> controllerOptions, IResourceRepository repository)
    : ResourceController<Broker>(loggerFactory, controllerOptions, repository)
{

    /// <summary>
    /// Gets the current <see cref="IServiceProvider"/>
    /// </summary>
    protected IServiceProvider ServiceProvider { get; } = serviceProvider;

    /// <summary>
    /// Gets a <see cref="ConcurrentDictionary{TKey, TValue}"/> containing key/health monitor mappings of managed gateways
    /// </summary>
    protected ConcurrentDictionary<string, BrokerHealthMonitor> HealthMonitors { get; } = new();

    /// <inheritdoc/>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
        foreach(var gateway in this.Resources.Values) await this.OnResourceCreatedAsync(gateway, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task OnResourceCreatedAsync(Broker broker, CancellationToken cancellationToken = default)
    {
        await base.OnResourceCreatedAsync(broker, cancellationToken).ConfigureAwait(false);
        if (this.Watch == null) return;
        var resourceMonitor = new ResourceMonitor<Broker>(this.Watch, broker, true);
        var healthMonitor = ActivatorUtilities.CreateInstance<BrokerHealthMonitor>(this.ServiceProvider, resourceMonitor);
        if (!this.HealthMonitors.TryAdd(this.GetResourceCacheKey(broker.GetName(), broker.GetNamespace()), healthMonitor)) return;
        await healthMonitor.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task OnResourceDeletedAsync(Broker broker, CancellationToken cancellationToken = default)
    {
        await base.OnResourceDeletedAsync(broker, cancellationToken).ConfigureAwait(false);
        if (!this.HealthMonitors.TryRemove(this.GetResourceCacheKey(broker.GetName(), broker.GetNamespace()), out var monitor)) return;
        await monitor.StopAsync(cancellationToken).ConfigureAwait(false);
        await monitor.DisposeAsync();
    }

    /// <inheritdoc/>
    protected override async ValueTask DisposeAsync(bool disposing)
    {
        await base.DisposeAsync(disposing).ConfigureAwait(false);
        if (!disposing) return;
        foreach(var kvp in this.HealthMonitors) await kvp.Value.DisposeAsync().ConfigureAwait(false);
        this.HealthMonitors.Clear();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        foreach (var kvp in this.HealthMonitors) kvp.Value.Dispose();
        this.HealthMonitors.Clear();
    }

}
