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
using System.Diagnostics.Metrics;

namespace CloudStreams.Gateway.Application.Services;

/// <summary>
/// Represents the default implementation of the <see cref="IGatewayMetrics"/> interface
/// </summary>
public class GatewayMetrics
    : IGatewayMetrics
{

    const string MetricsPrefix = "cloud_streams_gateway_";
    const string CloudEventMetricsPrefix = MetricsPrefix + "events_";

    bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="GatewayMetrics"/>
    /// </summary>
    public GatewayMetrics()
    {
        this.TotalIngestedEvents = this.Meter.CreateCounter<int>($"{CloudEventMetricsPrefix}ingested_count", "Cloud Event", "The total amount of ingested cloud events");
        this.TotalInvalidEvents = this.Meter.CreateCounter<int>($"{CloudEventMetricsPrefix}invalid_count", "Cloud Event", "The total amount of invalid cloud events");
        this.TotalRejectedEvents = this.Meter.CreateCounter<int>($"{CloudEventMetricsPrefix}rejected_count", "Cloud Event", "The total amount of rejected cloud events");
    }

    /// <summary>
    /// Gets the <see cref="System.Diagnostics.Metrics.Meter"/>
    /// </summary>
    protected Meter Meter { get; } = new(CloudStreamsDefaults.Telemetry.ActivitySource.Name, CloudStreamsDefaults.Telemetry.ActivitySource.Version);

    /// <summary>
    /// Gets the <see cref="Counter{T}"/> used to keep track of the total amount of ingested <see cref="CloudEvent"/>s
    /// </summary>
    protected Counter<int> TotalIngestedEvents { get; }

    /// <summary>
    /// Gets the <see cref="Counter{T}"/> used to keep track of the total amount of invalid <see cref="CloudEvent"/>s
    /// </summary>
    protected Counter<int> TotalInvalidEvents { get; }

    /// <summary>
    /// Gets the <see cref="Counter{T}"/> used to keep track of the total amount of rejected <see cref="CloudEvent"/>s
    /// </summary>
    protected Counter<int> TotalRejectedEvents { get; }

    /// <inheritdoc/>
    public virtual void IncrementTotalIngestedEvents()
    {
        this.TotalIngestedEvents.Add(1);
    }

    /// <inheritdoc/>
    public virtual void IncrementTotalInvalidEvents()
    {
        this.TotalInvalidEvents.Add(1);
    }

    /// <inheritdoc/>
    public virtual void IncrementTotalRejectedEvents()
    {
        this.TotalRejectedEvents.Add(1);
    }

    /// <summary>
    /// Disposes of the <see cref="GatewayMetrics"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="GatewayMetrics"/> is being disposed of</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this._disposed)
        {
            if (disposing)
            {
                this.Meter.Dispose();
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

}