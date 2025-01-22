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

using CloudStreams.Core.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Data.Infrastructure.ResourceOriented.Services;
using Neuroglia.Serialization;
using System.Diagnostics;

namespace CloudStreams.Gateway.Application.Services;

/// <summary>
/// Represents the service used to provision the solution's database
/// </summary>
/// <param name="serviceProvider">The current <see cref="IServiceProvider"/></param>
/// <param name="logger">The service used to perform logging</param>
/// <param name="jsonSerializer">The service used to serialize/deserialize data to/from JSON</param>
/// <param name="yamlSerializer">The service used to serialize/deserialize data to/from YAML</param>
public class DatabaseProvisioner(IServiceProvider serviceProvider, ILogger<DatabaseProvisioner> logger, IJsonSerializer jsonSerializer, IYamlSerializer yamlSerializer)
    : IHostedService
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
    /// Gets the service used to serialize/deserialize data to/from JSON
    /// </summary>
    protected IJsonSerializer JsonSerializer { get; } = jsonSerializer;

    /// <summary>
    /// Gets the service used to serialize/deserialize data to/from YAML
    /// </summary>
    protected IYamlSerializer YamlSerializer { get; } = yamlSerializer;

    /// <inheritdoc/>
    public virtual async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = this.ServiceProvider.CreateScope();
        var resources = scope.ServiceProvider.GetRequiredService<IResourceRepository>();
        await this.ProvisionGatewaysAsync(resources, cancellationToken).ConfigureAwait(false);
        await this.ProvisionBrokersAsync(resources, cancellationToken).ConfigureAwait(false);
        await this.ProvisionSubscriptionAsync(resources, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Provisions the solution's database with gateways
    /// </summary>
    /// <param name="resources">The <see cref="IResourceRepository"/> to provision</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task ProvisionGatewaysAsync(IResourceRepository resources, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var stopwatch = new Stopwatch();
        var directory = new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "data", "seed", "gateways"));
        if (!directory.Exists) return;
        this.Logger.LogInformation("Starting importing gateways from directory '{directory}'...", directory.FullName);
        var pattern = "*.*";
        var files = directory.GetFiles(pattern, SearchOption.AllDirectories).Where(f => f.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || f.FullName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || f.FullName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase));
        if (!files.Any())
        {
            this.Logger.LogWarning("No gateway static resource files matching search pattern '{pattern}' found in directory '{directory}'. Skipping import.", pattern, directory.FullName);
            return;
        }
        stopwatch.Restart();
        var count = 0;
        foreach (var file in files)
        {
            try
            {
                var extension = file.FullName.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                var serializer = extension?.ToLowerInvariant() switch
                {
                    "json" => (ITextSerializer)this.JsonSerializer,
                    "yml" or "yaml" => this.YamlSerializer,
                    _ => throw new NotSupportedException($"The specified extension '{extension}' is not supported for static resource files")
                };
                using var stream = file.OpenRead();
                using var streamReader = new StreamReader(stream);
                var text = await streamReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                var gateway = serializer.Deserialize<Core.Resources.Gateway>(text)!;
                await resources.AddAsync(gateway, false, cancellationToken).ConfigureAwait(false);
                this.Logger.LogInformation("Successfully imported gateway '{subscription}' from file '{file}'", $"{gateway.Metadata.Name}", file.FullName);
                count++;
            }
            catch (Exception ex)
            {
                this.Logger.LogError("An error occurred while reading a gateway from file '{file}': {ex}", file.FullName, ex);
                continue;
            }
        }
        stopwatch.Stop();
        this.Logger.LogInformation("Completed importing {count} gateway in {ms} milliseconds", count, stopwatch.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// Provisions the solution's database with brokers
    /// </summary>
    /// <param name="resources">The <see cref="IResourceRepository"/> to provision</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task ProvisionBrokersAsync(IResourceRepository resources, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var stopwatch = new Stopwatch();
        var directory = new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "data", "seed", "gateways"));
        if (!directory.Exists) return;
        this.Logger.LogInformation("Starting importing brokers from directory '{directory}'...", directory.FullName);
        var pattern = "*.*";
        var files = directory.GetFiles(pattern, SearchOption.AllDirectories).Where(f => f.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || f.FullName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || f.FullName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase));
        if (!files.Any())
        {
            this.Logger.LogWarning("No broker static resource files matching search pattern '{pattern}' found in directory '{directory}'. Skipping import.", pattern, directory.FullName);
            return;
        }
        stopwatch.Restart();
        var count = 0;
        foreach (var file in files)
        {
            try
            {
                var extension = file.FullName.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                var serializer = extension?.ToLowerInvariant() switch
                {
                    "json" => (ITextSerializer)this.JsonSerializer,
                    "yml" or "yaml" => this.YamlSerializer,
                    _ => throw new NotSupportedException($"The specified extension '{extension}' is not supported for static resource files")
                };
                using var stream = file.OpenRead();
                using var streamReader = new StreamReader(stream);
                var text = await streamReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                var broker = serializer.Deserialize<Broker>(text)!;
                await resources.AddAsync(broker, false, cancellationToken).ConfigureAwait(false);
                this.Logger.LogInformation("Successfully imported broker '{subscription}' from file '{file}'", $"{broker.Metadata.Name}", file.FullName);
                count++;
            }
            catch (Exception ex)
            {
                this.Logger.LogError("An error occurred while reading a broker from file '{file}': {ex}", file.FullName, ex);
                continue;
            }
        }
        stopwatch.Stop();
        this.Logger.LogInformation("Completed importing {count} broker in {ms} milliseconds", count, stopwatch.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// Provisions the solution's database with subscriptions
    /// </summary>
    /// <param name="resources">The <see cref="IResourceRepository"/> to provision</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task ProvisionSubscriptionAsync(IResourceRepository resources, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var stopwatch = new Stopwatch();
        var directory = new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "data", "seed", "subscriptions"));
        if (!directory.Exists) return;
        this.Logger.LogInformation("Starting importing subscriptions from directory '{directory}'...", directory.FullName);
        var pattern = "*.*";
        var files = directory.GetFiles(pattern, SearchOption.AllDirectories).Where(f => f.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || f.FullName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || f.FullName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase));
        if (!files.Any())
        {
            this.Logger.LogWarning("No subscription static resource files matching search pattern '{pattern}' found in directory '{directory}'. Skipping import.", pattern, directory.FullName);
            return;
        }
        stopwatch.Restart();
        var count = 0;
        foreach (var file in files)
        {
            try
            {
                var extension = file.FullName.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                var serializer = extension?.ToLowerInvariant() switch
                {
                    "json" => (ITextSerializer)this.JsonSerializer,
                    "yml" or "yaml" => this.YamlSerializer,
                    _ => throw new NotSupportedException($"The specified extension '{extension}' is not supported for static resource files")
                };
                using var stream = file.OpenRead();
                using var streamReader = new StreamReader(stream);
                var text = await streamReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                var subscription = serializer.Deserialize<Subscription>(text)!;
                await resources.AddAsync(subscription, false, cancellationToken).ConfigureAwait(false);
                this.Logger.LogInformation("Successfully imported subscription '{subscription}' from file '{file}'", $"{subscription.Metadata.Name}", file.FullName);
                count++;
            }
            catch (Exception ex)
            {
                this.Logger.LogError("An error occurred while reading a subscription from file '{file}': {ex}", file.FullName, ex);
                continue;
            }
        }
        stopwatch.Stop();
        this.Logger.LogInformation("Completed importing {count} subscription in {ms} milliseconds", count, stopwatch.Elapsed.TotalMilliseconds);
    }

    /// <inheritdoc/>
    public virtual Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

}
