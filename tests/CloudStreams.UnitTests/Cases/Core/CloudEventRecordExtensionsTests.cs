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

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the conversion of stored records back into <see cref="CloudEvent"/>s, which is where non-standard attributes are
/// filtered out. This is the regression guard for
/// <see href="https://github.com/neuroglia-io/cloud-streams/issues/63">#63</see>
/// </summary>
public class CloudEventRecordExtensionsTests
{

    static CloudEventRecord RecordWith(params (string Key, object Value)[] extras)
    {
        var record = SubscriptionFactory.CloudEventRecord();
        foreach (var extra in extras) record.Metadata.ContextAttributes[extra.Key] = extra.Value;
        return record;
    }

    [Theory]
    [InlineData("$id")]
    [InlineData("NotLowercased")]
    [InlineData("not-alphanumeric")]
    public void ToCloudEvent_Should_Drop_NonStandard_ContextAttributes(string name)
    {
        //arrange
        var record = RecordWith((name, "value"));

        //act
        var e = record.ToCloudEvent(CloudEventSequencingConfiguration.Default);

        //assert
        //written without a null-conditional on purpose: ExtensionAttributes is null whenever no extension survives, and
        //'e.ExtensionAttributes?.Should()...' would silently assert nothing in exactly that case
        (e.ExtensionAttributes?.ContainsKey(name) ?? false).Should().BeFalse();
    }

    [Fact]
    public void ToCloudEvent_Should_Preserve_Required_Attributes()
    {
        //arrange
        var record = SubscriptionFactory.CloudEventRecord();

        //act
        var e = record.ToCloudEvent(CloudEventSequencingConfiguration.Default);

        //assert
        e.Id.Should().Be(record.Metadata.ContextAttributes[CloudEventAttributes.Id].ToString());
        e.Type.Should().Be(record.Metadata.ContextAttributes[CloudEventAttributes.Type].ToString());
        e.Source.Should().NotBeNull();
    }

    [Fact]
    public void ToCloudEvent_Should_Add_The_Configured_Sequencing_Attribute()
    {
        //arrange
        var record = SubscriptionFactory.CloudEventRecord(42);
        var configuration = CloudEventSequencingConfiguration.Default;

        //act
        var e = record.ToCloudEvent(configuration);

        //assert
        e.ExtensionAttributes.Should().NotBeNull();
        e.ExtensionAttributes![configuration.AttributeName!].Should().Be(record.Sequence);
    }

    [Fact]
    public void ToCloudEvent_Should_Not_Sequence_When_Strategy_Is_None()
    {
        //arrange
        var record = SubscriptionFactory.CloudEventRecord(42);
        var configuration = new CloudEventSequencingConfiguration() { Strategy = CloudEventSequencingStrategy.None };

        //act
        var e = record.ToCloudEvent(configuration);

        //assert
        (e.ExtensionAttributes?.ContainsKey(CloudEventSequencingConfiguration.Default.AttributeName!) ?? false).Should().BeFalse();
    }

    [Fact]
    public void ToCloudEvent_Should_Throw_When_Record_Is_Null()
    {
        //act
        var act = () => ((CloudEventRecord)null!).ToCloudEvent(CloudEventSequencingConfiguration.Default);

        //assert
        act.Should().Throw<ArgumentNullException>();
    }

}
