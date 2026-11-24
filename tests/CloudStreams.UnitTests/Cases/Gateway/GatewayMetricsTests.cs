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

using CloudStreams.Gateway.Application.Services;
using CloudStreams.UnitTests.Cases.Broker;

namespace CloudStreams.UnitTests.Cases.Gateway;

/// <summary>
/// Covers the instruments of the <see cref="GatewayMetrics"/> (see #202)
/// </summary>
public class GatewayMetricsTests
{

    [Fact]
    public void Sources_Should_Be_Labelled_Without_Their_Query_String()
    {
        // arrange
        using var metrics = new GatewayMetrics();
        using var recorder = MeasurementRecorder.Listen(metrics);

        // act
        metrics.IncrementTotalIngestedEvents(new("https://orders.example.com/api?apiKey=secret"));

        // assert
        Assert.Equal("https://orders.example.com/api", Assert.Single(recorder.Of("cloud_streams_gateway_events_ingested_count")).Tags["source"]);
    }

    [Fact]
    public void Sources_Past_The_Maximum_Should_Be_Labelled_Other()
    {
        // arrange
        using var metrics = new GatewayMetrics();
        using var recorder = MeasurementRecorder.Listen(metrics);
        for (var i = 0; i < GatewayMetrics.MaxSourceLabels; i++) metrics.IncrementTotalIngestedEvents(new($"urn:source:{i}"));

        // act
        metrics.IncrementTotalIngestedEvents(new("urn:source:new"));
        metrics.IncrementTotalIgnoredEvents(new("urn:source:0"));

        // assert - the sources already labelled keep their label
        Assert.Equal(GatewayMetrics.OtherSource, recorder.Of("cloud_streams_gateway_events_ingested_count").Last().Tags["source"]);
        Assert.Equal("urn:source:0", Assert.Single(recorder.Of("cloud_streams_gateway_events_ignored_count")).Tags["source"]);
    }

    [Fact]
    public void Refused_Events_Should_Be_Labelled_With_The_Reason_They_Were_Refused_For()
    {
        // arrange
        using var metrics = new GatewayMetrics();
        using var recorder = MeasurementRecorder.Listen(metrics);

        // act
        metrics.IncrementTotalRejectedEvents(RejectionReason.SplitHorizon);
        metrics.IncrementTotalInvalidEvents(InvalidityReason.Schema);
        metrics.RecordIngestionDuration(IngestionOutcome.Invalid, TimeSpan.FromMilliseconds(5));

        // assert
        Assert.Equal(new Dictionary<string, object?>() { ["reason"] = RejectionReason.SplitHorizon }, Assert.Single(recorder.Of("cloud_streams_gateway_events_rejected_count")).Tags);
        Assert.Equal(new Dictionary<string, object?>() { ["reason"] = InvalidityReason.Schema }, Assert.Single(recorder.Of("cloud_streams_gateway_events_invalid_count")).Tags);
        Assert.Equal(new Dictionary<string, object?>() { ["outcome"] = IngestionOutcome.Invalid }, Assert.Single(recorder.Of("cloud_streams_gateway_ingestion_duration")).Tags);
    }

}
