using System.Text;
using System.Text.Json.Nodes;

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Represents a service used to monitor the health of a gateway
/// </summary>
/// <remarks>
/// Initializes a new <see cref="BrokerHealthMonitor"/>
/// </remarks>
/// <param name="logger">The service used to perform logging</param>
/// <param name="serializer">The  service used to serialize/deserialize objects to/from JSON</param>
/// <param name="repository">The service used to manage resources</param>
/// <param name="monitor">The service used to monitor the handled <see cref="Resources.Broker"/></param>
/// <param name="httpClient">The service used to perform HTTP requests</param>
public class BrokerHealthMonitor(ILogger<BrokerHealthMonitor> logger, IJsonSerializer serializer, IRepository repository, IResourceMonitor<Broker> monitor, HttpClient httpClient)
    : IHostedService, IDisposable, IAsyncDisposable
{

    const int DefaultInterval = 15;
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
    protected IRepository Repository { get; } = repository;

    /// <summary>
    /// Gets the service used to monitor the handled <see cref="Resources.Broker"/>
    /// </summary>
    protected IResourceMonitor<Broker> Monitor { get; } = monitor;

    /// <summary>
    /// Gets the service used to perform HTTP requests
    /// </summary>
    protected HttpClient HttpClient { get; } = httpClient;

    /// <summary>
    /// Gets the monitored <see cref="Resources.Broker"/>
    /// </summary>
    protected Broker Broker => this.Monitor.Resource;

    /// <summary>
    /// Gets the <see cref="Timer"/> used to periodically check the health of the <see cref="Resources.Broker"/>
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
        if (this.Broker.Spec.Service == null || this.Broker.Spec.Service.HealthChecks == null) return Task.CompletedTask;
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
    /// Handles changes to the <see cref="Resources.Broker"/>'s <see cref="ServiceHealthCheckConfiguration"/>
    /// </summary>
    /// <param name="configuration">The updated <see cref="ServiceHealthCheckConfiguration"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task OnHealthChecksConfigurationChangedAsync(ServiceHealthCheckConfiguration? configuration)
    {
        if (this.HealthCheckTimer != null) await this.HealthCheckTimer.DisposeAsync().ConfigureAwait(false);
        this.HealthCheckTimer = null;
        if (this.Broker.Spec.Service == null || this.Broker.Spec.Service.HealthChecks == null) return;
        var delay = this.Broker.Status?.LastHealthCheckAt.HasValue == true ? DateTimeOffset.Now - this.Broker.Status.LastHealthCheckAt : TimeSpan.Zero;
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
        if (this.Broker.Spec.Service.HealthChecks.Interval != null && this.Broker.Spec.Service.HealthChecks.Interval > delay) delay = this.Broker.Spec.Service.HealthChecks.Interval;
        this.HealthCheckTimer = new Timer(this.OnHealthCheckIntervalEllapsedAsync, null, this.Broker.Spec.Service.HealthChecks.Interval?.ToTimeSpan() ?? TimeSpan.FromSeconds(DefaultInterval), Timeout.InfiniteTimeSpan);
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
            if (this.Broker.Spec.Service == null || this.Broker.Spec.Service.HealthChecks == null) return;
            var hostNameAndPort = this.Broker.Spec.Service.Uri.OriginalString;
            if (hostNameAndPort.EndsWith('/')) hostNameAndPort = hostNameAndPort[..^1];
            var path = this.Broker.Spec.Service.HealthChecks.Request.Path;
            if (!path.StartsWith('/')) path = $"/{path}";
            var uri = $"{hostNameAndPort}{path}";
            using var request = new HttpRequestMessage(new HttpMethod(this.Broker.Spec.Service.HealthChecks.Request.Method), uri);
            if (this.Broker.Spec.Service.HealthChecks.Request.Headers != null) this.Broker.Spec.Service.HealthChecks.Request.Headers!.ToList().ForEach(h => request.Headers.TryAddWithoutValidation(h.Key, h.Value));
            if (this.Broker.Spec.Service.HealthChecks.Request.Body != null) request.Content = new StringContent(this.Serializer.SerializeToText(this.Broker.Spec.Service.HealthChecks.Request.Body), Encoding.UTF8, MediaTypeNames.Application.Json);
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
                        healthCheckResponse = this.Serializer.Deserialize<HealthCheckResponse>(content)!;
                    }
                    catch { }
                    if(healthCheckResponse == null)
                    {
                        var result = this.Serializer.Deserialize<JsonObject>(content);
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
                this.Logger.LogWarning("An error occured while performing health check of gateway '{gateway}': {ex}", this.Broker.GetQualifiedName(), ex);
                healthCheckResponse = new(HealthStatus.Unhealthy);
            }
            if (healthCheckResponse.Status != this.Broker.Status?.HealthStatus)
            {
                var patchSource = this.Broker.Clone()!;
                var patchTarget = this.Broker.Clone()!;
                patchTarget.Status ??= new();
                patchTarget.Status.HealthStatus = healthCheckResponse.Status;
                patchTarget.Status.LastHealthCheckAt = DateTimeOffset.Now;
                var patch = new Patch(PatchType.JsonPatch, JsonPatchUtility.CreateJsonPatchFromDiff(patchSource, patchTarget));
                await this.Repository.PatchStatusAsync<Broker>(patch, this.Broker.GetName(), this.Broker.GetNamespace(), false, this.CancellationTokenSource!.Token).ConfigureAwait(false);
            }
        }
        catch(Exception ex)
        {
            this.Logger.LogError("An error occured while checking health of gateway '{gateway}': {ex}", this.Broker.GetQualifiedName(), ex);
        }
        finally
        {
            if (this.Broker.Spec.Service != null && this.Broker.Spec.Service.HealthChecks != null)
            {
                this.HealthCheckTimer = new Timer(this.OnHealthCheckIntervalEllapsedAsync, null, this.Broker.Spec.Service.HealthChecks.Interval?.ToTimeSpan() ?? TimeSpan.FromSeconds(DefaultInterval), Timeout.InfiniteTimeSpan);
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