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
/// Covers <see cref="RetryBackoffDurationExtensions.ForAttempt"/>, which computes the wait between delivery attempts
/// </summary>
public class RetryBackoffDurationExtensionsTests
{

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(50)]
    public void ForAttempt_Constant_Should_Ignore_AttemptNumber(int attemptNumber)
    {
        //arrange
        var duration = RetryBackoffDuration.Constant(Duration.FromSeconds(3));

        //act
        var result = duration.ForAttempt(attemptNumber);

        //assert
        result.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(3, 6)]
    [InlineData(10, 20)]
    public void ForAttempt_Incremental_Should_Multiply_Period_By_AttemptNumber(int attemptNumber, int expectedSeconds)
    {
        //arrange
        var duration = RetryBackoffDuration.Incremental(Duration.FromSeconds(2));

        //act
        var result = duration.ForAttempt(attemptNumber);

        //assert
        result.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Theory]
    [InlineData(2, 4)]
    [InlineData(3, 9)]
    [InlineData(4, 16)]
    public void ForAttempt_Exponential_Should_Raise_AttemptNumber_To_Exponent(int attemptNumber, int expectedSeconds)
    {
        //arrange
        //note the code raises the *attempt number* to the exponent, not the exponent to the attempt number, so growth is
        //polynomial rather than exponential. This test characterizes the shipped behaviour; changing it is a deliberate decision
        var duration = RetryBackoffDuration.Exponential(Duration.FromSeconds(1), 2);

        //act
        var result = duration.ForAttempt(attemptNumber);

        //assert
        result.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void ForAttempt_Exponential_Should_Grow_Without_Bound_When_No_MaxPeriod_Is_Set()
    {
        //arrange
        //unbounded growth remains the behaviour when no ceiling is declared; it is only defensible for a bounded retry sequence
        var duration = RetryBackoffDuration.Exponential(Duration.FromSeconds(1), 2);

        //act
        var result = duration.ForAttempt(1000);

        //assert
        result.Should().Be(TimeSpan.FromSeconds(1000000));
    }

    [Theory]
    [InlineData(RetryBackoffDurationType.Exponential)]
    [InlineData(RetryBackoffDurationType.Incremental)]
    public void ForAttempt_Should_Clamp_At_MaxPeriod(RetryBackoffDurationType type)
    {
        //arrange
        //an unbounded retry is only survivable if its backoff converges on a steady-state interval rather than growing forever
        var duration = new RetryBackoffDuration() { Type = type, Period = Duration.FromSeconds(1), Exponent = 2, MaxPeriod = Duration.FromSeconds(30) };

        //act
        var result = duration.ForAttempt(1000);

        //assert
        result.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void ForAttempt_Should_Not_Clamp_Below_MaxPeriod()
    {
        //arrange
        var duration = RetryBackoffDuration.Incremental(Duration.FromSeconds(2), Duration.FromSeconds(30));

        //act
        var result = duration.ForAttempt(3);

        //assert
        result.Should().Be(TimeSpan.FromSeconds(6));
    }

    [Fact]
    public void ForAttempt_Should_Throw_When_Duration_Is_Null()
    {
        //act
        var act = () => ((RetryBackoffDuration)null!).ForAttempt(1);

        //assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ForAttempt_Exponential_Should_Throw_When_Exponent_Is_Null()
    {
        //arrange
        //'exponent' is not enforced as required for exponential durations, so a policy can be persisted in a shape that faults
        //on use. It used to surface as a bare 'Nullable object must have a value', which named neither the property nor the type
        var duration = new RetryBackoffDuration() { Type = RetryBackoffDurationType.Exponential, Period = Duration.FromSeconds(1) };

        //act
        var act = () => duration.ForAttempt(2);

        //assert
        act.Should().Throw<ArgumentException>().WithMessage("*Exponent*");
    }

}
