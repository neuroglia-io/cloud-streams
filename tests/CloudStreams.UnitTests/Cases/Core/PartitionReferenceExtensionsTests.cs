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

using CloudStreams.Core.Application;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the mapping of a <see cref="PartitionReference"/> onto the EventStore stream backing it
/// </summary>
/// <remarks>
/// The stream name is persisted state: change how it is derived and every existing partition silently becomes unreachable, so these are pinned deliberately.
/// </remarks>
public class PartitionReferenceExtensionsTests
{

    /// <summary>
    /// Regression guard for issue #66: partition ids containing <c>/</c> could not be resolved. The id must survive verbatim into the stream name.
    /// </summary>
    [Theory]
    [InlineData("https://server.com")]
    [InlineData("https://server.com/some/nested/path")]
    [InlineData("https://neuroglia.io/demo/api/v4")]
    public void GetStreamName_Should_Preserve_A_Source_Containing_Path_Segments(string source)
    {
        // arrange
        var partition = new PartitionReference(CloudEventPartitionType.BySource, source);

        // act
        var streamName = partition.GetStreamName();

        // assert
        Assert.Equal($"{Streams.ByCloudEventSourcePrefix}{source}", streamName);
        Assert.EndsWith(source, streamName);
    }

    /// <summary>
    /// Sibling of issue #99: a source of <c>https://test.com</c> must not gain the trailing slash <see cref="Uri.ToString"/> adds,
    /// because that would resolve to a different stream than the one the events were written to.
    /// </summary>
    [Fact]
    public void GetStreamName_Should_Not_Append_A_Trailing_Slash_To_A_Root_Source()
    {
        // arrange
        var partition = new PartitionReference(CloudEventPartitionType.BySource, "https://test.com");

        // act
        var streamName = partition.GetStreamName();

        // assert
        Assert.Equal($"{Streams.ByCloudEventSourcePrefix}https://test.com", streamName);
        Assert.DoesNotContain("test.com/", streamName);
    }

    [Theory]
    [InlineData(CloudEventPartitionType.BySubject, Streams.ByCloudEventSubjectPrefix)]
    [InlineData(CloudEventPartitionType.ByType, Streams.ByCloudEventTypePrefix)]
    [InlineData(CloudEventPartitionType.ByCorrelationId, Streams.ByCorrelationIdPrefix)]
    [InlineData(CloudEventPartitionType.ByCausationId, Streams.ByCausationIdPrefix)]
    public void GetStreamName_Should_Prefix_NonSource_Partitions_With_Their_Type(CloudEventPartitionType type, string expectedPrefix)
    {
        // arrange
        var partition = new PartitionReference(type, "some.partition.id");

        // act
        var streamName = partition.GetStreamName();

        // assert
        Assert.Equal($"{expectedPrefix}some.partition.id", streamName);
        Assert.True(Streams.IsPartition(streamName, type));
    }

}
