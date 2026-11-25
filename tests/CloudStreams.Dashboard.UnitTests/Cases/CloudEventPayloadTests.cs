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

using CloudStreams.Core;
using CloudStreams.Dashboard.Services;
using Neuroglia.Eventing.CloudEvents;
using System.Text;
using System.Text.Json;
using Xunit;

namespace CloudStreams.Dashboard.UnitTests.Cases;

/// <summary>
/// Covers how the dashboard displays, copies and downloads the payloads of cloud events (see #228)
/// </summary>
public class CloudEventPayloadTests
{

    static CloudEvent Event(string? contentType, object? data = null, string? dataBase64 = null) => new()
    {
        SpecVersion = "1.0",
        Id = "event",
        Source = new("https://example.com"),
        Type = "com.example.test",
        DataContentType = contentType,
        Data = data,
        DataBase64 = dataBase64
    };

    [Fact]
    public void Json_Data_Should_Be_Indented_And_Downloaded_As_Json()
    {
        // act
        var payload = CloudEventPayload.From(Event("application/json", JsonDocument.Parse("""{"total":42}""").RootElement));

        // assert
        Assert.Equal(CloudEventPayloadKind.Json, payload.Kind);
        Assert.Contains(Environment.NewLine, payload.Display);
        Assert.Equal(42, JsonDocument.Parse(payload.Content).RootElement.GetProperty("total").GetInt32());
        Assert.Equal(".json", payload.FileExtension);
    }

    [Fact]
    public void Text_Data_Should_Be_Displayed_As_Is_And_Downloaded_With_Its_Media_Type()
    {
        // act
        var payload = CloudEventPayload.From(Event("application/xml; charset=utf-8", "<order total=\"42\"/>"));

        // assert
        Assert.Equal(CloudEventPayloadKind.Text, payload.Kind);
        Assert.Equal("<order total=\"42\"/>", payload.Display);
        Assert.Equal("application/xml", payload.ContentType);
        Assert.Equal(".xml", payload.FileExtension);
    }

    [Fact]
    public void Binary_Data_Should_Be_Previewed_And_Downloaded_As_Bytes()
    {
        // arrange
        var bytes = Enumerable.Range(0, 300).Select(i => (byte)i).ToArray();

        // act
        var payload = CloudEventPayload.From(Event("application/octet-stream", dataBase64: Convert.ToBase64String(bytes)));

        // assert
        Assert.Equal(CloudEventPayloadKind.Binary, payload.Kind);
        Assert.Equal(bytes, payload.Content);
        Assert.StartsWith("300 bytes", payload.Display);
        Assert.Contains("00 01 02", payload.Display);
        Assert.EndsWith("…", payload.Display);
        Assert.Equal(".bin", payload.FileExtension);
    }

    [Fact]
    public void Events_Without_Data_Should_Have_No_Payload()
    {
        Assert.Equal(CloudEventPayloadKind.None, CloudEventPayload.From(Event(null)).Kind);
        Assert.Equal(CloudEventPayloadKind.None, CloudEventPayload.From(Event("application/json", JsonDocument.Parse("null").RootElement)).Kind);
    }

    [Fact]
    public void Records_Should_Be_Converted_With_Their_Binary_Data()
    {
        // arrange
        var record = new CloudEventRecord("cloud-events", 7, new CloudEventMetadata()
        {
            ContextAttributes = new Dictionary<string, object>()
            {
                ["specversion"] = "1.0",
                ["id"] = "binary",
                ["source"] = "https://example.com",
                ["type"] = "com.example.test",
                ["datacontenttype"] = "image/png",
                ["data_base64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("png"))
            }
        }, null);

        // act
        var e = record.ToCloudEvent(Neuroglia.Serialization.Json.JsonSerializer.Default);

        // assert
        Assert.Equal("binary", e.Id);
        Assert.Equal(CloudEventPayloadKind.Binary, CloudEventPayload.From(e).Kind);
        Assert.Equal(".png", CloudEventPayload.From(e).FileExtension);
    }

}
