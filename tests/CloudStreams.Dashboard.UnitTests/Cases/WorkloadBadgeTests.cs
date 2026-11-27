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

using CloudStreams.Core.Resources;
using CloudStreams.Dashboard.Components;
using Xunit;

namespace CloudStreams.Dashboard.UnitTests.Cases;

/// <summary>
/// Covers how the dashboard orders gateways and brokers by the readiness of the workloads the operator deploys (see #279)
/// </summary>
public class WorkloadBadgeTests
{

    static ResourceCondition Condition(string type, string status) => new(type, status, "Reason", null, 1, DateTimeOffset.UtcNow);

    [Fact]
    public void Degraded_Workloads_Should_Come_First_And_External_Ones_Last()
    {
        var degraded = WorkloadBadge.Order([Condition("Ready", "False"), Condition("Progressing", "False"), Condition("Degraded", "True")]);
        var unavailable = WorkloadBadge.Order([Condition("Ready", "False"), Condition("Progressing", "False"), Condition("Degraded", "False")]);
        var rollingOut = WorkloadBadge.Order([Condition("Ready", "False"), Condition("Progressing", "True"), Condition("Degraded", "False")]);
        var ready = WorkloadBadge.Order([Condition("Ready", "True"), Condition("Progressing", "False"), Condition("Degraded", "False")]);
        var external = WorkloadBadge.Order(null);
        Assert.True(degraded < unavailable && unavailable < rollingOut && rollingOut < ready && ready < external);
    }

}
