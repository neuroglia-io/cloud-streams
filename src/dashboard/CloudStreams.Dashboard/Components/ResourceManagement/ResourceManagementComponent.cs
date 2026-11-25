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

using BlazorBootstrap;
using CloudStreams.Dashboard.Components.ResourceManagement;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Neuroglia.Serialization;

namespace CloudStreams.Dashboard.Components;

/// <summary>
/// Represents the base class for all components used to manage <see cref="IResource"/>s
/// </summary>
/// <typeparam name="TComponent">The type of component inheriting the <see cref="ResourceManagementComponent{TComponent, TStore, TState, TResource}"/></typeparam>
/// <typeparam name="TStore">The type of <see cref="ResourceManagementComponentStore{TResource}"/> to use</typeparam>
/// <typeparam name="TState">The type of the component's state</typeparam>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to manage</typeparam>
public abstract class ResourceManagementComponent<TComponent, TStore, TState, TResource>
    : StatefulComponent<TComponent, TStore, TState>
    where TComponent : ResourceManagementComponent<TComponent, TStore, TState, TResource>
    where TStore : ResourceManagementComponentStore<TState, TResource>
    where TState : ResourceManagementComponentState<TResource>, new()
    where TResource : Resource, new()
{

    /// <summary>
    /// Gets/sets the service to build a bridge with the monaco interop extension
    /// </summary>
    [Inject]
    protected MonacoInterop? MonacoInterop { get; set; }

    /// <summary>
    /// Gets/sets the service used for JS interop
    /// </summary>
    [Inject]
    protected CommonJsInterop CommonJsInterop { get; set; } = default!;

    /// <summary>
    /// Gets the service used to serialize/deserialize objects to/from JSON
    /// </summary>
    [Inject]
    protected IJsonSerializer Serializer { get; set; } = null!;

    /// <summary>
    /// Gets the service used to serialize/deserialize objects to/from YAML
    /// </summary>
    [Inject]
    protected IYamlSerializer YamlSerializer { get; set; } = null!;

    /// <summary>
    /// Gets the service used to notify users of the outcome of their actions
    /// </summary>
    [Inject]
    protected ToastService ToastService { get; set; } = null!;

    /// <summary>
    /// Gets the service used to determine what the current user may do
    /// </summary>
    [Inject]
    protected UserAccess UserAccess { get; set; } = null!;

    /// <summary>
    /// Gets a boolean indicating whether the current user may resume and replay subscriptions
    /// </summary>
    protected bool CanOperate { get; private set; }

    /// <summary>
    /// Gets a boolean indicating whether the current user may create, edit and delete resources
    /// </summary>
    protected bool CanAdminister { get; private set; }

    /// <summary>
    /// Notifies the user that an action succeeded
    /// </summary>
    /// <param name="message">The message to display</param>
    protected void NotifySuccess(string message) => this.ToastService.Notify(new(ToastType.Success, message));

    /// <summary>
    /// Notifies the user that an action failed
    /// </summary>
    /// <param name="action">A description of the action that failed</param>
    /// <param name="ex">The exception that describes the failure</param>
    protected void NotifyFailure(string action, Exception ex)
    {
        var detail = ex is ProblemDetailsException problem ? problem.Problem.Detail ?? problem.Problem.Title : ex.Message;
        this.ToastService.Notify(new(ToastType.Danger, $"{action}: {detail}"));
    }

    /// <summary>
    /// Gets/sets the list of displayed <see cref="Resource"/>s
    /// </summary>
    protected EquatableList<TResource>? Resources { get; set; }

    /// <summary>
    /// Gets/sets the list of selected <see cref="Resource"/>s
    /// </summary>
    protected EquatableList<string> SelectedResourceNames { get; set; } = [];

    /// <summary>
    /// Gets/sets the <see cref="Offcanvas"/> used to show the <see cref="Resource"/>'s details
    /// </summary>
    protected Offcanvas? DetailsOffCanvas { get; set; }

    /// <summary>
    /// Gets/sets the <see cref="Offcanvas"/> used to edit the <see cref="Resource"/>
    /// </summary>
    protected Offcanvas? EditorOffCanvas { get; set; }

    /// <summary>
    /// Gets/sets the <see cref="ConfirmDialog"/> used to confirm the <see cref="Resource"/>'s deletion
    /// </summary>
    protected ConfirmDialog? Dialog { get; set; }

    /// <summary>
    /// Gets/sets the <see cref="Resource"/>'s <see cref="ResourceDefinition"/>
    /// </summary>
    protected ResourceDefinition? Definition { get; set; }

    /// <summary>
    /// Gets/sets the search term to filter the resources with
    /// </summary>
    protected string? SearchTerm { get; set; }

    /// <summary>
    /// Gets/sets a boolean value that indicates whether data is currently being gathered
    /// </summary>
    protected bool Loading { get; set; } = false;

    /// <summary>
    /// Gets/sets the checkbox used to (un)select all resources
    /// </summary>
    protected ElementReference? CheckboxAll { get; set; } = null;

    /// <summary>
    /// Gets the key of the column the resources are sorted by
    /// </summary>
    protected string SortColumn { get; private set; } = ResourceSortColumns.Name;

    /// <summary>
    /// Gets a boolean indicating whether the resources are sorted in descending order
    /// </summary>
    protected bool SortDescending { get; private set; }

    /// <summary>
    /// Gets the label selector the resources are filtered by, if any
    /// </summary>
    protected string? LabelSelector { get; private set; }

    /// <summary>
    /// Gets the reason why the label selector could not be applied, if any
    /// </summary>
    protected string? LabelSelectorError { get; private set; }

    /// <summary>
    /// Gets the maximum size of the files that can be imported
    /// </summary>
    protected const long MaxImportFileSize = 1024 * 1024;

    string activeResourceName = null!;
    /// <summary>
    /// Gets/sets the name of the active resource
    /// </summary>
    [Parameter] public string? Name { get; set; }

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        CanOperate = await UserAccess.CanOperateAsync();
        CanAdminister = await UserAccess.CanAdministerAsync();
        ConfigureSortKeys(Store.SortKeys);
        Store.Sort.Subscribe(sort => OnStateChanged(_ =>
        {
            SortColumn = sort.Column;
            SortDescending = sort.Descending;
        }), token: CancellationTokenSource.Token);
        Observable.CombineLatest(
            Store.Resources,
            Store.SelectedResourceNames,
            (resources, selectedResourceNames) => (resources, selectedResourceNames)
        ).SubscribeAsync(async (values) => {
            var (resources, selectedResourceNames) = values;
            OnStateChanged(_ =>
            {
                Resources = resources;
                SelectedResourceNames = selectedResourceNames;
            });
            if (CheckboxAll.HasValue)
            {

                if (selectedResourceNames.Count == 0)
                {
                    await CommonJsInterop.SetCheckboxStateAsync(CheckboxAll.Value, CheckboxState.Unchecked);
                }
                else if (selectedResourceNames.Count == (resources?.Count ?? 0))
                {
                    await CommonJsInterop.SetCheckboxStateAsync(CheckboxAll.Value, CheckboxState.Checked);
                }
                else
                {
                    await CommonJsInterop.SetCheckboxStateAsync(CheckboxAll.Value, CheckboxState.Indeterminate);
                }
            }
        }, cancellationToken: CancellationTokenSource.Token);
        Store.ActiveResourceName.Subscribe(value => OnStateChanged(_ => activeResourceName = value), token: CancellationTokenSource.Token);
        Store.SearchTerm.Subscribe(value => OnStateChanged(_ => SearchTerm = value), token: CancellationTokenSource.Token);
        Store.Loading.Subscribe(value => OnStateChanged(_ => Loading = value), token: CancellationTokenSource.Token);
        Store.Definition.Subscribe(definition =>
        {
            Definition = definition;
        }, CancellationTokenSource.Token);
        Store.ActiveResource.SubscribeAsync(async resource =>
        {
            if (resource != null)
            {
                await OnShowResourceDetailsAsync(resource);
            }
        }, cancellationToken: CancellationTokenSource.Token);
        await base.OnInitializedAsync().ConfigureAwait(false);
    }


    /// <inheritdoc/>
    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrEmpty(Name) && Name != activeResourceName)
        {
            Store.SetActiveResourceName(Name);
        }
        await base.OnParametersSetAsync();
    }

    /// <summary>
    /// Adds the functions used to get the value of the page's own sortable columns
    /// </summary>
    /// <param name="sortKeys">The functions used to get the value of each sortable column of a resource, by column key</param>
    protected virtual void ConfigureSortKeys(IDictionary<string, Func<TResource, IComparable?>> sortKeys) { }

    /// <summary>
    /// Sorts the resources by the specified column, or reverses their order if they already are
    /// </summary>
    /// <param name="column">The key of the column to sort the resources by</param>
    protected void OnSort(string column) => Store.ToggleSort(column);

    /// <summary>
    /// Filters the resources by the specified comma-separated label selectors, e.g. <c>env=prod,tier!=db</c>, or stops filtering them if none
    /// </summary>
    /// <param name="labelSelector">The label selectors to filter the resources by</param>
    protected void OnLabelSelector(string? labelSelector)
    {
        labelSelector = string.IsNullOrWhiteSpace(labelSelector) ? null : labelSelector.Trim();
        EquatableList<LabelSelector> selectors;
        try { selectors = labelSelector == null ? [] : new(Neuroglia.Data.Infrastructure.ResourceOriented.LabelSelector.ParseList(labelSelector)); }
        catch (Exception ex)
        {
            OnStateChanged(_ => LabelSelectorError = $"Invalid label selector: {ex.Message}");
            return;
        }
        OnStateChanged(_ =>
        {
            LabelSelector = labelSelector;
            LabelSelectorError = null;
        });
        Store.SetLabelSelectors(selectors);
    }

    /// <summary>
    /// Downloads the specified resources as a YAML stream, which can be imported back
    /// </summary>
    /// <param name="resources">The resources to export</param>
    protected async Task OnExportAsync(IEnumerable<TResource> resources)
    {
        var list = resources.ToList();
        if (list.Count == 0) return;
        var yaml = ResourceDocuments.ExportYaml(list, Serializer, YamlSerializer);
        var fileName = list.Count == 1 ? $"{list[0].GetName()}.yaml" : $"{typeof(TResource).Name.ToLowerInvariant()}s.yaml";
        await CommonJsInterop.DownloadFileAsync(fileName, "application/yaml", System.Text.Encoding.UTF8.GetBytes(yaml));
    }

    /// <summary>
    /// Exports the selected resources
    /// </summary>
    protected Task OnExportSelectedResourcesAsync() => OnExportAsync((Resources ?? []).Where(r => SelectedResourceNames.Contains(r.GetName())));

    /// <summary>
    /// Imports the resources described by the specified file, a YAML stream or JSON, creating them or updating those that exist
    /// </summary>
    /// <param name="e">The event that describes the file to import</param>
    protected async Task OnImportAsync(Microsoft.AspNetCore.Components.Forms.InputFileChangeEventArgs e)
    {
        var kind = typeof(TResource).Name.ToLowerInvariant();
        string text;
        try
        {
            if (e.File.Size > MaxImportFileSize) throw new InvalidOperationException($"the file is larger than {MaxImportFileSize / 1024} KB");
            using var reader = new StreamReader(e.File.OpenReadStream(MaxImportFileSize));
            text = await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            NotifyFailure($"Failed to read '{e.File.Name}'", ex);
            return;
        }
        IReadOnlyList<System.Text.Json.Nodes.JsonObject> documents;
        try { documents = ResourceDocuments.ParseDocuments(text, YamlSerializer); }
        catch (FormatException ex)
        {
            NotifyFailure($"Failed to import '{e.File.Name}'", ex);
            return;
        }
        var result = await Store.ImportAsync(documents, Serializer, CancellationTokenSource.Token);
        if (result.Failed.Count == 0) NotifySuccess($"Imported '{e.File.Name}': {result}");
        else ToastService.Notify(new(ToastType.Warning, $"Imported '{e.File.Name}' ({kind}s): {result}. {string.Join(" ", result.Failed)}"));
        // the watch reports what changed, but missing an event must not leave the list stale after an import
        await Store.RefreshAsync();
    }

    /// <summary>
    /// Activates the specified action when Enter or Space is pressed, as a click would, so that rows can be used with the keyboard (see #198)
    /// </summary>
    /// <param name="e">The keyboard event to handle</param>
    /// <param name="action">The action to activate</param>
    protected static Task OnActivationKeyDown(KeyboardEventArgs e, Func<Task> action) => KeyboardActivation.OnKeyDown(e, action);

    /// <summary>
    /// Handles search input value changes
    /// </summary>
    /// <param name="e">the <see cref="ChangeEventArgs"/> to handle</param>
    protected void OnSearchInput(ChangeEventArgs e)
    {
        Store.SetSearchTerm(e.Value?.ToString());
    }

    /// <summary>
    /// Handles the deletion of the targeted <see cref="Resource"/>
    /// </summary>
    /// <param name="resource">The <see cref="Resource"/> to delete</param>
    protected async Task OnDeleteResourceAsync(TResource resource)
    {
        if (Dialog == null) return;
        var confirmation = await Dialog.ShowAsync(
            title: $"Are you sure you want to delete '{resource.Metadata.Name}'?",
            message1: $"The {typeof(TResource).Name.ToLower()} will be permanently deleted. Are you sure you want to proceed ?",
            confirmDialogOptions: new ConfirmDialogOptions()
            {
                YesButtonColor = ButtonColor.Danger,
                YesButtonText = "Delete",
                NoButtonText = "Abort",
                IsVerticallyCentered = true
            }
        );
        if (!confirmation) return;
        try
        {
            await Store.DeleteResourceAsync(resource);
            NotifySuccess($"The {typeof(TResource).Name.ToLower()} '{resource.Metadata.Name}' has been deleted");
        }
        catch (Exception ex)
        {
            NotifyFailure($"Failed to delete the {typeof(TResource).Name.ToLower()} '{resource.Metadata.Name}'", ex);
        }
    }

    /// <summary>
    /// Handles the deletion of the selected <see cref="Resource"/>s
    /// </summary>
    protected async Task OnDeleteSelectedResourcesAsync()
    {
        if (Dialog == null) return;
        if (SelectedResourceNames.Count == 0) return;
        var confirmation = await Dialog.ShowAsync(
            title: $"Are you sure you want to delete {SelectedResourceNames.Count} resource{(SelectedResourceNames.Count > 1 ? "s" : "")}?",
            message1: $"The resource{(SelectedResourceNames.Count > 1 ? "s" : "")} will be permanently deleted. Are you sure you want to proceed ?",
            confirmDialogOptions: new ConfirmDialogOptions()
            {
                YesButtonColor = ButtonColor.Danger,
                YesButtonText = "Delete",
                NoButtonText = "Abort",
                IsVerticallyCentered = true
            }
        );
        if (!confirmation) return;
        try
        {
            await Store.DeleteSelectedResourcesAsync();
            NotifySuccess("The selected resources have been deleted");
        }
        catch (Exception ex)
        {
            NotifyFailure("Failed to delete the selected resources", ex);
        }
    }

    /// <summary>
    /// Opens the targeted <see cref="Resource"/>'s details
    /// </summary>
    /// <param name="resource">The <see cref="Resource"/> to show the details for</param>
    /// <remarks>The details are read-only: administrators edit the resource from them (see #227)</remarks>
    protected virtual Task OnShowResourceDetailsAsync(TResource resource)
    {
        if (DetailsOffCanvas == null) return Task.CompletedTask;
        var parameters = new Dictionary<string, object>
        {
            { nameof(ResourceDetails<TResource>.Resource), resource },
            { nameof(ResourceDetails<TResource>.CanEdit), CanAdminister },
            { nameof(ResourceDetails<TResource>.OnEdit), EventCallback.Factory.Create<TResource?>(this, async r =>
                {
                    await DetailsOffCanvas.HideAsync();
                    await OnShowResourceEditorAsync(r);
                }) },
            { nameof(ResourceDetails<TResource>.OnExport), EventCallback.Factory.Create<TResource?>(this, r => r == null ? Task.CompletedTask : OnExportAsync([r])) }
        };
        return DetailsOffCanvas.ShowAsync<ResourceDetails<TResource>>(title: $"{typeof(TResource).Name} '{resource.GetName()}'", parameters: parameters);
    }

    /// <summary>
    /// Opens the targeted <see cref="Resource"/>'s edition
    /// </summary>
    /// <param name="resource">The <see cref="Resource"/> to edit</param>
    protected virtual Task OnShowResourceEditorAsync(TResource? resource = null)
    {
        if (EditorOffCanvas == null) return Task.CompletedTask;
        var parameters = new Dictionary<string, object>
        {
            { nameof(ResourceEditor<TResource>.Resource), resource! }
        };
        string actionType = resource == null ? "creation" : "edition";
        return EditorOffCanvas.ShowAsync<ResourceEditor<TResource>>(title: typeof(TResource).Name + " " + actionType, parameters: parameters);
    }

}


