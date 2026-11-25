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

using Neuroglia.Data.PatchModel.Services;

namespace CloudStreams.Broker.Application.Services.Delivery;

/// <summary>
/// Represents the service used to report the status of a subscription, by patching it against the latest version of the subscription the broker knows of (see #163)
/// </summary>
/// <param name="repository">The service used to manage resources</param>
/// <param name="logger">The service used to perform logging</param>
/// <param name="subscription">The subscription to report the status of</param>
/// <remarks>
/// A patch is computed against the latest known version of the subscription, which is the result of the last patch or the last update reported by the resource
/// watch, and only applies if the subscription is still at that version. Other writers, such as the gateway's acknowledgement endpoint, the dashboard or another
/// broker replica, patch the same status: when one of them has changed it in the meantime, the subscription is read again and the change is applied to its current
/// state, so that no update overwrites another (see #161)
/// </remarks>
public class SubscriptionStatusReporter(IResourceRepository repository, ILogger logger, Subscription subscription)
{

    /// <summary>
    /// Gets the maximum amount of attempts at patching the status, when it keeps changing concurrently
    /// </summary>
    public const int MaxPatchAttempts = 5;

    volatile Subscription _latest = subscription;

    /// <summary>
    /// Gets/sets the latest version of the subscription the broker knows of, which is the result of the last patch or the last update reported by the resource watch
    /// </summary>
    public Subscription Latest
    {
        get => this._latest;
        set => this._latest = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets the state of the subscriber last reported, if any
    /// </summary>
    /// <remarks>The resource watch lags behind the patches applied by the broker, so the last reported state is tracked locally to avoid patching it after every delivery</remarks>
    public (SubscriberState State, string? Reason)? ReportedSubscriberState { get; private set; }

    /// <summary>
    /// Patches the subscription's status with the specified change
    /// </summary>
    /// <param name="update">An action that applies the change to a copy of the subscription</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A boolean indicating whether or not the status has been patched, which it is not if the change made no difference</returns>
    /// <exception cref="ProblemDetailsException">The status could not be patched, for example because it kept changing concurrently</exception>
    public virtual async Task<bool> PatchAsync(Action<Subscription> update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        var original = this.Latest;
        for (var attempt = 1; ; attempt++)
        {
            var resource = original.Clone()!;
            update(resource);
            var patch = JsonPatchUtility.CreateJsonPatchFromDiff(original, resource);
            if (!patch.Operations.Any()) return false;
            try
            {
                var patched = await repository.PatchStatusAsync<Subscription>(new Patch(PatchType.JsonPatch, patch), original.GetName(), original.GetNamespace(), original.Metadata.ResourceVersion, false, cancellationToken).ConfigureAwait(false);
                if (patched != null) this.Latest = patched;
                return true;
            }
            catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.NotModified)
            {
                return false;
            }
            catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.Conflict && attempt < MaxPatchAttempts)
            {
                logger.LogDebug("The status of subscription '{subscription}' has changed concurrently (attempt {attempt}/{maxAttempts}): retrying against its current state", original.GetQualifiedName(), attempt, MaxPatchAttempts);
                original = await repository.GetAsync<Subscription>(original.GetName(), original.GetNamespace(), cancellationToken).ConfigureAwait(false)
                    ?? throw new ProblemDetailsException(ResourceProblemDetails.ResourceNotFound(new ResourceReference<Subscription>(original.GetName(), original.GetNamespace())));
                this.Latest = original;
            }
        }
    }

    /// <summary>
    /// Sets the subscription's status phase
    /// </summary>
    /// <param name="phase">The phase to set</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual Task SetPhaseAsync(SubscriptionStatusPhase phase, CancellationToken cancellationToken = default) => this.PatchAsync(resource =>
    {
        resource.Status ??= new();
        resource.Status.Phase = phase;
    }, cancellationToken);

    /// <summary>
    /// Sets the state of the subscriber, unless it already is the one the subscription reports, or the one last reported
    /// </summary>
    /// <param name="current">The subscription, as last reported by the resource watch</param>
    /// <param name="state">The state to set</param>
    /// <param name="reason">The reason why, if any, the subscriber is in the specified state</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task SetSubscriberStateAsync(Subscription current, SubscriberState state, string? reason = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.Status?.Subscriber?.State == state && current.Status?.Subscriber?.Reason == reason) return;
        if (this.ReportedSubscriberState == (state, reason)) return;
        this.ReportedSubscriberState = (state, reason);
        await this.PatchAsync(resource =>
        {
            resource.Status ??= new();
            resource.Status.Subscriber ??= new();
            resource.Status.Subscriber.State = state;
            resource.Status.Subscriber.Reason = reason;
        }, cancellationToken).ConfigureAwait(false);
    }

}
