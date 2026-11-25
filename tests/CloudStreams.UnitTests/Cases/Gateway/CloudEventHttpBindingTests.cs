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
using CloudStreams.Gateway.Application.Services;
using System.Text;
using System.Text.Json.Nodes;

namespace CloudStreams.UnitTests.Cases.Gateway;

/// <summary>
/// Covers the content modes of the CloudEvents HTTP binding (see #208)
/// </summary>
public class CloudEventHttpBindingTests
{

    static readonly Neuroglia.Serialization.Json.JsonSerializer Serializer = Neuroglia.Serialization.Json.JsonSerializer.Default;

    static KeyValuePair<string, string?>[] Headers(params (string Name, string Value)[] headers) => [.. headers.Select(h => new KeyValuePair<string, string?>(h.Name, h.Value))];

    static readonly (string, string)[] RequiredHeaders = [("ce-specversion", "1.0"), ("ce-id", "42"), ("ce-source", "https://orders.example.com"), ("ce-type", "order.placed")];

    [Theory]
    [InlineData("application/cloudevents+json", false, CloudEventContentMode.Structured)]
    [InlineData("application/cloudevents+json; charset=utf-8", false, CloudEventContentMode.Structured)]
    [InlineData("application/cloudevents-batch+json", false, CloudEventContentMode.Batch)]
    [InlineData("application/json", true, CloudEventContentMode.Binary)]
    [InlineData("application/octet-stream", true, CloudEventContentMode.Binary)]
    [InlineData(null, true, CloudEventContentMode.Binary)]
    [InlineData("application/json", false, CloudEventContentMode.Unsupported)]
    [InlineData(null, false, CloudEventContentMode.Unsupported)]
    [InlineData("application/cloudevents+xml", true, CloudEventContentMode.Unsupported)]
    public void The_Content_Mode_Should_Be_Determined_By_The_Content_Type_And_The_Spec_Version_Header(string? contentType, bool hasSpecVersionHeader, CloudEventContentMode expected)
    {
        Assert.Equal(expected, CloudEventHttpBinding.GetContentMode(contentType, hasSpecVersionHeader));
    }

    [Fact]
    public void A_Binary_Event_With_Json_Data_Should_Carry_Its_Attributes_And_Its_Data()
    {
        // arrange
        var headers = Headers([.. RequiredHeaders, ("ce-subject", "orders/42"), ("ce-time", "2026-11-25T10:00:00Z"), ("ce-tenant", "acme"), ("Content-Length", "12")]);

        // act
        var e = CloudEventHttpBinding.ReadBinary("application/json", headers, Encoding.UTF8.GetBytes("{\"total\":12}"), Serializer);

        // assert
        Assert.Equal("42", e.Id);
        Assert.Equal("https://orders.example.com", e.Source.OriginalString);
        Assert.Equal("order.placed", e.Type);
        Assert.Equal("orders/42", e.Subject);
        Assert.Equal(new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.Zero), e.Time);
        Assert.Equal("application/json", e.DataContentType);
        Assert.Equal("acme", e.ExtensionAttributes!["tenant"]?.ToString());
        Assert.False(e.ExtensionAttributes.ContainsKey("length"));
        Assert.Equal(12, Serializer.SerializeToNode(e.Data)!["total"]!.GetValue<int>());
    }

    [Fact]
    public void A_Binary_Event_With_Text_Data_Should_Carry_It_As_A_String()
    {
        var e = CloudEventHttpBinding.ReadBinary("text/plain; charset=utf-8", Headers(RequiredHeaders), Encoding.UTF8.GetBytes("hello, world"), Serializer);
        Assert.Equal("hello, world", e.Data?.ToString());
        Assert.Null(e.DataBase64);
    }

    [Fact]
    public void A_Binary_Event_With_Binary_Data_Should_Carry_It_As_Base64()
    {
        var e = CloudEventHttpBinding.ReadBinary("application/octet-stream", Headers(RequiredHeaders), new byte[] { 1, 2, 3 }, Serializer);
        Assert.Null(e.Data);
        Assert.Equal("AQID", e.DataBase64);
    }

    [Fact]
    public void A_Binary_Event_Without_Body_Should_Have_No_Data()
    {
        var e = CloudEventHttpBinding.ReadBinary(null, Headers(RequiredHeaders), ReadOnlyMemory<byte>.Empty, Serializer);
        Assert.Null(e.Data);
        Assert.Null(e.DataBase64);
    }

    [Fact]
    public void Header_Values_Should_Be_Percent_Decoded()
    {
        var e = CloudEventHttpBinding.ReadBinary(null, Headers([.. RequiredHeaders, ("ce-subject", "caf%C3%A9 au lait")]), ReadOnlyMemory<byte>.Empty, Serializer);
        Assert.Equal("café au lait", e.Subject);
    }

    [Fact]
    public void A_Binary_Event_Whose_Json_Data_Is_Malformed_Should_Be_Refused()
    {
        var exception = Assert.Throws<FormatException>(() => CloudEventHttpBinding.ReadBinary("application/json", Headers(RequiredHeaders), Encoding.UTF8.GetBytes("{ not json"), Serializer));
        Assert.Contains("data", exception.Message);
    }

    [Fact]
    public void A_Batch_Should_Be_Read_Element_By_Element()
    {
        // arrange
        var body = Encoding.UTF8.GetBytes("[{\"specversion\":\"1.0\",\"id\":\"1\",\"source\":\"https://a.example.com\",\"type\":\"a\"}, 42]");

        // act
        var batch = CloudEventHttpBinding.ReadBatch(body);

        // assert - an invalid element does not prevent the others from being read
        Assert.Equal(2, batch.Count);
        Assert.Equal("1", CloudEventHttpBinding.ReadEvent(batch[0], Serializer).Id);
        Assert.Throws<FormatException>(() => CloudEventHttpBinding.ReadEvent(batch[1], Serializer));
    }

    [Fact]
    public void A_Batch_That_Is_Not_An_Array_Should_Be_Refused()
    {
        Assert.Throws<FormatException>(() => CloudEventHttpBinding.ReadBatch(Encoding.UTF8.GetBytes("{\"id\":\"1\"}")));
    }

    [Fact]
    public void Binary_Data_Should_Be_Delivered_As_It_Was_Published()
    {
        // arrange - the gateway stores data_base64 with the context attributes, which is how the broker reads it back (see #208)
        var published = CloudEventHttpBinding.ReadBinary("application/octet-stream", Headers(RequiredHeaders), new byte[] { 1, 2, 3 }, Serializer);
        var attributes = published.GetContextAttributes().ToDictionary(a => a.Key, a => a.Value);
        attributes[CloudEventAttributes.DataBase64] = published.DataBase64!;

        // act
        var delivered = new CloudEventDescriptor(new CloudEventMetadata(attributes), published.Data).ToCloudEvent();

        // assert
        Assert.Equal("AQID", delivered.DataBase64);
        Assert.Null(delivered.Data);
        var json = (JsonObject)Serializer.SerializeToNode(delivered)!;
        Assert.Equal("AQID", json[CloudEventAttributes.DataBase64]!.GetValue<string>());
        Assert.False(json.ContainsKey(CloudEventAttributes.Data));
    }

}
