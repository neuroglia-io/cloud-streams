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

namespace CloudStreams.UnitTests.Cases.Broker;

/// <summary>
/// Covers the instruments of the <see cref="BrokerMetrics"/> (see #202)
/// </summary>
public class BrokerMetricsTests
{

    [Fact]
    public void Measurements_Should_Be_Labelled_With_The_Subscription_And_Its_Stream_Rather_Than_The_Subscriber()
    {
        // arrange
        using var metrics = new BrokerMetrics();
        using var recorder = MeasurementRecorder.Listen(metrics);

        // act
        metrics.IncrementTotalPublishedEvents("orders.default", "All");
        metrics.RecordDeliveryDuration("orders.default", "All", DeliveryOutcome.Deferred, TimeSpan.FromMilliseconds(250));
        metrics.RecordAcknowledgementDuration("orders.default", "All", AcknowledgementOutcome.Acknowledged, TimeSpan.FromSeconds(3));

        // assert
        var published = Assert.Single(recorder.Of("cloud_streams_broker_events_published_count"));
        Assert.Equal(new Dictionary<string, object?>() { ["subscription"] = "orders.default", ["stream"] = "All" }, published.Tags);
        var delivery = Assert.Single(recorder.Of("cloud_streams_broker_delivery_duration"));
        Assert.Equal(0.25, delivery.Value);
        Assert.Equal(DeliveryOutcome.Deferred, delivery.Tags["outcome"]);
        var acknowledgement = Assert.Single(recorder.Of("cloud_streams_broker_ack_duration"));
        Assert.Equal(3d, acknowledgement.Value);
        Assert.Equal(AcknowledgementOutcome.Acknowledged, acknowledgement.Tags["outcome"]);
    }

    [Fact]
    public void Lag_Gauge_Should_Report_Every_Registered_Subscription_Until_It_Is_Unregistered()
    {
        // arrange
        using var metrics = new BrokerMetrics();
        using var recorder = MeasurementRecorder.Listen(metrics);
        using var orders = metrics.ObserveLag("orders.default", () => ("All", 12));
        var payments = metrics.ObserveLag("payments.default", () => ("$by-source-https://payments", 0));
        using var unknown = metrics.ObserveLag("unknown.default", () => null);
        using var failing = metrics.ObserveLag("failing.default", () => throw new InvalidOperationException());

        // act
        recorder.Collect();
        var registered = Lags(recorder);
        payments.Dispose();
        recorder.Collect();
        var unregistered = Lags(recorder).Skip(registered.Count);

        // assert - a subscription whose lag is unknown, or fails to be observed, is left out without preventing the others from being reported
        Assert.Equal([("orders.default", "All", 12d), ("payments.default", "$by-source-https://payments", 0d)], registered.Order());
        Assert.Equal([("orders.default", "All", 12d)], unregistered);
    }

    static List<(string Subscription, string Stream, double Lag)> Lags(MeasurementRecorder recorder) =>
        [.. recorder.Of("cloud_streams_broker_subscription_lag").Select(m => ((string)m.Tags["subscription"]!, (string)m.Tags["stream"]!, m.Value))];

}

/// <summary>
/// Records the measurements of the instruments of a <see cref="Meter"/>
/// </summary>
sealed class MeasurementRecorder
    : IDisposable
{

    readonly MeterListener _listener = new();
    readonly List<(string Instrument, double Value, Dictionary<string, object?> Tags)> _measurements = [];

    MeasurementRecorder(object metrics)
    {
        var meter = (Meter)metrics.GetType().GetProperty("Meter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(metrics)!;
        this._listener.InstrumentPublished = (instrument, listener) => { if (instrument.Meter == meter) listener.EnableMeasurementEvents(instrument); };
        this._listener.SetMeasurementEventCallback<int>((instrument, value, tags, _) => this.Record(instrument, value, tags));
        this._listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => this.Record(instrument, value, tags));
        this._listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => this.Record(instrument, value, tags));
        this._listener.Start();
    }

    public static MeasurementRecorder Listen(object metrics) => new(metrics);

    void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var dictionary = new Dictionary<string, object?>();
        foreach (var tag in tags) dictionary[tag.Key] = tag.Value;
        lock (this._measurements) this._measurements.Add((instrument.Name, value, dictionary));
    }

    public void Collect() => this._listener.RecordObservableInstruments();

    public IReadOnlyList<(double Value, Dictionary<string, object?> Tags)> Of(string instrument)
    {
        lock (this._measurements) return [.. this._measurements.Where(m => m.Instrument == instrument).Select(m => (m.Value, m.Tags))];
    }

    public void Dispose() => this._listener.Dispose();

}