/// <summary>
/// Represents the base class for all components used to manage <see cref="IResource"/>s
/// </summary>
/// <typeparam name="TStore">The type of the component's <see cref="Store"/></typeparam>
/// <typeparam name="TState">The type of the component's state</typeparam>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to manage</typeparam>
public abstract class ResourceManagementComponent<TStore, TState, TResource>
    : ResourceManagementComponent<ResourceManagementComponent<TStore, TState, TResource>, TStore, TState, TResource>
    where TStore : ResourceManagementComponentStore<TState, TResource>
    where TState : ResourceManagementComponentState<TResource>, new()
    where TResource : Resource, new()
{



}

/// <summary>
/// Represents the base class for all components used to manage <see cref="IResource"/>s
/// </summary>
/// <typeparam name="TState">The type of the component's state</typeparam>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to manage</typeparam>
public abstract class ResourceManagementComponent<TState, TResource>
    : ResourceManagementComponent<ResourceManagementComponentStore<TState, TResource>, TState, TResource>
    where TState : ResourceManagementComponentState<TResource>, new()
    where TResource : Resource, new()
{



}

/// <summary>
/// Represents the base class for all components used to manage <see cref="IResource"/>s
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to manage</typeparam>
public abstract class ResourceManagementComponent<TResource>
    : ResourceManagementComponent<ResourceManagementComponentStore<ResourceManagementComponentState<TResource>, TResource>, ResourceManagementComponentState<TResource>, TResource>
    where TResource : Resource, new()
{



}