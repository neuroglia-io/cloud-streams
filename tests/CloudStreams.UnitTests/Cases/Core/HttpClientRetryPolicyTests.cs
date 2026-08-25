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
/// Covers <see cref="HttpClientRetryPolicy.Default"/>, the policy every deployment that declares none of its own lands on.
/// See <see href="https://github.com/neuroglia-io/cloud-streams/issues/121">#121</see>.
/// </summary>
public class HttpClientRetryPolicyTests
{

    [Fact]
    public void Default_Should_Be_Bounded()
    {
        //act
        var policy = HttpClientRetryPolicy.Default;

        //assert
        //an unbounded fallback is chosen by nobody, is invisible on the resource, and never exhausts - so it never records a
        //fault an operator can see. The events are safe either way: the offset is acknowledged only after a delivery succeeds
        policy.MaxAttempts.Should().Be(HttpClientRetryPolicy.DefaultMaxAttempts);
    }

    [Fact]
    public void Default_Should_Cap_Its_Backoff()
    {
        //act
        var policy = HttpClientRetryPolicy.Default;

        //assert
        policy.BackoffDuration.Should().NotBeNull();
        policy.BackoffDuration.MaxPeriod.Should().NotBeNull();
        policy.BackoffDuration.ForAttempt(1000).Should().Be(policy.BackoffDuration.MaxPeriod!.ToTimeSpan());
    }

    [Fact]
    public void Default_Should_Return_A_New_Instance_Every_Time()
    {
        //arrange
        //the properties of a RetryPolicy are settable, so a shared static instance would be mutable state observable by every
        //subscription in the process
        var policy = HttpClientRetryPolicy.Default;

        //act
        policy.MaxAttempts = 1;

        //assert
        HttpClientRetryPolicy.Default.MaxAttempts.Should().Be(HttpClientRetryPolicy.DefaultMaxAttempts);
    }

}
