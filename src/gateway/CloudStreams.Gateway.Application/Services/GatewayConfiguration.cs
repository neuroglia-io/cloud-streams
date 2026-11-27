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

using CloudStreams.Core;
using CloudStreams.Core.Application;
using CloudStreams.Gateway.Application.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Data.Infrastructure.ResourceOriented.Services;

namespace CloudStreams.Gateway.Application.Services;

/// <summary>
/// Defines the fundamentals of the service that holds the configuration of the gateway the application serves
/// </summary>
public interface IGatewayConfiguration
{

    /// <summary>
    /// Gets a boolean indicating whether or not the gateway's configuration has been loaded. Until it has, its policies are unknown, and cloud events are refused
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Gets the gateway the application serves, if its configuration has been loaded
    /// </summary>
    Core.Resources.Gateway? Gateway { get; }

}

/// <summary>
/// Represents the default implementation of the <see cref="IGatewayConfiguration"/>, which creates the gateway's resource when missing, and monitors it
/// </summary>
/// <param name="serviceProvider">The current <see cref="IServiceProvider"/></param>
/// <param name="logger">The service used to perform logging</param>
/// <param name="options">The service used to access the current <see cref="GatewayOptions"/></param>
public class GatewayConfiguration(IServiceProvider serviceProvider, ILogger<GatewayConfiguration> logger, IOptions<GatewayOptions> options)
    : BackgroundService, IGatewayConfiguration
{

    IResourceMonitor<Core.Resources.Gateway>? _monitor;

    /// <inheritdoc/>
    public bool IsReady => this._monitor != null;

    /// <inheritdoc/>
    public Core.Resources.Gateway? Gateway => this._monitor?.Resource;

    /// <inheritdoc/>
    /// <remarks>
    /// The gateway's resource is only created when it does not exist, a conflict with a replica that created it concurrently is tolerated, and transient failures are
    /// retried with backoff rather than stopping the host (see #142)
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var name = options.Value.GetName();
        var @namespace = options.Value.GetNamespace();
        for (var attempt = 1; !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                var resources = serviceProvider.GetRequiredService<IResourceRepository>();
                Core.Resources.Gateway? gateway = null;
                try { gateway = await resources.GetAsync<Core.Resources.Gateway>(name, @namespace, stoppingToken).ConfigureAwait(false); }
                catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.NotFound) { }
                if (gateway == null)
                {
                    await resources.EnsureNamespaceAsync(@namespace, stoppingToken).ConfigureAwait(false);
                    // a gateway that creates its own resource is not deployed by the operator, which must therefore not deploy it again (see #279)
                    var metadata = new ResourceMetadata(name, @namespace) { Annotations = new Dictionary<string, string>() { { CloudStreamsDefaults.Resources.WorkloadAnnotation, CloudStreamsDefaults.Resources.ExternalWorkload } } };
                    try { await resources.AddAsync(new Core.Resources.Gateway(metadata, new Core.Resources.GatewaySpec()), false, stoppingToken).ConfigureAwait(false); }
                    catch (ProblemDetailsException ex) when (ex.Problem.IsConflict()) { /* created by another replica in the meantime */ }
                }
                this._monitor = await resources.MonitorAsync<Core.Resources.Gateway>(name, @namespace, false, stoppingToken).ConfigureAwait(false);
                logger.LogInformation("The configuration of the gateway '{gateway}' of the namespace '{namespace}' has been loaded: admitting cloud events", name, @namespace);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt - 1), 30));
                logger.LogError(ex, "Failed to load the configuration of the gateway '{gateway}' of the namespace '{namespace}' (attempt {attempt}). Cloud events are refused until it is loaded. Retrying in {delay}...", name, @namespace, attempt, delay);
                try { await Task.Delay(delay, stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        this._monitor?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

}
