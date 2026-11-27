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

using CloudStreams.Operator.Services;
using k8s.Models;

namespace CloudStreams.UnitTests.Cases.Operator;

/// <summary>
/// Covers the conditions the operator reports on the gateways and brokers it deploys (see #279)
/// </summary>
public class WorkloadStatusHelperTests
{

    static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    static ManagedResource Resource(params ResourceCondition[] conditions) => new() { Kind = WorkloadKind.Broker, Name = "orders", Namespace = "tenant-a", Uid = "uid", Generation = 4, Conditions = conditions };

    static V1Deployment Deployment(int desired, int available, int updated, long observedGeneration = 2, params V1DeploymentCondition[] conditions) => new()
    {
        Metadata = new() { Generation = 2 },
        Spec = new() { Replicas = desired },
        Status = new() { Replicas = desired, AvailableReplicas = available, UpdatedReplicas = updated, ObservedGeneration = observedGeneration, Conditions = conditions }
    };

    static string? Status(List<ResourceCondition> conditions, string type) => conditions.Single(c => c.Type == type).Status;

    [Fact]
    public void A_Rolled_Out_Workload_Should_Be_Ready()
    {
        var conditions = WorkloadStatusHelper.GetConditions(Resource(), Deployment(2, 2, 2), null, Now);
        Assert.Equal(("True", "False", "False"), (Status(conditions, "Ready"), Status(conditions, "Progressing"), Status(conditions, "Degraded")));
        Assert.All(conditions, c => Assert.Equal(4ul, c.ObservedGeneration));
    }

    [Fact]
    public void A_Rolling_Out_Workload_Should_Be_Progressing()
    {
        var conditions = WorkloadStatusHelper.GetConditions(Resource(), Deployment(3, 1, 2), null, Now);
        Assert.Equal(("False", "True", "False"), (Status(conditions, "Ready"), Status(conditions, "Progressing"), Status(conditions, "Degraded")));
        Assert.Equal("PartiallyAvailable", conditions[0].Reason);
    }

    [Fact]
    public void A_Workload_Whose_Change_Is_Not_Observed_Yet_Should_Not_Be_Ready()
    {
        var conditions = WorkloadStatusHelper.GetConditions(Resource(), Deployment(2, 2, 2, observedGeneration: 1), null, Now);
        Assert.Equal("False", Status(conditions, "Ready"));
    }

    [Fact]
    public void A_Stalled_Rollout_Should_Be_Degraded()
    {
        var stalled = new V1DeploymentCondition("False", "Progressing", reason: "ProgressDeadlineExceeded", message: "ReplicaSet has timed out progressing");
        var conditions = WorkloadStatusHelper.GetConditions(Resource(), Deployment(2, 0, 2, 2, stalled), null, Now);
        Assert.Equal(("False", "False", "True"), (Status(conditions, "Ready"), Status(conditions, "Progressing"), Status(conditions, "Degraded")));
        Assert.Equal("ReplicaSet has timed out progressing", conditions.Single(c => c.Type == "Degraded").Message);
    }

    [Fact]
    public void A_Workload_That_Could_Not_Be_Applied_Should_Be_Degraded()
    {
        var conditions = WorkloadStatusHelper.GetConditions(Resource(), null, "The secret 'x' does not exist", Now);
        Assert.Equal(("False", "True"), (Status(conditions, "Ready"), Status(conditions, "Degraded")));
        Assert.All(conditions, c => Assert.Equal("ApplyFailed", c.Reason));
    }

    [Fact]
    public void The_Transition_Time_Should_Only_Change_With_The_Status()
    {
        var before = Now.AddHours(-1);
        var resource = Resource(new ResourceCondition("Ready", "True", "Available", "2/2 replicas available", 3, before), new ResourceCondition("Degraded", "True", "ApplyFailed", null, 3, before));
        var conditions = WorkloadStatusHelper.GetConditions(resource, Deployment(2, 2, 2), null, Now);
        Assert.Equal(before, conditions.Single(c => c.Type == "Ready").LastTransitionTime);
        Assert.Equal(Now, conditions.Single(c => c.Type == "Degraded").LastTransitionTime);
    }

    [Fact]
    public void Unchanged_Conditions_Should_Not_Be_Written_Again()
    {
        var conditions = WorkloadStatusHelper.GetConditions(Resource(), Deployment(2, 2, 2), null, Now);
        var reported = Resource([.. conditions]) with { AvailableReplicas = 2 };
        Assert.False(WorkloadStatusHelper.HasChanged(reported, WorkloadStatusHelper.GetConditions(reported, Deployment(2, 2, 2), null, Now.AddMinutes(5)), 2));
        Assert.True(WorkloadStatusHelper.HasChanged(reported, WorkloadStatusHelper.GetConditions(reported, Deployment(2, 1, 2), null, Now), 1));
    }

}
