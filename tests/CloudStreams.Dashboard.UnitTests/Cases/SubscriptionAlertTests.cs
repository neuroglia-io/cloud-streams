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
using CloudStreams.Core.Resources;
using CloudStreams.Dashboard.Components.Help;
using CloudStreams.Dashboard.Pages.Subscriptions.List;
using CloudStreams.Dashboard.Services;
using Neuroglia;
using Xunit;

namespace CloudStreams.Dashboard.UnitTests.Cases;

/// <summary>
/// Covers how the dashboard counts the subscriptions that need attention, which the main menu shows from any page (see #272), and the topics of its help (see #271)
/// </summary>
public class SubscriptionAlertTests
{

    static Subscription Subscription(string name, string @namespace = "default", bool faulted = false, bool suspended = false, bool unreachable = false) => new()
    {
        Metadata = new() { Name = name, Namespace = @namespace },
        Spec = new() { Subscriber = new() { Endpoint = new Uri("https://subscriber.example.com") }, Suspended = suspended },
        Status = new()
        {
            Stream = faulted ? new() { Fault = new ProblemDetails(new Uri("https://cloud-streams.io/problems/test"), "Faulted", 500) } : null,
            Subscriber = unreachable ? new() { State = SubscriberState.Unreachable } : null
        }
    };

    [Fact]
    public void Faulted_And_Suspended_Subscriptions_Should_Be_Counted_In_Every_Namespace()
    {
        // arrange
        var subscriptions = new[]
        {
            Subscription("orders", faulted: true),
            Subscription("billing", "tenant-a", faulted: true, suspended: true),
            Subscription("audit", "tenant-b", suspended: true),
            Subscription("mail", unreachable: true),
            Subscription("healthy")
        };

        // act
        var summary = SubscriptionAlertSummary.Of(subscriptions);

        // assert - a subscription is counted once, as faulted if it is both
        Assert.True(summary.HasAlerts);
        Assert.Equal(["default/orders", "tenant-a/billing"], summary.Faulted);
        Assert.Equal(["tenant-b/audit"], summary.Suspended);
        Assert.Equal(["default/mail"], summary.Unreachable);
        Assert.Equal(["default", "tenant-a", "tenant-b"], summary.Namespaces);
        Assert.Equal("2 faulted, 1 suspended subscriptions, in default, tenant-a and tenant-b. Subscriber unreachable, still retrying: default/mail", summary.Describe());
    }

    [Fact]
    public void Healthy_Subscriptions_Should_Raise_No_Alert()
    {
        var summary = SubscriptionAlertSummary.Of([Subscription("healthy"), Subscription("mail", unreachable: true)]);
        Assert.False(summary.HasAlerts);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void The_List_Should_Keep_The_Subscriptions_That_Need_Attention(bool faulted, bool suspended, bool kept)
    {
        Assert.Equal(kept, SubscriptionListStore.NeedsAttention(Subscription("orders", faulted: faulted, suspended: suspended)));
    }

    [Fact]
    public void Help_Topics_Should_Have_Unique_Anchors_And_A_Summary()
    {
        Assert.Equal(HelpTopicCatalog.All.Count, HelpTopicCatalog.All.Select(t => t.Id).Distinct().Count());
        Assert.All(HelpTopicCatalog.All, t =>
        {
            Assert.Matches("^[a-z]+$", t.Id);
            Assert.False(string.IsNullOrWhiteSpace(t.Summary));
            Assert.Equal($"help#{t.Id}", t.Href);
        });
    }

}
