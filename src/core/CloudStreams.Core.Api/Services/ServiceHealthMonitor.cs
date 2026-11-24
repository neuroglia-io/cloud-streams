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

using Neuroglia.Data.Infrastructure.ResourceOriented.Services;
using Neuroglia.Serialization;
using System.Net.Mime;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Represents the base class of services used to monitor the health of the service described by a resource
/// </summary>
/// <typeparam name="TResource">The type of the resource that describes the service to monitor</typeparam>
/// <param name="logger">The service used to perform logging</param>
/// <param name="serializer">The service used to serialize/deserialize objects to/from JSON</param>
/// <param name="repository">The service used to manage resources</param>
/// <param name="monitor">The service used to monitor the handled resource</param>
/// <param name="httpClient">The service used to perform HTTP requests</param>
/// <remarks>
/// A single timer is (re)scheduled after every check and whenever the health check configuration changes, so that checks never overlap, monitoring starts when
/// health checks are configured after the resource has been created, honours the time elapsed since the last check, and stops for good once the monitor has been
/// stopped (see #180)
/// </remarks>
public abstract class ServiceHealthMonitor<TResource>(ILogger logger, IJsonSerializer serializer, IResourceRepository repository, IResourceMonitor<TResource> monitor, HttpClient httpClient)
    : IHostedService, IDisposable, IAsyncDisposable
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Gets the default interval, in seconds, between health checks
    /// </summary>
    protected const int DefaultInterval = 15;

    readonly Lock _lock = new();
    IDisposable? _configurationSubscription;
    DateTimeOffset? _lastCheckAt;
    int _checking;
    bool _stopped;
    bool _disposed;

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; } = logger;

    /// <summary>
    /// Gets the service used to serialize/deserialize objects to/from JSON
    /// </summary>
    protected IJsonSerializer Serializer { get; } = serializer;

    /// <summary>
    /// Gets the service used to manage resources
    /// </summary>
    protected IResourceRepository Repository { get; } = repository;

    /// <summary>
    /// Gets the service used to monitor the handled resource
    /// </summary>
    protected IResourceMonitor<TResource> Monitor { get; } = monitor;

    /// <summary>
    /// Gets the service used to perform HTTP requests
    /// </summary>
    protected HttpClient HttpClient { get; } = httpClient;

    /// <summary>
    /// Gets the monitored resource
    /// </summary>
    protected TResource Resource => this.Monitor.Resource;

    /// <summary>
    /// Gets the <see cref="Timer"/> used to periodically check the health of the monitored service
    /// </summary>
    protected Timer? HealthCheckTimer { get; private set; }

    /// <summary>
    /// Gets the monitor's <see cref="System.Threading.CancellationTokenSource"/>
    /// </summary>
    protected CancellationTokenSource? CancellationTokenSource { get; private set; }

    /// <summary>
    /// Gets the configuration of the service described by the specified resource, if any
    /// </summary>
    /// <param name="resource">The resource to get the service configuration of</param>
    /// <returns>The configuration of the service described by the specified resource, if any</returns>
    protected abstract ServiceConfiguration? GetService(TResource resource);

    /// <summary>
    /// Gets the health status last reported for the specified resource, if any
    /// </summary>
    /// <param name="resource">The resource to get the health status of</param>
    /// <returns>The health status last reported for the specified resource, if any</returns>
    protected abstract string? GetHealthStatus(TResource resource);

    /// <summary>
    /// Gets the date and time at which the health of the specified resource was last reported, if ever
    /// </summary>
    /// <param name="resource">The resource to get the date and time of the last health check of</param>
    /// <returns>The date and time at which the health of the specified resource was last reported, if ever</returns>
    protected abstract DateTimeOffset? GetLastHealthCheckAt(TResource resource);

    /// <summary>
    /// Sets the health status of the specified resource
    /// </summary>
    /// <param name="resource">The resource to set the health status of</param>
    /// <param name="healthStatus">The health status to set</param>
    /// <param name="checkedAt">The date and time at which the health has been checked</param>
    protected abstract void SetHealthStatus(TResource resource, string healthStatus, DateTimeOffset checkedAt);

    /// <inheritdoc/>
    public virtual Task StartAsync(CancellationToken cancellationToken)
    {
        lock (this._lock)
        {
            if (this._disposed) throw new ObjectDisposedException(this.GetType().Name);
            this._stopped = false;
            this.CancellationTokenSource ??= new();
            this.HealthCheckTimer ??= new Timer(this.OnHealthCheckIntervalElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        // always observed, so that health checks configured after the resource's creation start monitoring
        this._configurationSubscription ??= this.Monitor.Select(e => this.GetService(e.Resource)?.HealthChecks).DistinctUntilChanged().Subscribe(_ => this.ScheduleNextCheck(resumed: true));
        this.ScheduleNextCheck(resumed: true);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual Task StopAsync(CancellationToken cancellationToken)
    {
        lock (this._lock)
        {
            this._stopped = true;
            this.HealthCheckTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        this._configurationSubscription?.Dispose();
        this._configurationSubscription = null;
        try { this.CancellationTokenSource?.Cancel(); }
        catch (ObjectDisposedException) { }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Schedules the next health check, if health checks are configured
    /// </summary>
    /// <param name="resumed">A boolean indicating whether or not monitoring is being (re)started, in which case the time elapsed since the last check is taken into account</param>
    protected virtual void ScheduleNextCheck(bool resumed)
    {
        lock (this._lock)
        {
            if (this._stopped || this._disposed || this.HealthCheckTimer == null) return;
            var healthChecks = this.GetService(this.Resource)?.HealthChecks;
            if (healthChecks == null)
            {
                this.HealthCheckTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                return;
            }
            var interval = healthChecks.Interval?.ToTimeSpan() ?? TimeSpan.FromSeconds(DefaultInterval);
            if (interval <= TimeSpan.Zero) interval = TimeSpan.FromSeconds(DefaultInterval);
            var dueTime = interval;
            if (resumed)
            {
                var lastCheckAt = new[] { this._lastCheckAt, this.GetLastHealthCheckAt(this.Resource) }.Max();
                dueTime = lastCheckAt.HasValue ? interval - (DateTimeOffset.UtcNow - lastCheckAt.Value) : TimeSpan.Zero;
                if (dueTime < TimeSpan.Zero) dueTime = TimeSpan.Zero;
                if (dueTime > interval) dueTime = interval;
            }
            this.HealthCheckTimer.Change(dueTime, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Handles ticks of the <see cref="HealthCheckTimer"/>
    /// </summary>
    /// <param name="state">The timer's async state</param>
    protected virtual async void OnHealthCheckIntervalElapsed(object? state)
    {
        // a check that is still running when the configuration changes reschedules itself once done: ticks must never overlap
        if (Interlocked.Exchange(ref this._checking, 1) == 1) return;
        try
        {
            await this.CheckHealthAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException) { }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "An error occurred while checking the health of {kind} '{name}'", this.Resource.Kind, this.Resource.GetQualifiedName());
        }
        finally
        {
            Interlocked.Exchange(ref this._checking, 0);
            this.ScheduleNextCheck(resumed: false);
        }
    }

    /// <summary>
    /// Checks the health of the monitored service, and reports it in the resource's status if it changed
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task CheckHealthAsync()
    {
        var cancellationToken = this.CancellationTokenSource?.Token ?? CancellationToken.None;
        var service = this.GetService(this.Resource);
        if (service?.HealthChecks == null) return;
        var baseUri = service.Uri.OriginalString.TrimEnd('/');
        var path = service.HealthChecks.Request.Path;
        if (!path.StartsWith('/')) path = $"/{path}";
        using var request = new HttpRequestMessage(new HttpMethod(service.HealthChecks.Request.Method), $"{baseUri}{path}");
        if (service.HealthChecks.Request.Headers != null) foreach (var header in service.HealthChecks.Request.Headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        if (service.HealthChecks.Request.Body != null) request.Content = new StringContent(this.Serializer.SerializeToText(service.HealthChecks.Request.Body), Encoding.UTF8, MediaTypeNames.Application.Json);
        string healthStatus;
        try
        {
            using var response = await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            healthStatus = this.ParseHealthStatus(content, response.IsSuccessStatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            this.Logger.LogWarning(ex, "An error occurred while performing the health check of {kind} '{name}'", this.Resource.Kind, this.Resource.GetQualifiedName());
            healthStatus = HealthStatus.Unhealthy;
        }
        var checkedAt = DateTimeOffset.UtcNow;
        this._lastCheckAt = checkedAt;
        if (healthStatus == this.GetHealthStatus(this.Resource)) return;
        var patchSource = this.Resource.Clone()!;
        var patchTarget = this.Resource.Clone()!;
        this.SetHealthStatus(patchTarget, healthStatus, checkedAt);
        var patch = new Patch(PatchType.JsonPatch, JsonPatchUtility.CreateJsonPatchFromDiff(patchSource, patchTarget));
        await this.Repository.PatchStatusAsync<TResource>(patch, this.Resource.GetName(), this.Resource.GetNamespace(), null, false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses the health status reported by the monitored service
    /// </summary>
    /// <param name="content">The content of the health check response</param>
    /// <param name="isSuccessStatusCode">A boolean indicating whether or not the health check response has a success status code</param>
    /// <returns>The health status reported by the monitored service</returns>
    protected virtual string ParseHealthStatus(string content, bool isSuccessStatusCode)
    {
        try
        {
            HealthCheckResponse? response = null;
            try { response = this.Serializer.Deserialize<HealthCheckResponse>(content); }
            catch { }
            if (response == null || string.IsNullOrWhiteSpace(response.Status))
            {
                var result = this.Serializer.Deserialize<JsonObject>(content);
                response = result?.TryGetPropertyValue(nameof(HealthCheckResult.Status).ToCamelCase(), out var node) == true && node != null
                    ? new(node.GetValue<string>())
                    : new(isSuccessStatusCode ? HealthStatus.Healthy : HealthStatus.Unhealthy);
            }
            return response.Status.ToCamelCase();
        }
        catch
        {
            return isSuccessStatusCode ? HealthStatus.Healthy : HealthStatus.Unhealthy;
        }
    }

    /// <summary>
    /// Disposes of the <see cref="ServiceHealthMonitor{TResource}"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="ServiceHealthMonitor{TResource}"/> is being disposed of</param>
    /// <returns>A new awaitable <see cref="ValueTask"/></returns>
    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        Timer? timer;
        lock (this._lock)
        {
            if (this._disposed) return;
            this._disposed = true;
            this._stopped = true;
            timer = this.HealthCheckTimer;
            this.HealthCheckTimer = null;
        }
        if (!disposing) return;
        this._configurationSubscription?.Dispose();
        try { this.CancellationTokenSource?.Cancel(); }
        catch (ObjectDisposedException) { }
        if (timer != null) await timer.DisposeAsync().ConfigureAwait(false);
        this.CancellationTokenSource?.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.DisposeAsync(disposing: true).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of the <see cref="ServiceHealthMonitor{TResource}"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="ServiceHealthMonitor{TResource}"/> is being disposed of</param>
    protected virtual void Dispose(bool disposing) => this.DisposeAsync(disposing).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

}
