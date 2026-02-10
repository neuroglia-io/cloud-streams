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

using System.Diagnostics.Metrics;

namespace CloudStreams.Broker.Application.Services;

/// <summary>
/// Represents the default implementation of the <see cref="IBrokerMetrics"/> interface
/// </summary>
public class BrokerMetrics
    : IBrokerMetrics
{

    const string MetricsPrefix = "cloud_streams_broker_";
    const string CloudEventMetricsPrefix = MetricsPrefix + "events_";

    bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="BrokerMetrics"/>
    /// </summary>
    public BrokerMetrics()
    {
        this.TotalPublishedEvents = this.Meter.CreateCounter<int>($"{CloudEventMetricsPrefix}published_count", "Cloud Event", "The total amount of cloud events published (dispatched) to subscribers");
        this.TotalDeliveryFailures = this.Meter.CreateCounter<int>($"{MetricsPrefix}delivery_failures_count", "Failure", "The total amount of delivery failures");
    }

    /// <summary>
    /// Gets the <see cref="System.Diagnostics.Metrics.Meter"/>
    /// </summary>
    protected Meter Meter { get; } = new(CloudStreamsDefaults.Telemetry.ActivitySource.Name, CloudStreamsDefaults.Telemetry.ActivitySource.Version);

    /// <summary>
    /// Gets the <see cref="Counter{T}"/> used to keep track of the total amount of published <see cref="CloudEvent"/>s
    /// </summary>
    protected Counter<int> TotalPublishedEvents { get; }

    /// <summary>
    /// Gets the <see cref="Counter{T}"/> used to keep track of the total amount of delivery failures
    /// </summary>
    protected Counter<int> TotalDeliveryFailures { get; }

    /// <inheritdoc/>
    public virtual void IncrementTotalPublishedEvents(string? stream, string? subscriber)
    {
        this.TotalPublishedEvents.Add(1, new KeyValuePair<string, object?>("stream", stream), new KeyValuePair<string, object?>("subscriber", subscriber));
    }

    /// <inheritdoc/>
    public virtual void IncrementTotalDeliveryFailures(string? stream, string? subscriber)
    {
        this.TotalDeliveryFailures.Add(1, new KeyValuePair<string, object?>("stream", stream), new KeyValuePair<string, object?>("subscriber", subscriber));
    }

    /// <summary>
    /// Disposes of the <see cref="BrokerMetrics"/>
    /// </summary>
    /// <param name="disposing">A boolean indicating whether or not the <see cref="BrokerMetrics"/> is being disposed of</param>
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
