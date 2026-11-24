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

using System.Text.Json;

namespace CloudStreams.UnitTests.Cases.Core;

public class CloudEventExtensionsTests
{

    static CloudEvent WithSequence(object value) => new()
    {
        Id = "1",
        SpecVersion = CloudEventSpecVersion.V1.Version,
        Source = new("https://cloud-streams.io/tests"),
        Type = "com.cloud-streams.test.v1",
        ExtensionAttributes = new Dictionary<string, object>() { { CloudEventExtensionAttributes.Sequence, value } }
    };

    public static TheoryData<object, ulong?> Sequences => new()
    {
        { "42", 42ul },
        { "abc", null },
        { "-1", null },
        { 42ul, 42ul },
        { 42L, 42ul },
        { -1L, null },
        { 42, 42ul },
        { 42u, 42ul },
        { 42m, 42ul },
        { 4.2m, null },
        { JsonDocument.Parse("42").RootElement, 42ul },
        { JsonDocument.Parse("\"42\"").RootElement, 42ul },
        { JsonDocument.Parse("-1").RootElement, null },
        { JsonDocument.Parse("true").RootElement, null },
    };

    [Theory]
    [MemberData(nameof(Sequences))]
    public void GetSequence_Should_Parse_Supported_Representations_And_Never_Throw(object value, ulong? expected)
    {
        Assert.Equal(expected, WithSequence(value).GetSequence());
    }

}
