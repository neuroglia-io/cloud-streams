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

using CloudStreams.Core.Api.Client.Services;
using System.Reactive.Subjects;

namespace CloudStreams.Dashboard.Services;

/// <summary>
/// Describes the subscriptions that need attention, in every namespace the user may read (see #272)
/// </summary>
/// <param name="Faulted">The qualified names of the faulted subscriptions, which stopped at an event they failed to deliver</param>
/// <param name="Suspended">The qualified names of the suspended subscriptions, which are not faulted</param>
/// <param name="Unreachable">The qualified names of the subscriptions whose subscriber is unreachable, which are still being retried, and are neither faulted nor suspended</param>
/// <param name="Namespaces">The namespaces of the faulted and suspended subscriptions</param>
public sealed record SubscriptionAlertSummary(IReadOnlyList<string> Faulted, IReadOnlyList<string> Suspended, IReadOnlyList<string> Unreachable, IReadOnlyList<string> Namespaces)
{

    /// <summary>
    /// Gets a summary of no subscription
    /// </summary>
    public static SubscriptionAlertSummary Empty { get; } = new([], [], [], []);

    /// <summary>
    /// Gets a boolean indicating whether any subscription is faulted or suspended
    /// </summary>
    public bool HasAlerts => this.Faulted.Count > 0 || this.Suspended.Count > 0;

    /// <summary>
    /// Summarizes the specified subscriptions
    /// </summary>
    /// <param name="subscriptions">The subscriptions to summarize</param>
    /// <returns>A new <see cref="SubscriptionAlertSummary"/></returns>
    public static SubscriptionAlertSummary Of(IEnumerable<Subscription> subscriptions)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);
        var faulted = new List<Subscription>();
        var suspended = new List<Subscription>();
        var unreachable = new List<Subscription>();
        foreach (var subscription in subscriptions)
        {
            if (subscription.Status?.Stream?.Fault != null) faulted.Add(subscription);
            else if (subscription.Spec?.Suspended == true || subscription.Status?.Phase == SubscriptionStatusPhase.Suspended) suspended.Add(subscription);
            else if (subscription.Status?.Subscriber?.State == SubscriberState.Unreachable) unreachable.Add(subscription);
        }
        static string[] Names(IEnumerable<Subscription> subscriptions) => [.. subscriptions.Select(s => $"{s.GetNamespace()}/{s.GetName()}").Order(StringComparer.Ordinal)];
        var namespaces = faulted.Concat(suspended).Select(s => s.GetNamespace() ?? Namespace.DefaultNamespaceName).Distinct().Order(StringComparer.Ordinal).ToArray();
        return new(Names(faulted), Names(suspended), Names(unreachable), namespaces);
    }

    /// <summary>
    /// Describes the summary in a sentence, e.g. <c>2 faulted, 1 suspended, in default and tenant-a</c>
    /// </summary>
    /// <returns>The description of the summary</returns>
    public string Describe()
    {
        var parts = new List<string>();
        if (this.Faulted.Count > 0) parts.Add($"{this.Faulted.Count} faulted");
        if (this.Suspended.Count > 0) parts.Add($"{this.Suspended.Count} suspended");
        var description = parts.Count == 0 ? "No faulted or suspended subscription" : $"{string.Join(", ", parts)} subscription{(this.Faulted.Count + this.Suspended.Count == 1 ? "" : "s")}, in {JoinNames(this.Namespaces)}";
        if (this.Unreachable.Count > 0) description += $". Subscriber unreachable, still retrying: {JoinNames(this.Unreachable)}";
        return description;
    }

    static string JoinNames(IReadOnlyList<string> names) => names.Count switch
    {
        0 => string.Empty,
        1 => names[0],
        _ => $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}"
    };

}

