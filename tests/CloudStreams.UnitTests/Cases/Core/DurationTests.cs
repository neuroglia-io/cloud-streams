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

using Neuroglia.Serialization.Yaml;
using JsonSerializer = Neuroglia.Serialization.Json.JsonSerializer;

namespace CloudStreams.UnitTests.Cases.Core;

public class DurationTests
{

    [Theory]
    [InlineData("PT30S", 30_000)]
    [InlineData("PT1M30S", 90_000)]
    [InlineData("P1DT2H", 93_600_000)]
    [InlineData("PT0.5S", 500)]
    public void ISO_8601_Durations_Should_Be_Parsed(string value, long expectedMilliseconds)
    {
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), Duration.Parse(value).ToTimeSpan());
    }

    [Theory]
    [InlineData("")]
    [InlineData("30")]
    [InlineData("-PT30S")]
    [InlineData("PT")]
    public void Invalid_Durations_Should_Not_Be_Parsed(string value)
    {
        Assert.False(Duration.TryParse(value, out _));
    }

    [Fact]
    public void Durations_Should_Be_Serialized_As_ISO_8601_Durations()
    {
        // arrange
        var subscriber = new Subscriber() { Endpoint = new Uri("http://localhost"), Timeout = Duration.FromSeconds(30) };

        // act
        var json = JsonSerializer.Default.SerializeToText(subscriber);
        var yaml = YamlSerializer.Default.SerializeToText(subscriber);

        // assert
        Assert.Contains("\"timeout\":\"PT30S\"", json);
        Assert.Contains("timeout: PT30S", yaml);
    }

    [Theory]
    [InlineData("{\"uri\":\"http://localhost\",\"timeout\":\"PT30S\"}")]
    [InlineData("{\"uri\":\"http://localhost\",\"timeout\":{\"seconds\":30}}")]
    public void Durations_Should_Be_Deserialized_From_Json_Strings_And_Objects(string json)
    {
        Assert.Equal(TimeSpan.FromSeconds(30), JsonSerializer.Default.Deserialize<Subscriber>(json)!.Timeout!.ToTimeSpan());
    }

    [Theory]
    [InlineData("uri: http://localhost\ntimeout: PT30S")]
    [InlineData("uri: http://localhost\ntimeout:\n  seconds: 30")]
    public void Durations_Should_Be_Deserialized_From_Yaml_Scalars_And_Mappings(string yaml)
    {
        Assert.Equal(TimeSpan.FromSeconds(30), YamlSerializer.Default.Deserialize<Subscriber>(yaml)!.Timeout!.ToTimeSpan());
    }

    [Fact]
    public void Converting_A_Huge_Duration_Should_Not_Overflow()
    {
        Assert.Equal(TimeSpan.MaxValue, Duration.FromDays(uint.MaxValue).ToTimeSpan());
    }

    [Fact]
    public void Converting_A_Negative_TimeSpan_Should_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Duration.FromTimeSpan(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Exponential_Backoff_Should_Never_Exceed_Its_Maximum_Delay()
    {
        // arrange
        var backoff = RetryBackoffDuration.Exponential(Duration.FromSeconds(1), 3);

        // act & assert
        Assert.Equal(RetryBackoffDuration.DefaultMaxDelay, backoff.ForAttempt(10_000));
        Assert.Equal(RetryBackoffDuration.DefaultMaxDelay, backoff.ForAttempt(int.MaxValue));
        backoff.MaxDelay = Duration.FromSeconds(10);
        Assert.Equal(TimeSpan.FromSeconds(8), backoff.ForAttempt(2));
        Assert.Equal(TimeSpan.FromSeconds(10), backoff.ForAttempt(3));
    }

    [Fact]
    public void Exponential_Backoff_Without_Exponent_Should_Use_The_Default_One()
    {
        // arrange
        var backoff = new RetryBackoffDuration() { Type = RetryBackoffDurationType.Exponential, Period = Duration.FromSeconds(1) };

        // act & assert
        Assert.Equal(TimeSpan.FromSeconds(Math.Pow(3, RetryBackoffDuration.DefaultExponent)), backoff.ForAttempt(3));
    }

}
