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
    protected Core.Api.Client.Services.ResourceWatch<TResource> ResourceWatch { get; private set; } = null!;

    /// <summary>
    /// Gets an <see cref="IDisposable"/> that represents the store's <see cref="ResourceWatch"/> subscription
    /// </summary>
    protected IDisposable ResourceWatchSubscription { get; private set; } = null!;

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
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="Namespace"/>s
    /// </summary>
    public IObservable<EquatableList<Namespace>?> Namespaces => this.Select(s => s.Namespaces).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe current namespace
    /// </summary>
    public IObservable<string?> Namespace => this.Select(s => s.Namespace).DistinctUntilChanged();

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
            (resources, searchTerm) =>
            {
                if (resources == null)
                {
                    return [];
                }
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    return resources!;
                }
                return new EquatableList<TResource>(resources!.Where(r => r.GetName().Contains(searchTerm)));
            }
         )
        .DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe the <see cref="ResourcesFilter"/> 
    /// </summary>
    protected virtual IObservable<ResourcesFilter> Filter => Observable.CombineLatest(
            Namespace,
            LabelSelectors,
            (@namespace, labelSelectors) => new ResourcesFilter()
            {
                Namespace = @namespace,
                LabelSelectors = labelSelectors
            }
        )
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
    /// Sets the <see cref="ResourceManagementComponentState{TResource}.Namespace"/>
    /// </summary>
    /// <param name="namespace">The new namespace</param>
    public void SetNamespace(string? @namespace)
    {
        Reduce(state => state with
        {
            Namespace = @namespace
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
        await ListNamespacesAsync().ConfigureAwait(false);
        await GetResourceDefinitionAsync().ConfigureAwait(false);
        await ResourceEventHub.StartAsync().ConfigureAwait(false);
        ResourceWatch = await ResourceEventHub.WatchAsync<TResource>().ConfigureAwait(false);
        ResourceWatch
            .Do(e => Logger.LogTrace("ResourceWatch received event '{type}' for '{name}'", e.Type.ToString(), e.Resource.GetName()))
            .SubscribeAsync(
                onNextAsync: OnResourceWatchEventAsync,
                onErrorAsync: ex => Task.Run(() => Logger.LogError("ResourceWatch exception: {exception}", ex.ToString())),
                onCompletedAsync: () => Task.CompletedTask,
                cancellationToken: CancellationTokenSource.Token
            );
        Filter.Throttle(TimeSpan.FromMilliseconds(10)).SubscribeAsync(
            onNextAsync: ListResourcesAsync,
            onErrorAsync: ex => Task.Run(() => Logger.LogError("Resource filter exception: {exception}", ex.ToString())),
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
        var resources = new EquatableList<TResource>(Get().Resources.Where(r => r.GetName() != resource.GetName() && r.GetNamespace() != resource.GetNamespace()));
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
    /// Lists all available <see cref="Namespace"/>s
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task ListNamespacesAsync()
    {
        await Task.CompletedTask;
        //var namespaceList = new EquatableList<Namespace>(await (await ResourceManagementApi.Namespaces.ListAsync().ConfigureAwait(false)).OrderBy(ns => ns.GetQualifiedName()).ToListAsync().ConfigureAwait(false));
        //Reduce(s => s with
        //{
        //    Namespaces = namespaceList
        //});
    }

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
        var resources = new EquatableList<TResource>(await (await ResourceManagementApi.Manage<TResource>().ListAsync(filter?.Namespace, filter?.LabelSelectors).ConfigureAwait(false)).ToListAsync().ConfigureAwait(false));
        Reduce(s => s with
        {
            Resources = resources,
            Loading = false
        });
        }
        catch (Exception ex)
        {
            Logger.LogError("Unable to list resources: {exception}", ex.ToString());
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
    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;
        ResourceWatchSubscription?.Dispose();
        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (!disposing) return;
        await ResourceWatch.DisposeAsync().ConfigureAwait(false);
        ResourceWatchSubscription.Dispose();
        base.Dispose(disposing);
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