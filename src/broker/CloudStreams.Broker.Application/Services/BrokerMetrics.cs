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

using System.Collections.Concurrent;
using System.Diagnostics;
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

    /// <summary>
    /// Gets the bucket boundaries, in seconds, of the delivery duration histogram, which are those of the HTTP client duration metric
    /// </summary>
    static readonly IReadOnlyList<double> DeliveryDurationBuckets = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10, 30, 60];

    /// <summary>
    /// Gets the bucket boundaries, in seconds, of the acknowledgement duration histogram: subscribers that defer an acknowledgement typically take seconds to minutes
    /// </summary>
    static readonly IReadOnlyList<double> AcknowledgementDurationBuckets = [0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 120, 300, 600, 1800, 3600];

    readonly ConcurrentDictionary<object, (string Subscription, Func<(string Stream, ulong Lag)?> Observe)> _lagSources = new();
    bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="BrokerMetrics"/>
    /// </summary>
    public BrokerMetrics()
    {
        this.TotalPublishedEvents = this.Meter.CreateCounter<int>($"{CloudEventMetricsPrefix}published_count", "{event}", "The total amount of cloud events published (dispatched) to subscribers");
        this.TotalDeliveryFailures = this.Meter.CreateCounter<int>($"{MetricsPrefix}delivery_failures_count", "{failure}", "The total amount of delivery failures");
        this.DeliveryDuration = this.Meter.CreateHistogram<double>($"{MetricsPrefix}delivery_duration", "s", "The duration of delivery attempts, from the request to the subscriber's response, mutation included",
            advice: new() { HistogramBucketBoundaries = DeliveryDurationBuckets });
        this.PendingAcknowledgements = this.Meter.CreateUpDownCounter<int>($"{MetricsPrefix}pending_acks", "{event}", "The amount of cloud events whose acknowledgement has been deferred by the subscriber and is pending");
        this.AcknowledgementDuration = this.Meter.CreateHistogram<double>($"{MetricsPrefix}ack_duration", "s", "How long deferred acknowledgements have been pending before being acknowledged, negatively acknowledged or expiring",
            advice: new() { HistogramBucketBoundaries = AcknowledgementDurationBuckets });
        this.TotalAcknowledgementTimeouts = this.Meter.CreateCounter<int>($"{MetricsPrefix}ack_timeouts_count", "{timeout}", "The total amount of deferred acknowledgements that expired before the subscriber called back");
        this.TotalDeadLetters = this.Meter.CreateCounter<int>($"{MetricsPrefix}dead_letters_count", "{event}", "The total amount of cloud events that could not be delivered, and have been dead-lettered");
        this.Meter.CreateObservableGauge($"{MetricsPrefix}subscription_lag", this.ObserveLags, "{event}", "The amount of records of its stream a subscription has yet to handle");
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

    /// <summary>
    /// Gets the <see cref="Histogram{T}"/> used to record the duration of delivery attempts, in seconds
    /// </summary>
    protected Histogram<double> DeliveryDuration { get; }

    /// <summary>
    /// Gets the gauge used to track the amount of cloud events whose acknowledgement is pending
    /// </summary>
    protected UpDownCounter<int> PendingAcknowledgements { get; }

    /// <summary>
    /// Gets the <see cref="Histogram{T}"/> used to record how long deferred acknowledgements have been pending, in seconds
    /// </summary>
    protected Histogram<double> AcknowledgementDuration { get; }

    /// <summary>
    /// Gets the counter used to track the total amount of deferred acknowledgements that have expired
    /// </summary>
    protected Counter<int> TotalAcknowledgementTimeouts { get; }

    /// <summary>
    /// Gets the counter used to track the total amount of dead-lettered cloud events
    /// </summary>
    protected Counter<int> TotalDeadLetters { get; }

    /// <inheritdoc/>
    public virtual void IncrementTotalPublishedEvents(string subscription, string stream) => this.TotalPublishedEvents.Add(1, Tags(subscription, stream));

    /// <inheritdoc/>
    public virtual void IncrementTotalDeliveryFailures(string subscription, string stream) => this.TotalDeliveryFailures.Add(1, Tags(subscription, stream));

    /// <inheritdoc/>
    public virtual void RecordDeliveryDuration(string subscription, string stream, string outcome, TimeSpan duration) => this.DeliveryDuration.Record(duration.TotalSeconds, Tags(subscription, stream, outcome));

    /// <inheritdoc/>
    public virtual void AddPendingAcknowledgements(string subscription, string stream, int delta) => this.PendingAcknowledgements.Add(delta, Tags(subscription, stream));

    /// <inheritdoc/>
    public virtual void RecordAcknowledgementDuration(string subscription, string stream, string outcome, TimeSpan duration) => this.AcknowledgementDuration.Record(duration.TotalSeconds, Tags(subscription, stream, outcome));

    /// <inheritdoc/>
    public virtual void IncrementTotalAcknowledgementTimeouts(string subscription, string stream) => this.TotalAcknowledgementTimeouts.Add(1, Tags(subscription, stream));

    /// <inheritdoc/>
    public virtual void IncrementTotalDeadLetters(string subscription, string stream) => this.TotalDeadLetters.Add(1, Tags(subscription, stream));

    /// <inheritdoc/>
    public virtual IDisposable ObserveLag(string subscription, Func<(string Stream, ulong Lag)?> observe)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscription);
        ArgumentNullException.ThrowIfNull(observe);
        var key = new object();
        this._lagSources[key] = (subscription, observe);
        return new LagRegistration(() => this._lagSources.TryRemove(key, out _));
    }

    /// <summary>
    /// Observes the lag of every registered subscription
    /// </summary>
    /// <returns>The current lag of every registered subscription whose lag is known</returns>
    protected virtual IEnumerable<Measurement<long>> ObserveLags()
    {
        foreach (var (subscription, observe) in this._lagSources.Values)
        {
            (string Stream, ulong Lag)? lag;
            try { lag = observe(); }
            catch { continue; } // a subscription that fails to report its lag must not prevent the others from being reported
            if (lag.HasValue) yield return new((long)Math.Min(lag.Value.Lag, long.MaxValue), Tags(subscription, lag.Value.Stream));
        }
    }

    static TagList Tags(string subscription, string stream) => new() { { "subscription", subscription }, { "stream", stream } };

    static TagList Tags(string subscription, string stream, string outcome) => new() { { "subscription", subscription }, { "stream", stream }, { "outcome", outcome } };

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

    sealed class LagRegistration(Action unregister)
        : IDisposable
    {

        Action? _unregister = unregister;

        public void Dispose() => Interlocked.Exchange(ref this._unregister, null)?.Invoke();

    }

}
