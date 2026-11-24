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

using CloudStreams.Broker.Application.Configuration;

namespace CloudStreams.UnitTests.Cases.Broker;

public class SubscriptionLeaseTests
{

    [Fact]
    public void Leases_Should_Be_Enabled_And_Renewed_Three_Times_Per_Duration_By_Default()
    {
        // arrange
        var options = new SubscriptionLeaseOptions();

        // assert
        Assert.True(options.Enabled);
        Assert.Equal(TimeSpan.FromSeconds(15), options.Duration);
        Assert.Equal(TimeSpan.FromSeconds(5), options.GetRenewalInterval());
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(0, 5)]
    [InlineData(15, 5)]
    [InlineData(30, 5)]
    public void A_Renewal_Interval_That_Would_Let_The_Lease_Expire_Should_Be_Ignored(int seconds, int expected)
    {
        // arrange
        var options = new SubscriptionLeaseOptions() { RenewalInterval = TimeSpan.FromSeconds(seconds) };

        // assert
        Assert.Equal(TimeSpan.FromSeconds(expected), options.GetRenewalInterval());
    }

    [Fact]
    public async Task The_Unrestricted_Manager_Should_Always_Grant_Leases()
    {
        // arrange
        var leases = new UnrestrictedSubscriptionLeaseManager();

        // act & assert
        Assert.True(await leases.TryAcquireAsync("subscription"));
        Assert.True(await leases.TryAcquireAsync("subscription"));
    }

}
