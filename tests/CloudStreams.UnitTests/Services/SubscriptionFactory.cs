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

namespace CloudStreams.UnitTests.Services;

/// <summary>
/// Creates the resources and cloud events used by the broker test cases
/// </summary>
public static class SubscriptionFactory
{

    /// <summary>
    /// The namespace test resources are created in
    /// </summary>
    public const string Namespace = "default";

    /// <summary>
    /// The name test subscriptions are created with
    /// </summary>
    public const string Name = "test-subscription";

    /// <summary>
    /// The uri test subscribers are hosted at
    /// </summary>
    public static readonly Uri SubscriberUri = new("http://localhost/events");

    /// <summary>
    /// Creates a new <see cref="Subscription"/>
    /// </summary>
    /// <param name="retryPolicy">The subscriber's <see cref="HttpClientRetryPolicy"/>, if any</param>
    /// <param name="offset">The offset to start streaming at, if any. When null, the subscription does not track an offset</param>
    /// <returns>A new <see cref="Subscription"/></returns>
    public static Subscription Subscription(HttpClientRetryPolicy? retryPolicy = null, long? offset = 0) => new(new ResourceMetadata(Name, Namespace), new SubscriptionSpec()
    {
        Stream = offset.HasValue ? new CloudEventStream() { Offset = offset } : null,
        Subscriber = new Subscriber()
        {
            Uri = SubscriberUri,
            RetryPolicy = retryPolicy
        }
    });

    /// <summary>
    /// Creates a new <see cref="Core.Resources.Broker"/>
    /// </summary>
    /// <param name="retryPolicy">The broker's default <see cref="HttpClientRetryPolicy"/>, if any</param>
    /// <returns>A new <see cref="Core.Resources.Broker"/></returns>
    public static Core.Resources.Broker Broker(HttpClientRetryPolicy? retryPolicy = null) => new(new ResourceMetadata("test-broker", Namespace), new BrokerSpec()
    {
        Dispatch = new BrokerDispatchConfiguration() { RetryPolicy = retryPolicy }
    });

    /// <summary>
    /// Creates a new <see cref="HttpClientRetryPolicy"/>
    /// </summary>
    /// <param name="maxAttempts">The maximum number of delivery attempts to perform, including the first</param>
    /// <param name="period">The constant backoff period between attempts</param>
    /// <param name="statusCodes">The status codes the policy applies to, if any</param>
    /// <param name="circuitBreaker">The policy's circuit breaker, if any</param>
    /// <param name="maxPeriod">The ceiling the computed backoff is clamped to, if any</param>
    /// <returns>A new <see cref="HttpClientRetryPolicy"/></returns>
    public static HttpClientRetryPolicy RetryPolicy(int? maxAttempts = null, TimeSpan? period = null, IEnumerable<int>? statusCodes = null, CircuitBreakerPolicy? circuitBreaker = null, TimeSpan? maxPeriod = null) => new()
    {
        MaxAttempts = maxAttempts,
        BackoffDuration = new RetryBackoffDuration()
        {
            Type = RetryBackoffDurationType.Constant,
            Period = Duration.FromTimeSpan(period ?? TimeSpan.FromSeconds(3)),
            MaxPeriod = maxPeriod.HasValue ? Duration.FromTimeSpan(maxPeriod.Value) : null
        },
        StatusCodes = statusCodes?.ToList(),
        CircuitBreaker = circuitBreaker
    };

    /// <summary>
    /// Creates a new <see cref="CloudEvent"/>
    /// </summary>
    /// <param name="id">The event's id</param>
    /// <returns>A new <see cref="CloudEvent"/></returns>
    public static CloudEvent CloudEvent(string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString("N"),
        SpecVersion = CloudEventSpecVersion.V1.Version,
        Source = new("https://cloud-streams.io/tests"),
        Type = "io.cloud-streams.tests.event.v1",
        Time = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Creates a new <see cref="CloudEventRecord"/>.
    /// <para>
    /// Deliberately uses <see cref="CloudEventMetadata"/>'s parameterless constructor: the dictionary constructor runs the
    /// inverted validation this suite also covers, so building fixtures through it would make every broker test depend on that bug.
    /// </para>
    /// </summary>
    /// <param name="sequence">The record's sequence in the stream</param>
    /// <returns>A new <see cref="CloudEventRecord"/></returns>
    public static CloudEventRecord CloudEventRecord(ulong sequence = 0) => new()
    {
        StreamId = "test-stream",
        Sequence = sequence,
        Metadata = new CloudEventMetadata()
        {
            ContextAttributes = new Dictionary<string, object>()
            {
                { CloudEventAttributes.Id, Guid.NewGuid().ToString("N") },
                { CloudEventAttributes.SpecVersion, CloudEventSpecVersion.V1.Version },
                { CloudEventAttributes.Source, "https://cloud-streams.io/tests" },
                { CloudEventAttributes.Type, "io.cloud-streams.tests.event.v1" }
            }
        },
        Data = new { foo = "bar" }
    };

}
