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

using CloudStreams.Core.Api.Configuration;
using CloudStreams.Core.Application.Services;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;
using HealthCheckResult = Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult;

namespace CloudStreams.Core.Api.HealthChecks;

/// <summary>
/// Exposes the health checks of Cloud Streams applications
/// </summary>
/// <remarks>
/// <para>
/// <c>/healthz/live</c> tells whether the process is alive, and runs no check. <c>/healthz</c> tells whether the application is ready to serve: it checks Redis,
/// EventStoreDB, the projections Cloud Streams relies on and, on gateways, whether the admission configuration has been loaded, and answers <c>503</c> if any of them
/// is unhealthy (see #201).
/// </para>
/// <para>
/// When the dashboard requires users to sign in, only authenticated callers get the details of each check: anonymous ones only get the overall status.
/// </para>
/// </remarks>
public static class CloudStreamsHealthChecks
{

    /// <summary>
    /// Gets the path of the liveness endpoint
    /// </summary>
    public const string LivenessPath = "/healthz/live";

    /// <summary>
    /// Gets the path of the readiness endpoint
    /// </summary>
    public const string ReadinessPath = "/healthz";

    /// <summary>
    /// Adds the health checks shared by all Cloud Streams applications
    /// </summary>
    /// <param name="healthChecks">The <see cref="IHealthChecksBuilder"/> to configure</param>
    /// <returns>The configured <see cref="IHealthChecksBuilder"/></returns>
    public static IHealthChecksBuilder AddCloudStreamsHealthChecks(this IHealthChecksBuilder healthChecks)
    {
        return healthChecks
            .AddCheck<RedisHealthCheck>("redis")
            .AddCheck<EventStoreHealthCheck>("eventstore")
            .AddCheck<ProjectionsHealthCheck>("projections");
    }

    /// <summary>
    /// Exposes the liveness and readiness endpoints
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/> to configure</param>
    /// <returns>The configured <see cref="IApplicationBuilder"/></returns>
    public static IApplicationBuilder UseCloudStreamsHealthChecks(this IApplicationBuilder app)
    {
        app.UseHealthChecks(LivenessPath, new HealthCheckOptions() { Predicate = _ => false });
        app.UseHealthChecks(ReadinessPath, new HealthCheckOptions() { ResponseWriter = WriteResponseAsync });
        return app;
    }

    /// <summary>
    /// Writes the specified <see cref="HealthReport"/>, in detail only if the caller is allowed to see the details
    /// </summary>
    /// <param name="context">The current <see cref="HttpContext"/></param>
    /// <param name="report">The <see cref="HealthReport"/> to write</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        var options = context.RequestServices.GetService<CloudStreamsAuthenticationOptions>();
        var detailed = options?.Dashboard.Enabled != true || context.User.Identity?.IsAuthenticated == true;
        if (detailed) return UIResponseWriter.WriteHealthCheckUIResponse(context, report);
        context.Response.ContentType = "text/plain";
        return context.Response.WriteAsync(report.Status.ToString());
    }

}

/// <summary>
/// Represents the health check of the Redis database that stores resources
/// </summary>
/// <param name="serviceProvider">The current <see cref="IServiceProvider"/></param>
/// <param name="configuration">The current <see cref="IConfiguration"/></param>
public class RedisHealthCheck(IServiceProvider serviceProvider, IConfiguration configuration)
    : IHealthCheck, IDisposable
{

    readonly Lazy<IConnectionMultiplexer> _connection = new(() =>
    {
        var registered = serviceProvider.GetService<IConnectionMultiplexer>();
        if (registered != null) return registered;
        var options = ConfigurationOptions.Parse(configuration.GetConnectionString("redis")!);
        options.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(options);
    });

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await this._connection.Value.GetDatabase().PingAsync().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy($"Redis answered in {latency.TotalMilliseconds:0}ms");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Redis is unreachable", ex);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // only the connection this check created is disposed of: a registered one belongs to the container
        if (this._connection.IsValueCreated && serviceProvider.GetService<IConnectionMultiplexer>() == null) this._connection.Value.Dispose();
        GC.SuppressFinalize(this);
    }

}

/// <summary>
/// Represents the health check of the EventStoreDB database that stores cloud events
/// </summary>
/// <param name="eventStore">The service used to store cloud events</param>
public class EventStoreHealthCheck(ICloudEventStore eventStore)
    : IHealthCheck
{

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await eventStore.GetStreamMetadataAsync(timeout.Token).ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("EventStoreDB is unreachable", ex);
        }
    }

}

/// <summary>
/// Represents the health check of the projections Cloud Streams relies on to partition cloud events
/// </summary>
/// <param name="eventStore">The service used to store cloud events</param>
public class ProjectionsHealthCheck(CloudEventStore eventStore)
    : IHealthCheck
{

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var missing = eventStore.MissingProjections;
        if (missing == null) return Task.FromResult(HealthCheckResult.Degraded("The projections have not been set up yet"));
        if (missing.Count > 0) return Task.FromResult(HealthCheckResult.Unhealthy($"The following projections could not be set up: {string.Join(", ", missing)}"));
        return Task.FromResult(HealthCheckResult.Healthy());
    }

}
