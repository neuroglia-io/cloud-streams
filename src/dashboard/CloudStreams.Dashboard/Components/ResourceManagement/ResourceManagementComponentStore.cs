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
using Neuroglia.Data;
using Neuroglia.Serialization;
using System.Net;

namespace CloudStreams.Dashboard.Components.ResourceManagement;

/// <summary>
/// Represents a <see cref="ComponentStore{TState}"/> used to manage Cloud Streams <see cref="IResource"/>s of the specified type
/// </summary>

/// <typeparam name="TState">The type of the state managed by the component store</typeparam>
/// <typeparam name="TResource">The type of <see cref="IResource"/>s to manage</typeparam>
/// <remarks>
/// Initializes a new <see cref="ResourceManagementComponentStore{TResource}"/>
/// </remarks>
/// <param name="logger">The service used to perform logging</param>
/// <param name="resourceManagementApi">The service used to interact with the Cloud Streams Resource management API</param>
/// <param name="resourceEventHub">The <see cref="IResourceEventWatchHub"/> websocket service client</param>
public class ResourceManagementComponentStore<TState, TResource>(ILogger<ResourceManagementComponentStore<TState, TResource>> logger, ICloudStreamsCoreApiClient resourceManagementApi, ResourceWatchEventHubClient resourceEventHub)
     : ComponentStore<TState>(new())
    where TResource : Resource, new()
    where TState : ResourceManagementComponentState<TResource>, new()
{
    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger<ResourceManagementComponentStore<TState, TResource>> Logger { get; } = logger;

    /// <summary>
    /// Gets the service used to interact with the CloudStreams API
    /// </summary>
    protected ICloudStreamsCoreApiClient ResourceManagementApi { get; } = resourceManagementApi;

    /// <summary>
    /// Gets the <see cref="IResourceEventWatchHub"/> websocket service client
    /// </summary>
    protected ResourceWatchEventHubClient ResourceEventHub { get; } = resourceEventHub;

    /// <summary>
    /// Gets the service used to monitor resources of the specified type
    /// </summary>
    protected Core.Api.Client.Services.ResourceWatch<TResource>? ResourceWatch { get; private set; }

    #region Selectors
    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ResourceDefinition"/>s of the specified type
    /// </summary>
    public IObservable<ResourceDefinition?> Definition => this.Select(s => s.Definition).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe unfiltered <see cref="IResource"/>s of the specified type
    /// </summary>
    protected IObservable<EquatableList<TResource>?> InternalResources => this.Select(s => s.Resources).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe the <see cref="ResourceManagementComponentState{TResource}.SelectedResourceNames"/> changes
    /// </summary>
    public IObservable<EquatableList<string>> SelectedResourceNames => this.Select(s => s.SelectedResourceNames).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe the  <see cref="ResourceManagementComponentState{TResource}.SearchTerm"/> changes
    /// </summary>
    public IObservable<string?> SearchTerm => this.Select(state => state.SearchTerm).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe the <see cref="ResourceManagementComponentState{TResource}.LabelSelectors"/> changes
    /// </summary>
    public IObservable<EquatableList<LabelSelector>?> LabelSelectors => this.Select(state => state.LabelSelectors).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe changes
    /// </summary>
    public IObservable<bool> Loading => this.Select(state => state.Loading).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="IResource"/>s of the specified type
    /// </summary>
    public IObservable<EquatableList<TResource>?> Resources => Observable.CombineLatest(
            InternalResources,
            SearchTerm.Throttle(TimeSpan.FromMilliseconds(100)).StartWith(""),
            Sort,
            (resources, searchTerm, sort) =>
            {
                if (resources == null) return [];
                IEnumerable<TResource> filtered = string.IsNullOrWhiteSpace(searchTerm) ? resources : resources.Where(r => r.GetName().Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                return new EquatableList<TResource>(this.SortResources(filtered, sort.Column, sort.Descending));
            }
         )
        .DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe the column the resources are sorted by, and in which order
    /// </summary>
    public IObservable<(string Column, bool Descending)> Sort => this.Select(state => (state.SortColumn, state.SortDescending)).DistinctUntilChanged();

    /// <summary>
    /// Gets the functions used to get the value of each sortable column of a resource, by column key. Defaults to the name, creation time and generation of resources
    /// </summary>
    /// <remarks>Pages add the columns of their own, which are compared as <see cref="IComparable"/>s, absent values coming first</remarks>
    public IDictionary<string, Func<TResource, IComparable?>> SortKeys { get; } = new Dictionary<string, Func<TResource, IComparable?>>()
    {
        [ResourceSortColumns.Name] = r => r.GetName(),
        [ResourceSortColumns.Created] = r => r.Metadata.CreationTimestamp,
        [ResourceSortColumns.Generation] = r => r.Metadata.Generation
    };

    /// <summary>
    /// Sorts the specified resources by the specified column, then by name
    /// </summary>
    /// <param name="resources">The resources to sort</param>
    /// <param name="column">The key of the column to sort the resources by</param>
    /// <param name="descending">A boolean indicating whether to sort the resources in descending order</param>
    /// <returns>The sorted resources</returns>
    protected virtual IEnumerable<TResource> SortResources(IEnumerable<TResource> resources, string column, bool descending)
    {
        var key = this.SortKeys.TryGetValue(column, out var k) ? k : this.SortKeys[ResourceSortColumns.Name];
        var comparer = Comparer<IComparable?>.Create((x, y) => (x, y) switch
        {
            (null, null) => 0,
            (null, _) => -1,
            (_, null) => 1,
            _ when x.GetType() == y.GetType() => x.CompareTo(y),
            _ => string.Compare(x.ToString(), y.ToString(), StringComparison.OrdinalIgnoreCase)
        });
        var sorted = descending ? resources.OrderByDescending(key, comparer) : resources.OrderBy(key, comparer);
        return sorted.ThenBy(r => r.GetName(), StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe the <see cref="ResourcesFilter"/>
    /// </summary>
    protected virtual IObservable<ResourcesFilter> Filter => LabelSelectors
        .Select(labelSelectors => new ResourcesFilter() { LabelSelectors = labelSelectors })
        .DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe the <see cref="ResourceManagementComponentState{TResource}.ActiveResourceName"/> changes
    /// </summary>
    public virtual IObservable<string> ActiveResourceName => this.Select(state => state.ActiveResourceName).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe the active resource changes
    /// </summary>
    public virtual IObservable<TResource?> ActiveResource => Observable.CombineLatest(
        Resources.Where(resources => resources != null),
        ActiveResourceName.Where(name => !string.IsNullOrWhiteSpace(name)),
        (resources, name) => resources!.FirstOrDefault(r => r.GetName() == name)
    )
        .DistinctUntilChanged();
    #endregion

    #region Setters
    /// <summary>
    /// Sorts the resources by the specified column: in ascending order, or, if they already are, in descending order
    /// </summary>
    /// <param name="column">The key of the column to sort the resources by</param>
    public virtual void ToggleSort(string column)
    {
        if (string.IsNullOrWhiteSpace(column)) return;
        Reduce(state => state with
        {
            SortColumn = column,
            SortDescending = state.SortColumn == column && !state.SortDescending
        });
    }

    /// <summary>
    /// Sets the <see cref="ResourceManagementComponentState{TResource}.SearchTerm" />
    /// </summary>
    /// <param name="searchTerm">The new search term</param>
    public virtual void SetSearchTerm(string? searchTerm)
    {
        Reduce(state => state with
        {
            SearchTerm = searchTerm
        });
    }

    /// <summary>
    /// Sets the <see cref="ResourceManagementComponentState{TResource}.ActiveResourceName" />
    /// </summary>
    /// <param name="activeResourceName">The new value</param>
    public virtual void SetActiveResourceName(string activeResourceName)
    {
        Reduce(state => state with
        {
            ActiveResourceName = activeResourceName ?? string.Empty
        });
    }

    /// <summary>
    /// Sets the <see cref="ResourceManagementComponentState{TResource}.LabelSelectors" />
    /// </summary>
    /// <param name="labelSelectors">The new label selectors</param>
    public virtual void SetLabelSelectors(EquatableList<LabelSelector>? labelSelectors)
    {
        Reduce(state => state with
        {
            LabelSelectors = new EquatableList<LabelSelector>(labelSelectors ?? [])
        });
    }

    /// <summary>
    /// Adds a single <see cref="LabelSelector" />
    /// </summary>
    /// <param name="labelSelector">The label selector to add</param>
    public virtual void AddLabelSelector(LabelSelector labelSelector)
    {
        if (labelSelector == null)
        {
            return;
        }
        var labelSelectors = new EquatableList<LabelSelector>(Get(state => state.LabelSelectors) ?? []);
        var existingSelector = labelSelectors.FirstOrDefault(selector => selector.Key == labelSelector.Key);
        if (existingSelector != null)
        {
            labelSelectors.Remove(existingSelector);
        }
        labelSelectors.Add(labelSelector);
        SetLabelSelectors(labelSelectors);
    }

    /// <summary>
    /// Removes a single <see cref="LabelSelector" /> by key
    /// </summary>
    /// <param name="labelSelectorKey">The label selector key to remove</param>
    public void RemoveLabelSelector(string labelSelectorKey)
    {
        if (string.IsNullOrWhiteSpace(labelSelectorKey))
        {
            return;
        }
        var labelSelectors = new EquatableList<LabelSelector>(Get(state => state.LabelSelectors) ?? []);
        var existingSelector = labelSelectors.FirstOrDefault(selector => selector.Key == labelSelectorKey);
        if (existingSelector != null)
        {
            labelSelectors.Remove(existingSelector);
        }
        SetLabelSelectors(labelSelectors);
    }

    /// <summary>
    /// Toggles the resources selection
    /// </summary>
    /// <param name="name">The name of the resource to select, or all if none is provided</param>
    public virtual void ToggleResourceSelection(string? name = null)
    {
        Reduce(state =>
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                if (state.SelectedResourceNames.Count > 0)
                {
                    return state with
                    {
                        SelectedResourceNames = []
                    };
                }
                return state with
                {
                    SelectedResourceNames = [.. state.Resources?.Select(resource => resource.GetName()) ?? []]
                };
            }
            if (state.SelectedResourceNames.Contains(name))
            {
                return state with
                {
                    SelectedResourceNames = [.. state.SelectedResourceNames.Where(n => n != name)]
                };
            }
            return state with
            {
                SelectedResourceNames = [.. state.SelectedResourceNames, name]
            };
        });
    }
    #endregion

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        await GetResourceDefinitionAsync().ConfigureAwait(false);
        // real-time updates are a nicety: failing to establish them must not prevent the resources from being listed (see #192)
        try
        {
            await ResourceEventHub.StartAsync().ConfigureAwait(false);
            ResourceWatch = await ResourceEventHub.WatchAsync<TResource>().ConfigureAwait(false);
            // the subscription ends when the store's token is canceled, on disposal
            ResourceWatch
                .Do(e => Logger.LogTrace("ResourceWatch received event '{type}' for '{name}'", e.Type.ToString(), e.Resource.GetName()))
                .SubscribeAsync(
                    onNextAsync: OnResourceWatchEventAsync,
                    onErrorAsync: ex => Task.Run(() => Logger.LogError(ex, "The resource watch failed")),
                    onCompletedAsync: () => Task.CompletedTask,
                    cancellationToken: CancellationTokenSource.Token
                );
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to watch resources, real-time updates are disabled");
        }
        // whatever changed while the connection was down is fetched again once it is re-established
        ResourceEventHub.Reconnected.SubscribeAsync(
            onNextAsync: _ => ListResourcesAsync(Get(state => new ResourcesFilter() { LabelSelectors = state.LabelSelectors })),
            onErrorAsync: _ => Task.CompletedTask,
            onCompletedAsync: () => Task.CompletedTask,
            cancellationToken: CancellationTokenSource.Token
        );
        Filter.Throttle(TimeSpan.FromMilliseconds(10)).SubscribeAsync(
            onNextAsync: ListResourcesAsync,
            onErrorAsync: ex => Task.Run(() => Logger.LogError(ex, "The resource filter failed")),
            onCompletedAsync: () => Task.CompletedTask,
            cancellationToken: CancellationTokenSource.Token
        );
        await base.InitializeAsync();
    }

    /// <summary>
    /// Deletes the specified <see cref="IResource"/>
    /// </summary>
    /// <param name="resource">The <see cref="IResource"/> to delete</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task DeleteResourceAsync(TResource resource)
    {
        await ResourceManagementApi.Manage<TResource>().DeleteAsync(resource.GetName(), resource.GetNamespace()).ConfigureAwait(false);
        // only the deleted resource is removed: resources are cluster-scoped, so filtering out its namespace would remove all of them (see #190)
        var resources = new EquatableList<TResource>(Get().Resources.Where(r => !(r.GetName() == resource.GetName() && r.GetNamespace() == resource.GetNamespace())));
        Reduce(s => s with
        {
            Resources = resources
        });
    }

    /// <summary>
    /// Deletes the selected <see cref="IResource"/>s
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public async Task DeleteSelectedResourcesAsync()
    {
        var selectedResourcesNames = Get(state => state.SelectedResourceNames);
        var resources = (Get(state => state.Resources) ?? []).Where(resource => selectedResourcesNames.Contains(resource.GetName()));
        foreach (var resource in resources)
        {
            await DeleteResourceAsync(resource);
        }
        Reduce(state => state with
        {
            SelectedResourceNames = []
        });
    }

    /// <summary>
    /// Fetches the definition of the managed <see cref="IResource"/> type
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task GetResourceDefinitionAsync()
    {
        var definition = await ResourceManagementApi.Manage<TResource>().GetDefinitionAsync().ConfigureAwait(false);
        Reduce(s => s with
        {
            Definition = definition
        });
    }

    /// <summary>
    /// Imports the specified documents: each creates the resource it describes, or updates its spec, labels and annotations if it already exists (see #227)
    /// </summary>
    /// <param name="documents">The documents to import</param>
    /// <param name="serializer">The service used to read the documents</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>An object that describes what became of each document</returns>
    /// <remarks>Documents are imported one at a time, and one failing does not prevent the others from being imported</remarks>
    public virtual async Task<ResourceImportResult> ImportAsync(IEnumerable<System.Text.Json.Nodes.JsonObject> documents, IJsonSerializer serializer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var result = new ResourceImportResult();
        var expectedKind = typeof(TResource).Name;
        var api = ResourceManagementApi.Manage<TResource>();
        var index = 0;
        foreach (var document in documents)
        {
            index++;
            var name = document["metadata"]?["name"]?.GetValue<string>();
            var label = string.IsNullOrWhiteSpace(name) ? $"document #{index}" : $"'{name}'";
            try
            {
                var kind = document["kind"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(kind) && !kind.Equals(expectedKind, StringComparison.OrdinalIgnoreCase)) throw new FormatException($"it is a {kind}, not a {expectedKind.ToLowerInvariant()}");
                if (string.IsNullOrWhiteSpace(name)) throw new FormatException("it has no 'metadata.name'");
                var resource = serializer.Deserialize<TResource>(document.ToJsonString()) ?? throw new FormatException("it is empty");
                TResource? existing;
                try { existing = await api.GetAsync(name, null, cancellationToken).ConfigureAwait(false); }
                catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.NotFound) { existing = null; }
                if (existing == null)
                {
                    resource.Metadata.ResourceVersion = null;
                    await api.CreateAsync(resource, cancellationToken).ConfigureAwait(false);
                    result.Created.Add(name);
                    continue;
                }
                var patch = JsonCons.Utilities.JsonPatch.FromDiff(
                    System.Text.Json.JsonDocument.Parse(ResourceDocuments.GetEditableDocument(existing, serializer).ToJsonString()).RootElement,
                    System.Text.Json.JsonDocument.Parse(ResourceDocuments.GetEditableDocument(resource, serializer).ToJsonString()).RootElement);
                if (patch.RootElement.GetArrayLength() < 1)
                {
                    result.Unchanged.Add(name);
                    continue;
                }
                await api.PatchAsync(new Patch(PatchType.JsonPatch, patch), name, null, cancellationToken).ConfigureAwait(false);
                result.Updated.Add(name);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var detail = ex is ProblemDetailsException problem ? problem.Problem.Detail ?? problem.Problem.Title : ex.Message;
                result.Failed.Add($"{label}: {detail}");
            }
        }
        return result;
    }

    /// <summary>
    /// Lists the resources again, filtered as they currently are
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual Task RefreshAsync() => ListResourcesAsync(Get(state => new ResourcesFilter() { LabelSelectors = state.LabelSelectors }));

    /// <summary>
    /// Lists all the resources managed by CloudStreams
    /// </summary>
    /// <param name="filter">The <see cref="ResourcesFilter" />, if any, to list the resources of</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task ListResourcesAsync(ResourcesFilter? filter = null)
    {
        try
        {
        Reduce(state => state with
        {
            Loading = true
        });
        var resources = new EquatableList<TResource>(await (await ResourceManagementApi.Manage<TResource>().ListAsync(null, filter?.LabelSelectors).ConfigureAwait(false)).ToListAsync().ConfigureAwait(false));
        Reduce(s => s with
        {
            Resources = resources,
            Loading = false
        });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to list resources");
            Reduce(state => state with
            {
                Loading = false
            });
        }
    }

    /// <summary>
    /// Handles the specified <see cref="IResourceWatchEvent"/>
    /// </summary>
    /// <param name="e">The <see cref="IResourceWatchEvent"/> to handle</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual Task OnResourceWatchEventAsync(IResourceWatchEvent<TResource> e)
    {
        var labelSelectors = Get(state => state.LabelSelectors);
        if (labelSelectors != null && labelSelectors.Count > 0 && !labelSelectors.All(selector => {
            if (e.Resource?.Metadata?.Labels == null || e.Resource.Metadata.Labels.Count == 0 || !e.Resource.Metadata.Labels.TryGetValue(selector.Key, out string? value))
            {
                return false;
            }
            var label = value;
            return selector.Operator switch
            {
                LabelSelectionOperator.Equals => !string.IsNullOrWhiteSpace(selector.Value) && selector.Value.Equals(label),
                LabelSelectionOperator.NotEquals => !string.IsNullOrWhiteSpace(selector.Value) && !selector.Value.Equals(label),
                LabelSelectionOperator.Contains => selector.Values != null && selector.Values.Contains(label),
                LabelSelectionOperator.NotContains => selector.Values != null && !selector.Values.Contains(label),
                _ => false,
            };
        }))
        {
            return Task.CompletedTask;
        }
        switch (e.Type)
        {
            case ResourceWatchEventType.Created:
                Reduce(state =>
                {
                    var resources = state.Resources == null ? [] : new EquatableList<TResource>(state.Resources);
                    if (resources.Any(r => r.GetQualifiedName() == e.Resource.GetQualifiedName()))
                    {
                        return state;
                    }
                    resources.Add(e.Resource);
                    return state with
                    {
                        Resources = resources
                    };
                });
                break;
            case ResourceWatchEventType.Updated:
                Reduce(state =>
                {
                    if (state.Resources == null) return state;
                    var resources = new EquatableList<TResource>(state.Resources);
                    var resource = resources.FirstOrDefault(r => r.GetQualifiedName() == e.Resource.GetQualifiedName());
                    if (resource == null) return state;
                    var index = resources.IndexOf(resource);
                    resources.Remove(resource);
                    resources.Insert(index, e.Resource);
                    return state with
                    {
                        Resources = resources
                    };
                });
                break;
            case ResourceWatchEventType.Deleted:
                Reduce(state =>
                {
                    if (state.Resources == null)
                    {
                        return state;
                    }
                    var resources = new EquatableList<TResource>(state.Resources);
                    var resource = resources.FirstOrDefault(r => r.GetQualifiedName() == e.Resource.GetQualifiedName());
                    if (resource == null) return state;
                    resources.Remove(resource);
                    return state with
                    {
                        Resources = resources
                    };
                });
                break;
            default:
                throw new NotSupportedException($"The specified {nameof(ResourceWatchEventType)} '{e.Type}' is not supported");
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>Blazor disposes of components synchronously, so the watch, whose release requires a call to the server, is released in the background (see #191)</remarks>
    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;
        var watch = ResourceWatch;
        ResourceWatch = null;
        if (watch != null) _ = ReleaseWatchAsync(watch);
        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (!disposing) return;
        var watch = ResourceWatch;
        ResourceWatch = null;
        if (watch != null) await ReleaseWatchAsync(watch).ConfigureAwait(false);
        await base.DisposeAsync(disposing).ConfigureAwait(false);
    }

    /// <summary>
    /// Releases the specified watch, which tells the server to stop watching once no other store watches the same resources
    /// </summary>
    /// <param name="watch">The watch to release</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    async Task ReleaseWatchAsync(Core.Api.Client.Services.ResourceWatch<TResource> watch)
    {
        try { await watch.DisposeAsync().ConfigureAwait(false); }
        catch (Exception ex) { Logger.LogWarning(ex, "Failed to stop watching resources"); }
    }

}

/// <summary>
/// Represents a <see cref="ComponentStore{TState}"/> used to manage CloudStreams <see cref="IResource"/>s of the specified type
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/>s to manage</typeparam>
/// <remarks>
/// Initializes a new <see cref="ResourceManagementComponentStore{TResource}"/>
/// </remarks>
/// <param name="logger">The service used to perform logging</param>
/// <param name="resourceManagementApi">The service used to interact with the CloudStreams API</param>
/// <param name="resourceEventHub">The <see cref="IResourceEventWatchHub"/> websocket service client</param>
public class ResourceManagementComponentStore<TResource>(ILogger<ResourceManagementComponentStore<TResource>> logger, ICloudStreamsCoreApiClient resourceManagementApi, ResourceWatchEventHubClient resourceEventHub)
     : ResourceManagementComponentStore<ResourceManagementComponentState<TResource>, TResource>(logger, resourceManagementApi, resourceEventHub)
    where TResource : Resource, new()
{



}