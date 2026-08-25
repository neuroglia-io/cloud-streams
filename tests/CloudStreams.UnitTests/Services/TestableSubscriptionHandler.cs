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

using CloudStreams.Broker.Application.Services;
using FluentValidation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Neuroglia.Data.Expressions.Services;

namespace CloudStreams.UnitTests.Services;

/// <summary>
/// A <see cref="SubscriptionHandler"/> that exposes the protected members the broker test cases exercise, and that makes the
/// handler's two pieces of fire-and-forget work observable so tests are not racing them
/// </summary>
/// <remarks>
/// Every dispatch method dereferences <c>CancellationTokenSource</c>, which stays null until <c>InitializeAsync</c> runs, and
/// <c>CatchUpAsync</c> returns early unless <c>InitializeCloudEventStreamAsync</c> has set the stream initialization token
/// source. Initialization also starts a catch-up of its own whenever <c>Status.ObservedGeneration</c> is null — which it always
/// is in a fixture whose resource is never written back — so both hooks below exist to keep that from racing the test.
/// </remarks>
public class TestableSubscriptionHandler(ILoggerFactory loggerFactory, IHostApplicationLifetime hostApplicationLifetime, IJsonSerializer serializer, ICloudEventStore cloudEventStore, IResourceRepository resourceRepository,
    IResourceController<Subscription> subscriptionController, IResourceMonitor<Core.Resources.Broker> broker, IExpressionEvaluator expressionEvaluator, IEnumerable<IValidator<CloudEvent>> cloudEventValidators, HttpClient httpClient,
    IBrokerMetrics metrics, Subscription subscription)
    : SubscriptionHandler(loggerFactory, hostApplicationLifetime, serializer, cloudEventStore, resourceRepository, subscriptionController, broker, expressionEvaluator, cloudEventValidators, httpClient, metrics, subscription)
{

    readonly TaskCompletionSource _streamInitialized = new(TaskCreationOptions.RunContinuationsAsynchronously);
    int _streamInitializations;

    /// <summary>
    /// Gets/sets a boolean indicating whether to suppress the cloud event stream initialization entirely
    /// </summary>
    public bool SkipStreamInitialization { get; set; }

    /// <summary>
    /// Gets/sets a boolean indicating whether to suppress the catch-up that initialization starts on its own.
    /// <para>
    /// Defaults to true. Initialization fires <c>_ = CatchUpAsync()</c> without awaiting it, and against an empty store that
    /// loop polls every 50 milliseconds forever while holding <c>SubscriptionOutOfSync</c> true. Left running it would race
    /// every catch-up assertion, mutate the fakes concurrently, and outlive the test. Tests drive catch-up explicitly through
    /// <see cref="CatchUpPublicAsync"/>, which bypasses this switch and invokes the real implementation.
    /// </para>
    /// </summary>
    public bool SuppressAutomaticCatchUp { get; set; } = true;

    /// <summary>
    /// Gets a <see cref="Task"/> that completes once the cloud event stream initialization has run to completion
    /// </summary>
    public Task StreamInitialized => this._streamInitialized.Task;

    /// <summary>
    /// Gets the number of times the cloud event stream has been initialized.
    /// <para>
    /// Re-initialization is the only observable effect of a resume: it is what re-observes the stream and replays what the
    /// subscription missed, and a resume path that fails to reach it leaves the subscription deaf with nothing else to assert on.
    /// </para>
    /// </summary>
    public int StreamInitializationCount => Volatile.Read(ref this._streamInitializations);

    /// <summary>
    /// Gets a boolean indicating whether the subscription is latched out of sync, and is therefore dropping live events
    /// </summary>
    public bool IsOutOfSync => this.SubscriptionOutOfSync;

    /// <summary>
    /// Gets the offset of the last <see cref="CloudEventRecord"/> the handler knows to exist in the stream
    /// </summary>
    public ulong? LastKnownStreamOffset => this.StreamOffset;

    /// <inheritdoc cref="SubscriptionHandler.DispatchAsync(CloudEvent, ulong, bool, bool)"/>
    public Task DispatchPublicAsync(CloudEvent e, ulong offset, bool retryOnError = true, bool catchUpWhenAvailable = false) => this.DispatchAsync(e, offset, retryOnError, catchUpWhenAvailable);

    /// <summary>
    /// Runs the real catch-up loop, bypassing <see cref="SuppressAutomaticCatchUp"/>
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public Task CatchUpPublicAsync() => base.CatchUpAsync();

    /// <inheritdoc cref="SubscriptionHandler.SetSubscriberStateAsync"/>
    public Task SetSubscriberStatePublicAsync(SubscriberState state, string? reason = null) => this.SetSubscriberStateAsync(state, reason);

    /// <inheritdoc/>
    protected override Task CatchUpAsync() => this.SuppressAutomaticCatchUp ? Task.CompletedTask : base.CatchUpAsync();

    /// <inheritdoc/>
    protected override async Task InitializeCloudEventStreamAsync()
    {
        Interlocked.Increment(ref this._streamInitializations);
        if (this.SkipStreamInitialization)
        {
            this._streamInitialized.TrySetResult();
            return;
        }
        try { await base.InitializeCloudEventStreamAsync().ConfigureAwait(false); }
        finally { this._streamInitialized.TrySetResult(); }
    }

}
