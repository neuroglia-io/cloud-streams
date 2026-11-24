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

using CloudStreams.Core.Application.Validation;

namespace CloudStreams.UnitTests.Cases.Core;

public class CloudEventValidatorTests
{

    readonly CloudEventValidator _validator = new();

    static CloudEvent Valid() => new()
    {
        Id = "1",
        SpecVersion = CloudEventSpecVersion.V1.Version,
        Source = new("https://cloud-streams.io/tests"),
        Type = "com.cloud-streams.test.v1"
    };

    [Fact]
    public void A_Valid_Cloud_Event_Should_Pass()
    {
        Assert.True(this._validator.Validate(Valid()).IsValid);
    }

    [Fact]
    public void A_Cloud_Event_With_Valid_Optional_Attributes_Should_Pass()
    {
        // arrange
        var e = Valid();
        e.Subject = "subject";
        e.DataContentType = "application/json";
        e.DataSchema = new("https://cloud-streams.io/schemas/test.json");
        e.ExtensionAttributes = new Dictionary<string, object>() { { "origin", "gateway" }, { "sequence", 1 } };

        // assert
        Assert.True(this._validator.Validate(e).IsValid);
    }

    public static TheoryData<string, Action<CloudEvent>> InvalidEvents => new()
    {
        { "specversion", e => e.SpecVersion = "0.3" },
        { "id", e => e.Id = "" },
        { "type", e => e.Type = "" },
        { "subject", e => e.Subject = "" },
        { "datacontenttype", e => e.DataContentType = "not a media type" },
        { "dataschema", e => e.DataSchema = new("relative/schema.json", UriKind.Relative) },
        { "extension", e => e.ExtensionAttributes = new Dictionary<string, object>() { { "Not-Valid", "value" } } },
    };

    [Theory]
    [MemberData(nameof(InvalidEvents))]
    public void An_Invalid_Cloud_Event_Should_Fail(string attribute, Action<CloudEvent> invalidate)
    {
        // arrange
        var e = Valid();
        invalidate(e);

        // act
        var result = this._validator.Validate(e);

        // assert
        Assert.False(result.IsValid, $"An invalid '{attribute}' should fail validation");
    }

}
