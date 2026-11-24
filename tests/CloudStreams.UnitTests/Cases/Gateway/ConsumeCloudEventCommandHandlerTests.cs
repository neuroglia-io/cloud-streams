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
using CloudStreams.Gateway.Application.Commands.CloudEvents;

namespace CloudStreams.UnitTests.Cases.Gateway;

/// <summary>
/// Covers the metrics recorded by the <see cref="ConsumeCloudEventCommandHandler"/> (see #202)
/// </summary>
public class ConsumeCloudEventCommandHandlerTests
{

    static readonly CloudEvent Event = new() { Id = "1", SpecVersion = CloudEventSpecVersion.V1.Version, Source = new("https://orders.example.com"), Type = "order.placed" };

    static ICloudEventAdmissionControl Admits()
    {
        var admission = Substitute.For<ICloudEventAdmissionControl>();
        admission.EvaluateAsync(Arg.Any<CloudEvent>(), Arg.Any<IReadOnlyCollection<string>?>(), Arg.Any<CancellationToken>()).Returns(new OperationResult<CloudEventDescriptor>((int)HttpStatusCode.OK, new CloudEventDescriptor(new(), null)));
        return admission;
    }

    [Fact]
    public async Task A_Stored_Event_Should_Be_Counted_As_Ingested_And_Its_Ingestion_Timed()
    {
        // arrange
        var metrics = Substitute.For<IGatewayMetrics>();
        var handler = new ConsumeCloudEventCommandHandler(Admits(), metrics, Substitute.For<ICloudEventStore>());

        // act
        var result = await handler.HandleAsync(new ConsumeEventCommand(Event), CancellationToken.None);

        // assert
        Assert.Equal((int)HttpStatusCode.Created, result.Status);
        metrics.Received(1).IncrementTotalIngestedEvents(Event.Source);
        metrics.Received(1).RecordIngestionDuration(IngestionOutcome.Stored, Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task An_Event_That_Fails_To_Be_Appended_Should_Be_Counted_As_An_Append_Failure()
    {
        // arrange
        var metrics = Substitute.For<IGatewayMetrics>();
        var store = Substitute.For<ICloudEventStore>();
        store.AppendAsync(Arg.Any<CloudEventDescriptor>(), Arg.Any<CancellationToken>()).Returns<Task<CloudEventRecord>>(_ => throw new InvalidOperationException("The event store is unavailable"));
        var handler = new ConsumeCloudEventCommandHandler(Admits(), metrics, store);

        // act
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(new ConsumeEventCommand(Event), CancellationToken.None));

        // assert
        metrics.Received(1).IncrementTotalAppendFailures(Event.Source);
        metrics.DidNotReceive().IncrementTotalIngestedEvents(Arg.Any<Uri>());
        metrics.Received(1).RecordIngestionDuration(IngestionOutcome.Failed, Arg.Any<TimeSpan>());
    }

    [Theory]
    [InlineData(HttpStatusCode.Accepted, IngestionOutcome.Ignored)]
    [InlineData(HttpStatusCode.BadRequest, IngestionOutcome.Invalid)]
    [InlineData(HttpStatusCode.Forbidden, IngestionOutcome.Rejected)]
    [InlineData(HttpStatusCode.Conflict, IngestionOutcome.Rejected)]
    [InlineData(HttpStatusCode.ServiceUnavailable, IngestionOutcome.Failed)]
    public async Task An_Event_That_Is_Not_Admitted_Should_Have_Its_Ingestion_Timed_With_Its_Outcome(HttpStatusCode status, string outcome)
    {
        // arrange
        var metrics = Substitute.For<IGatewayMetrics>();
        var admission = Substitute.For<ICloudEventAdmissionControl>();
        admission.EvaluateAsync(Arg.Any<CloudEvent>(), Arg.Any<IReadOnlyCollection<string>?>(), Arg.Any<CancellationToken>()).Returns(new OperationResult<CloudEventDescriptor>((int)status));
        var store = Substitute.For<ICloudEventStore>();
        var handler = new ConsumeCloudEventCommandHandler(admission, metrics, store);

        // act
        await handler.HandleAsync(new ConsumeEventCommand(Event), CancellationToken.None);

        // assert
        metrics.Received(1).RecordIngestionDuration(outcome, Arg.Any<TimeSpan>());
        await store.DidNotReceive().AppendAsync(Arg.Any<CloudEventDescriptor>(), Arg.Any<CancellationToken>());
    }

}
