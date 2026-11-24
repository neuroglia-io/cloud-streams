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
/// Covers the patterns publishers are bound to (issue #147)
/// </summary>
public class CloudEventSourcePatternTests
{

    [Theory]
    [InlineData("https://orders.example.com", "https://orders.example.com", true)]
    [InlineData("https://orders.example.com/eu", "https://orders.example.com", false)]
    [InlineData("https://orders.example.com/eu", "https://orders.example.com/*", true)]
    [InlineData("https://orders.example.com/", "https://orders.example.com/*", true)]
    [InlineData("https://orders.example.com.evil.net/eu", "https://orders.example.com/*", false)]
    [InlineData("https://orders.example.com.evil.net", "https://orders.example.com*", true)]
    [InlineData("https://eu.orders.example.com/v1", "https://*.orders.example.com/*", true)]
    [InlineData("https://orders.example.com/v1", "https://*.orders.example.com/*", false)]
    [InlineData("urn:orders:eu:1", "urn:orders:*:1", true)]
    [InlineData("urn:orders:eu:12", "urn:orders:*:1", false)]
    [InlineData("anything", "*", true)]
    [InlineData("https://ORDERS.example.com", "https://orders.example.com", false)]
    [InlineData("https://orders.example.com", "", false)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaab", "*a*a*a*a*a*a*a*a*a*a*a*a*c", false)]
    public void Patterns_Should_Match_Whole_Sources_With_Wildcards(string source, string pattern, bool expected)
    {
        Assert.Equal(expected, CloudEventSourcePattern.IsMatch(source, pattern));
    }

    [Fact]
    public void A_Source_Should_Match_Any_Of_The_Patterns()
    {
        var source = new Uri("https://billing.example.com/invoices");
        Assert.True(CloudEventSourcePattern.IsMatchAny(source, ["https://orders.example.com/*", "https://billing.example.com/*"]));
        Assert.False(CloudEventSourcePattern.IsMatchAny(source, ["https://orders.example.com/*"]));
        Assert.False(CloudEventSourcePattern.IsMatchAny(source, []));
    }

}
