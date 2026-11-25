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
using System.Collections.Concurrent;
using System.Diagnostics;
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

    /// <summary>
    /// Gets the maximum amount of distinct sources used as labels. Admitted events whose source is not one of them are labelled <see cref="OtherSource"/>
    /// </summary>
    public const int MaxSourceLabels = 100;

    /// <summary>
    /// Gets the label of the sources past the <see cref="MaxSourceLabels"/> first ones
    /// </summary>
    public const string OtherSource = "other";

    /// <summary>
    /// Gets the bucket boundaries, in seconds, of the ingestion duration histogram, which are those of the HTTP server duration metric
    /// </summary>
    static readonly IReadOnlyList<double> IngestionDurationBuckets = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10];

    readonly ConcurrentDictionary<string, byte> _sourceLabels = new();
    bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="GatewayMetrics"/>
    /// </summary>
    public GatewayMetrics()
    {
        this.TotalIngestedEvents = this.Meter.CreateCounter<int>($"{CloudEventMetricsPrefix}ingested_count", "{event}", "The total amount of ingested cloud events");
        this.TotalInvalidEvents = this.Meter.CreateCounter<int>($"{CloudEventMetricsPrefix}invalid_count", "{event}", "The total amount of invalid cloud events");
        this.TotalRejectedEvents = this.Meter.CreateCounter<int>($"{CloudEventMetricsPrefix}rejected_count", "{event}", "The total amount of rejected cloud events");
        this.TotalIgnoredEvents = this.Meter.CreateCounter<int>($"{CloudEventMetricsPrefix}ignored_count", "{event}", "The total amount of cloud events ignored by the split-horizon policy");
        this.TotalDuplicateEvents = this.Meter.CreateCounter<int>($"{CloudEventMetricsPrefix}duplicate_count", "{event}", "The total amount of cloud events published again within the deduplication window, which have not been stored again");
        this.TotalAppendFailures = this.Meter.CreateCounter<int>($"{MetricsPrefix}append_failures_count", "{failure}", "The total amount of admitted cloud events that could not be appended to the event store");
        this.IngestionDuration = this.Meter.CreateHistogram<double>($"{MetricsPrefix}ingestion_duration", "s", "The duration of the ingestion of cloud events, from their admission to their storage",
            advice: new() { HistogramBucketBoundaries = IngestionDurationBuckets });
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

    /// <summary>
    /// Gets the <see cref="Counter{T}"/> used to count the cloud events ignored by the split-horizon policy
    /// </summary>
    protected Counter<int> TotalIgnoredEvents { get; }

    /// <summary>
    /// Gets the <see cref="Counter{T}"/> used to count the cloud events published again within the deduplication window
    /// </summary>
    protected Counter<int> TotalDuplicateEvents { get; }

    /// <summary>
    /// Gets the <see cref="Counter{T}"/> used to count the admitted cloud events that could not be appended to the event store
    /// </summary>
    protected Counter<int> TotalAppendFailures { get; }

    /// <summary>
    /// Gets the <see cref="Histogram{T}"/> used to record the duration of the ingestion of cloud events, in seconds
    /// </summary>
    protected Histogram<double> IngestionDuration { get; }

    /// <inheritdoc/>
    public virtual void IncrementTotalIngestedEvents(Uri source) => this.TotalIngestedEvents.Add(1, new KeyValuePair<string, object?>("source", this.GetSourceLabel(source)));

    /// <inheritdoc/>
    public virtual void IncrementTotalInvalidEvents(string reason) => this.TotalInvalidEvents.Add(1, new KeyValuePair<string, object?>("reason", reason));

    /// <inheritdoc/>
    public virtual void IncrementTotalRejectedEvents(string reason) => this.TotalRejectedEvents.Add(1, new KeyValuePair<string, object?>("reason", reason));

    /// <inheritdoc/>
    public virtual void IncrementTotalIgnoredEvents(Uri source) => this.TotalIgnoredEvents.Add(1, new KeyValuePair<string, object?>("source", this.GetSourceLabel(source)));

    /// <inheritdoc/>
    public virtual void IncrementTotalDuplicateEvents(Uri source) => this.TotalDuplicateEvents.Add(1, new KeyValuePair<string, object?>("source", this.GetSourceLabel(source)));

    /// <inheritdoc/>
    public virtual void IncrementTotalAppendFailures(Uri source) => this.TotalAppendFailures.Add(1, new KeyValuePair<string, object?>("source", this.GetSourceLabel(source)));

    /// <inheritdoc/>
    public virtual void RecordIngestionDuration(string outcome, TimeSpan duration) => this.IngestionDuration.Record(duration.TotalSeconds, new KeyValuePair<string, object?>("outcome", outcome));

    /// <summary>
    /// Gets the label of the specified source
    /// </summary>
    /// <param name="source">The source to get the label of</param>
    /// <returns>The source itself, if it is one of the <see cref="MaxSourceLabels"/> first distinct sources, or <see cref="OtherSource"/> otherwise</returns>
    /// <remarks>The query string of the source, if any, is left out: it is not used to tell sources apart, and could carry credentials</remarks>
    protected virtual string GetSourceLabel(Uri source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var label = source.IsAbsoluteUri ? source.GetLeftPart(UriPartial.Path) : source.OriginalString.Split('?', 2)[0];
        if (this._sourceLabels.ContainsKey(label)) return label;
        // the limit may be exceeded by a few concurrent additions, which is harmless: what matters is that it bounds the amount of labels
        if (this._sourceLabels.Count >= MaxSourceLabels) return OtherSource;
        this._sourceLabels.TryAdd(label, 0);
        return label;
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