/// <summary>
/// Represents the service that follows the subscriptions that need attention, in every namespace the user may read, for the main menu to show them from any page
/// (see #272)
/// </summary>
/// <param name="api">The service used to interact with the Cloud Streams API</param>
/// <param name="resourceEventHub">The service used to watch resources</param>
/// <param name="logger">The service used to perform logging</param>
public class SubscriptionAlertMonitor(ICloudStreamsCoreApiClient api, ResourceWatchEventHubClient resourceEventHub, ILogger<SubscriptionAlertMonitor> logger)
    : IAsyncDisposable
{

    readonly Lock _lock = new();
    readonly Dictionary<string, Subscription> _subscriptions = [];
    readonly BehaviorSubject<SubscriptionAlertSummary> _summary = new(SubscriptionAlertSummary.Empty);
    readonly List<IDisposable> _subscriptionsToDispose = [];
    Core.Api.Client.Services.ResourceWatch<Subscription>? _watch;
    Task? _startTask;

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe the summary of the subscriptions that need attention
    /// </summary>
    public IObservable<SubscriptionAlertSummary> Summary => this._summary.DistinctUntilChanged(SummaryComparer.Instance);

    /// <summary>
    /// Starts following the subscriptions, unless already started
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public Task StartAsync()
    {
        lock (this._lock) return this._startTask ??= this.InitializeAsync();
    }

    async Task InitializeAsync()
    {
        try
        {
            await resourceEventHub.StartAsync().ConfigureAwait(false);
            // the watch spans every namespace the user may read, as the list does
            this._watch = await resourceEventHub.WatchAsync<Subscription>().ConfigureAwait(false);
            this._subscriptionsToDispose.Add(this._watch.Subscribe(this.OnWatchEvent));
            // events may have been missed while disconnected: the subscriptions are listed again
            this._subscriptionsToDispose.Add(resourceEventHub.Reconnected.Subscribe(connectionId => { _ = this.ReloadAsync(); }));
            await this.ReloadAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to follow the subscriptions that need attention");
        }
    }

    /// <summary>
    /// Lists the subscriptions again, and replaces those that are followed
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task ReloadAsync()
    {
        try
        {
            var subscriptions = await (await api.Resources.Subscriptions.ListAsync().ConfigureAwait(false)).ToListAsync().ConfigureAwait(false);
            lock (this._lock)
            {
                this._subscriptions.Clear();
                foreach (var subscription in subscriptions) this._subscriptions[Key(subscription)] = subscription;
            }
            this.Publish();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to list the subscriptions that need attention");
        }
    }

    void OnWatchEvent(IResourceWatchEvent<Subscription> e)
    {
        lock (this._lock)
        {
            switch (e.Type)
            {
                case ResourceWatchEventType.Created:
                case ResourceWatchEventType.Updated:
                    this._subscriptions[Key(e.Resource)] = e.Resource;
                    break;
                case ResourceWatchEventType.Deleted:
                    this._subscriptions.Remove(Key(e.Resource));
                    break;
                default:
                    return;
            }
        }
        this.Publish();
    }

    void Publish()
    {
        SubscriptionAlertSummary summary;
        lock (this._lock) summary = SubscriptionAlertSummary.Of(this._subscriptions.Values);
        this._summary.OnNext(summary);
    }

    static string Key(Subscription subscription) => $"{subscription.GetNamespace()}/{subscription.GetName()}";

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (var subscription in this._subscriptionsToDispose) subscription.Dispose();
        if (this._watch != null) await this._watch.DisposeAsync().ConfigureAwait(false);
        this._summary.Dispose();
        GC.SuppressFinalize(this);
    }

    sealed class SummaryComparer
        : IEqualityComparer<SubscriptionAlertSummary>
    {

        public static readonly SummaryComparer Instance = new();

        public bool Equals(SubscriptionAlertSummary? x, SubscriptionAlertSummary? y) => x == null ? y == null : y != null
            && x.Faulted.SequenceEqual(y.Faulted) && x.Suspended.SequenceEqual(y.Suspended) && x.Unreachable.SequenceEqual(y.Unreachable) && x.Namespaces.SequenceEqual(y.Namespaces);

        public int GetHashCode(SubscriptionAlertSummary obj) => HashCode.Combine(obj.Faulted.Count, obj.Suspended.Count, obj.Unreachable.Count);

    }

}
