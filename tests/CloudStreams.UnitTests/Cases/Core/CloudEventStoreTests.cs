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

using Neuroglia.Data.Infrastructure.EventSourcing.Services;

namespace CloudStreams.UnitTests.Cases.Core;

public class CloudEventStoreTests
{

    static async IAsyncEnumerable<IEventRecord> ThrowsWhileEnumerating(string streamName)
    {
        await Task.Yield();
        throw new StreamNotFoundException(streamName);
#pragma warning disable CS0162 // makes the method an async iterator
        yield break;
#pragma warning restore CS0162
    }

    static CloudEventStore CreateStore(IEventStore? eventStore = null)
    {
        if (eventStore == null)
        {
            eventStore = Substitute.For<IEventStore>();
            eventStore.ReadAsync(Arg.Any<string>(), Arg.Any<StreamReadDirection>(), Arg.Any<long>(), Arg.Any<ulong?>(), Arg.Any<CancellationToken>())
                .Returns(call => ThrowsWhileEnumerating(call.ArgAt<string>(0)));
        }
        return new CloudEventStore(eventStore, Substitute.For<IProjectionManager>(), Neuroglia.Serialization.Json.JsonSerializer.Default);
    }

    [Fact]
    public async Task Reading_The_Stream_Of_An_Empty_Store_Should_Yield_Nothing()
    {
        // act
        var records = await CreateStore().ReadAsync(StreamReadDirection.Forwards, StreamPosition.StartOfStream).ToListAsync();

        // assert
        Assert.Empty(records);
    }

    [Theory]
    [InlineData(CloudEventPartitionType.BySource, "https://cloud-streams.io/tests")]
    [InlineData(CloudEventPartitionType.BySubject, "subject")]
    [InlineData(CloudEventPartitionType.ByType, "com.cloud-streams.test.v1")]
    [InlineData(CloudEventPartitionType.ByCorrelationId, "correlation")]
    [InlineData(CloudEventPartitionType.ByCausationId, "causation")]
    public async Task Reading_A_Partition_Whose_Stream_Does_Not_Exist_Should_Fail_With_A_Not_Found_Problem(CloudEventPartitionType type, string id)
    {
        // act
        var exception = await Assert.ThrowsAsync<ProblemDetailsException>(() => CreateStore().ReadPartitionAsync(new(type, id), StreamReadDirection.Forwards, StreamPosition.StartOfStream).ToListAsync().AsTask());

        // assert
        Assert.Equal((int)HttpStatusCode.NotFound, exception.Problem.Status);
    }

    [Fact]
    public async Task Reading_A_Source_Partition_Whose_Id_Is_Not_A_Uri_Should_Fail_With_A_Bad_Request_Problem()
    {
        // act
        var exception = Assert.Throws<ProblemDetailsException>(() => CreateStore().ReadPartitionAsync(new(CloudEventPartitionType.BySource, "http://[invalid"), StreamReadDirection.Forwards, StreamPosition.StartOfStream));

        // assert
        Assert.Equal((int)HttpStatusCode.BadRequest, exception.Problem.Status);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task The_Metadata_Of_An_Existing_But_Empty_Stream_Should_Describe_An_Empty_Stream()
    {
        // arrange
        var eventStore = Substitute.For<IEventStore>();
        eventStore.ReadAsync(Arg.Any<string>(), Arg.Any<StreamReadDirection>(), Arg.Any<long>(), Arg.Any<ulong?>(), Arg.Any<CancellationToken>()).Returns(AsyncEnumerable.Empty<IEventRecord>());

        // act
        var metadata = await CreateStore(eventStore).GetStreamMetadataAsync();

        // assert
        Assert.Null(metadata.Length);
        Assert.Null(metadata.LastEvent);
    }

}
