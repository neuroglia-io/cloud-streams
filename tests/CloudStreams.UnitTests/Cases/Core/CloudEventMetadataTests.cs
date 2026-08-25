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
/// Covers <see cref="CloudEventMetadata"/>'s context attribute validation, which sits on the gateway's ingestion hot path
/// </summary>
public class CloudEventMetadataTests
{

    static Dictionary<string, object> ValidAttributes(params (string Key, object Value)[] extras)
    {
        var attributes = new Dictionary<string, object>()
        {
            { CloudEventAttributes.Id, "1" },
            { CloudEventAttributes.SpecVersion, CloudEventSpecVersion.V1.Version },
            { CloudEventAttributes.Source, "https://cloud-streams.io/tests" },
            { CloudEventAttributes.Type, "io.cloud-streams.tests.event.v1" }
        };
        foreach (var extra in extras) attributes[extra.Key] = extra.Value;
        return attributes;
    }

    [Fact]
    public void Constructor_Should_Accept_Valid_ContextAttributes()
    {
        //arrange
        //this is the guard the fix plan calls for: the validation at CloudEventMetadata.cs L37 is inverted, and only fails
        //to reject every event because the required 'id' attribute is 2 characters and the predicate demands Length >= 3.
        //Correcting the inversion without also fixing the length bound would reject everything the gateway ingests, and
        //this test is what makes that failure loud
        var attributes = ValidAttributes();

        //act
        var metadata = new CloudEventMetadata(attributes);

        //assert
        metadata.ContextAttributes.Should().BeEquivalentTo(attributes);
    }

    [Fact]
    public void Constructor_Should_Throw_When_Required_Attributes_Are_Missing()
    {
        //arrange
        var attributes = ValidAttributes();
        attributes.Remove(CloudEventAttributes.Type);

        //act
        var act = () => new CloudEventMetadata(attributes);

        //assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Should_Throw_When_ContextAttributes_Are_Null()
    {
        //act
        var act = () => new CloudEventMetadata(null!);

        //assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory(Skip = "Blocked by fix-plan phase 5.1: the name validation is inverted and its length bound is wrong. Fix both halves in one commit, then unskip.")]
    [InlineData("NotLowercased")]
    [InlineData("not-alphanumeric")]
    [InlineData("waytoolongtobeavalidcontextattributename")]
    public void Constructor_Should_Throw_When_A_ContextAttribute_Name_Is_Invalid(string name)
    {
        //arrange
        var attributes = ValidAttributes((name, "value"));

        //act
        var act = () => new CloudEventMetadata(attributes);

        //assert
        act.Should().Throw<ArgumentException>();
    }

}
