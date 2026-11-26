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

using CloudStreams.Core.Api.Client.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace CloudStreams.UnitTests.Cases.Core;

public class ResourceManagementApiTests
{

    static (ResourceManagementApi<Subscription> Api, StubHttpMessageHandler Http) Create(string responseContent)
    {
        var http = new StubHttpMessageHandler((_, _, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseContent, Encoding.UTF8, "application/json") }));
        var client = http.CreateClient();
        client.BaseAddress = new("http://localhost/");
        return (new ResourceManagementApi<Subscription>(NullLogger<ResourceManagementApi<Subscription>>.Instance, Neuroglia.Serialization.Json.JsonSerializer.Default, client, "subscriptions"), http);
    }

    [Fact]
    public async Task Listing_Resources_Should_Send_Escaped_Label_Selectors_Under_The_Name_The_Server_Binds()
    {
        // arrange
        var (api, http) = Create("[]");

        // act
        await (await api.ListAsync(labelSelectors: [new LabelSelector("app", LabelSelectionOperator.Equals, "a&b")])).ToListAsync();

        // assert
        var query = Assert.Single(http.Attempts).RequestUri!.Query;
        Assert.StartsWith("?labelSelector=", query);
        Assert.DoesNotContain("&b", query);
    }

    [Fact]
    public async Task Resource_Uris_Should_Never_Include_A_Namespace_And_Should_Escape_Names()
    {
        // arrange
        var (api, http) = Create(Neuroglia.Serialization.Json.JsonSerializer.Default.SerializeToText(new Subscription() { Metadata = new() { Name = "test" }, Spec = new() { Subscriber = new() { Endpoint = new Uri("http://localhost") } } }));

        // act
        await api.GetAsync("a b", "ignored-namespace");

        // assert
        var uri = Assert.Single(http.Attempts).RequestUri!;
        Assert.EndsWith("/subscriptions/a%20b", uri.AbsoluteUri);
        Assert.DoesNotContain("ignored-namespace", uri.AbsoluteUri);
    }

}
