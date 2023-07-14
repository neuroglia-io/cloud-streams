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
using Hylo;
using Hylo.Infrastructure.Services;
using Json.Patch;
using Json.Pointer;
using System.Diagnostics;
using System.Net;
using System.Net.Mime;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Represents a service used to monitor the health of a gateway
/// </summary>
public class GatewayHealthMonitor
    : IHostedService, IDisposable, IAsyncDisposable
{

    const int DefaultInterval = 15;
    bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="GatewayHealthMonitor"/>
    /// </summary>
    /// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
    /// <param name="repository">The service used to manage resources</param>
    /// <param name="monitor">The service used to monitor the handled <see cref="Data.Gateway"/></param>
    /// <param name="httpClient">The service used to perform HTTP requests</param>
    public GatewayHealthMonitor(ILoggerFactory loggerFactory, IRepository repository, IResourceMonitor<Gateway> monitor, HttpClient httpClient)
    {
        this.Logger = loggerFactory.CreateLogger(this.GetType());
        this.Repository = repository;
        this.Monitor = monitor;
        this.HttpClient = httpClient;
    }

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the service used to manage resources
    /// </summary>
    protected IRepository Repository { get; }

    /// <summary>
    /// Gets the service used to monitor the handled <see cref="Data.Gateway"/>
    /// </summary>
    protected IResourceMonitor<Gateway> Monitor { get; }

    /// <summary>
    /// Gets the service used to perform HTTP requests
    /// </summary>
    protected HttpClient HttpClient { get; }

    /// <summary>
    /// Gets the monitored <see cref="Data.Gateway"/>
    /// </summary>
    protected Gateway Gateway => this.Monitor.Resource;

    /// <summary>
    /// Gets the <see cref="Timer"/> used to periodically check the health of the <see cref="Gateway"/>
    /// </summary>
    protected Timer? HealthCheckTimer { get; private set; }

    /// <summary>
    /// Gets the <see cref="GatewayHealthMonitor"/>'s <see cref="System.Threading.CancellationTokenSource"/>
    /// </summary>
    protected CancellationTokenSource? CancellationTokenSource { get; private set; }

    /// <inheritdoc/>
    public virtual Task StartAsync(CancellationToken cancellationToken)
    {
        this.CancellationTokenSource = new();
        if (this.Gateway.Spec.Service == null || this.Gateway.Spec.Service.HealthChecks == null) return Task.CompletedTask;
        this.Monitor.Select(e => e.Resource.Spec.Service?.HealthChecks).DistinctUntilChanged().SubscribeAsync(this.OnHealthChecksConfigurationChangedAsync, this.CancellationTokenSource.Token);
        this.HealthCheckTimer = new Timer(this.OnHealthCheckIntervalEllapsedAsync, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual async Task StopAsync(CancellationToken cancellationToken)
    {
        this.CancellationTokenSource?.Cancel();
        if(this.HealthCheckTimer != null) await this.HealthCheckTimer.DisposeAsync().ConfigureAwait(false);
        this.HealthCheckTimer = null;
    }

    /// <summary>
    /// Handles changes to the <see cref="Gateway"/>'s <see cref="ServiceHealthCheckConfiguration"/>
    /// </summary>
    /// <param name="configuration">The updated <see cref="ServiceHealthCheckConfiguration"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task OnHealthChecksConfigurationChangedAsync(ServiceHealthCheckConfiguration? configuration)
    {
        if (this.HealthCheckTimer != null) await this.HealthCheckTimer.DisposeAsync().ConfigureAwait(false);
        this.HealthCheckTimer = null;
        if (this.Gateway.Spec.Service == null || this.Gateway.Spec.Service.HealthChecks == null) return;
        var delay = this.Gateway.Status?.LastHealthCheckAt.HasValue == true ? DateTimeOffset.Now - this.Gateway.Status.LastHealthCheckAt : TimeSpan.Zero;
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
        if (this.Gateway.Spec.Service.HealthChecks.Interval.HasValue && this.Gateway.Spec.Service.HealthChecks.Interval > delay) delay = this.Gateway.Spec.Service.HealthChecks.Interval;
        this.HealthCheckTimer = new Timer(this.OnHealthCheckIntervalEllapsedAsync, null, this.Gateway.Spec.Service.HealthChecks.Interval ?? TimeSpan.FromSeconds(DefaultInterval), Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Handles ticks of the <see cref="HealthCheckTimer"/>
    /// </summary>
    /// <param name="state">The timer's async state</param>
    protected virtual async void OnHealthCheckIntervalEllapsedAsync(object? state)
    {
        try
        {
            if (this.HealthCheckTimer != null) await this.HealthCheckTimer.DisposeAsync().ConfigureAwait(false);
            this.HealthCheckTimer = null;
            if (this.Gateway.Spec.Service == null || this.Gateway.Spec.Service.HealthChecks == null) return;
            var hostNameAndPort = this.Gateway.Spec.Service.Uri.OriginalString;
            if (hostNameAndPort.EndsWith('/')) hostNameAndPort = hostNameAndPort[..^1];
            var path = this.Gateway.Spec.Service.HealthChecks.Request.Path;
            if (!path.StartsWith('/')) path = $"/{path}";
            var uri = $"{hostNameAndPort}{path}";
            using var request = new HttpRequestMessage(new HttpMethod(this.Gateway.Spec.Service.HealthChecks.Request.Method), uri);
            if (this.Gateway.Spec.Service.HealthChecks.Request.Headers != null) this.Gateway.Spec.Service.HealthChecks.Request.Headers!.ToList().ForEach(h => request.Headers.TryAddWithoutValidation(h.Key, h.Value));
            if (this.Gateway.Spec.Service.HealthChecks.Request.Body != null) request.Content = new StringContent(Serializer.Json.Serialize(this.Gateway.Spec.Service.HealthChecks.Request.Body), Encoding.UTF8, MediaTypeNames.Application.Json);
            HealthCheckResponse? healthCheckResponse = null;
            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                using var response = await this.HttpClient.SendAsync(request, this.CancellationTokenSource!.Token).ConfigureAwait(false);
                stopwatch.Stop();
                var content = await response.Content.ReadAsStringAsync(this.CancellationTokenSource.Token).ConfigureAwait(false);
                try
                {
                    try
                    {
                        healthCheckResponse = Serializer.Json.Deserialize<HealthCheckResponse>(content)!;
                    }
                    catch { }
                    if(healthCheckResponse == null)
                    {
                        var result = Serializer.Json.Deserialize<JsonObject>(content);
                        if (result?.TryGetPropertyValue(nameof(HealthCheckResult.Status).ToCamelCase(), out var node) == true && node != null) healthCheckResponse = new(node.GetValue<string>().ToCamelCase());
                        else healthCheckResponse = new(response.IsSuccessStatusCode ? HealthStatus.Healthy : HealthStatus.Unhealthy);
                    } 
                }
                catch
                {
                    healthCheckResponse = new(HealthStatus.Unhealthy);
                }
                healthCheckResponse.Status = healthCheckResponse.Status.ToCamelCase();
            }
            catch (Exception ex)
            {
                this.Logger.LogWarning("An error occured while performing health check of gateway '{gateway}': {ex}", this.Gateway.GetQualifiedName(), ex);
                healthCheckResponse = new(HealthStatus.Unhealthy);
            }
            if (healthCheckResponse.Status != this.Gateway.Status?.HealthStatus)
            {
                var patchSource = this.Gateway.Clone()!;
                var patchTarget = this.Gateway.Clone()!;
                patchTarget.Status ??= new GatewayStatus();
                patchTarget.Status.HealthStatus = healthCheckResponse.Status;
                patchTarget.Status.LastHealthCheckAt = DateTimeOffset.Now;
                var patch = new Patch(PatchType.JsonPatch, JsonPatchHelper.CreateJsonPatchFromDiff(patchSource, patchTarget));
                await this.Repository.PatchStatusAsync<Gateway>(patch, this.Gateway.GetName(), this.Gateway.GetNamespace(), false, this.CancellationTokenSource!.Token).ConfigureAwait(false);
            }
        }
        catch(Exception ex)
        {
            this.Logger.LogError("An error occured while checking health of gateway '{gateway}': {ex}", this.Gateway.GetQualifiedName(), ex);
        }
        finally
        {
            if (this.Gateway.Spec.Service != null && this.Gateway.Spec.Service.HealthChecks != null)
            {
                this.HealthCheckTimer = new Timer(this.OnHealthCheckIntervalEllapsedAsync, null, this.Gateway.Spec.Service.HealthChecks.Interval ?? TimeSpan.FromSeconds(DefaultInterval), Timeout.InfiniteTimeSpan);
            }
        }
    }

    /// <summary>
    /// Disposes of the <see cref="GatewayHealthMonitor"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="GatewayHealthMonitor"/> is being disposed of</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this._disposed)
        {
            if (disposing)
            {
                this.CancellationTokenSource?.Dispose();
                this.HealthCheckTimer?.Dispose();
                this.HealthCheckTimer = null;
            }
            this._disposed = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of the <see cref="GatewayHealthMonitor"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="GatewayHealthMonitor"/> is being disposed of</param>
    /// <returns>A new awaitable <see cref="ValueTask"/></returns>
    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (!this._disposed)
        {
            if (disposing)
            {
                this.CancellationTokenSource?.Dispose();
                if (this.HealthCheckTimer != null) await this.HealthCheckTimer.DisposeAsync().ConfigureAwait(false);
                this.HealthCheckTimer = null;
            }
            this._disposed = true;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.DisposeAsync(disposing: true);
        GC.SuppressFinalize(this);
    }

}