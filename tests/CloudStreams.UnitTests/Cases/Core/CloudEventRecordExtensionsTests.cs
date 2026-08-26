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
/// Covers the conversion of a stored <see cref="CloudEventRecord"/> back into the <see cref="CloudEvent"/> it describes.
/// </summary>
/// <remarks>
/// This conversion is what every subscriber ultimately receives, and it has regressed more than once. Each test here pins a specific closed issue.
/// </remarks>
public class CloudEventRecordExtensionsTests
{

    /// <summary>
    /// Regression guard for issue #63: the broker stamped <c>$traceId</c> and <c>$spanId</c> onto every dispatched event,
    /// which made subscribers reject them for failing CloudEvents spec validation.
    /// </summary>
    [Fact]
    public void ToCloudEvent_Should_Strip_NonStandard_Context_Attributes()
    {
        // arrange
        var record = CreateRecord(attributes =>
        {
            attributes["$traceId"] = "9ac45f5f402733bdb251b11024cf738a";
            attributes["$spanId"] = "6e15e720f0cf8a21";
            attributes["notLowercased"] = "nope";
        });

        // act
        var e = record.ToCloudEvent(CloudEventSequencingConfiguration.Default);

        // assert
        Assert.NotNull(e.ExtensionAttributes);
        Assert.DoesNotContain("$traceId", e.ExtensionAttributes!.Keys);
        Assert.DoesNotContain("$spanId", e.ExtensionAttributes!.Keys);
        Assert.DoesNotContain("notLowercased", e.ExtensionAttributes!.Keys);
    }

    /// <summary>
    /// Regression guard for issue #99: a source of <c>https://test.com</c> was surfaced as <c>https://test.com/</c>,
    /// because <see cref="Uri.ToString"/> appends the root path where <see cref="Uri.OriginalString"/> does not.
    /// </summary>
    [Fact]
    public void ToCloudEvent_Should_Preserve_The_Source_Without_Appending_A_Trailing_Slash()
    {
        // arrange
        var record = CreateRecord(attributes => attributes[CloudEventAttributes.Source] = "https://test.com");

        // act
        var e = record.ToCloudEvent(CloudEventSequencingConfiguration.Default);

        // assert
        Assert.Equal("https://test.com", e.Source.OriginalString);
    }

    /// <summary>
    /// Regression guard for pull request #71: the conversion assumed the event's data was always a JSON object, and threw when it was an array.
    /// </summary>
    [Fact]
    public void ToCloudEvent_Should_Support_Array_Data()
    {
        // arrange
        var record = CreateRecord(data: new[] { "first", "second" });

        // act
        var e = record.ToCloudEvent(CloudEventSequencingConfiguration.Default);

        // assert
        Assert.NotNull(e.Data);
    }

    [Fact]
    public void ToCloudEvent_Should_Sequence_Using_The_Configured_Attribute()
    {
        // arrange
        var record = CreateRecord(sequence: 37824);

        // act
        var e = record.ToCloudEvent(CloudEventSequencingConfiguration.Default);

        // assert
        Assert.NotNull(e.ExtensionAttributes);
        Assert.Equal(37824ul, Convert.ToUInt64(e.ExtensionAttributes![CloudEventExtensionAttributes.Sequence].ToString()));
    }

    [Fact]
    public void ToCloudEvent_Should_Not_Sequence_When_The_Strategy_Is_None()
    {
        // arrange
        var record = CreateRecord(sequence: 12);
        var sequencing = new CloudEventSequencingConfiguration() { Strategy = CloudEventSequencingStrategy.None };

        // act
        var e = record.ToCloudEvent(sequencing);

        // assert
        Assert.True(e.ExtensionAttributes == null || !e.ExtensionAttributes.ContainsKey(CloudEventExtensionAttributes.Sequence));
    }

    /// <summary>
    /// When the event already carries the sequencing attribute and the policy is to fall back, the record's own sequence must land on the fallback attribute
    /// rather than overwriting what the publisher sent.
    /// </summary>
    [Fact]
    public void ToCloudEvent_Should_Use_The_Fallback_Attribute_When_The_Sequence_Attribute_Conflicts()
    {
        // arrange
        var record = CreateRecord(attributes => attributes[CloudEventExtensionAttributes.Sequence] = 7, sequence: 99);
        var sequencing = new CloudEventSequencingConfiguration()
        {
            Strategy = CloudEventSequencingStrategy.Attribute,
            AttributeName = CloudEventExtensionAttributes.Sequence,
            AttributeConflictResolution = CloudEventAttributeConflictResolution.Fallback,
            FallbackAttributeName = "cloudstreamssequence"
        };

        // act
        var e = record.ToCloudEvent(sequencing);

        // assert
        Assert.NotNull(e.ExtensionAttributes);
        Assert.Equal(7ul, Convert.ToUInt64(e.ExtensionAttributes![CloudEventExtensionAttributes.Sequence].ToString()));
        Assert.Equal(99ul, Convert.ToUInt64(e.ExtensionAttributes!["cloudstreamssequence"].ToString()));
    }

    static CloudEventRecord CreateRecord(Action<IDictionary<string, object>>? configureAttributes = null, object? data = null, ulong sequence = 0)
    {
        var attributes = new Dictionary<string, object>()
        {
            { CloudEventAttributes.SpecVersion, CloudEventSpecVersion.V1.Version },
            { CloudEventAttributes.Id, Guid.NewGuid().ToString() },
            { CloudEventAttributes.Source, "https://cloud-streams.io/tests" },
            { CloudEventAttributes.Type, "com.cloud-streams.test.v1" },
            { CloudEventAttributes.Time, DateTimeOffset.UtcNow.ToString("O") }
        };
        configureAttributes?.Invoke(attributes);
        return new("test-stream", sequence, new(attributes), data ?? new { foo = "bar" });
    }

}
