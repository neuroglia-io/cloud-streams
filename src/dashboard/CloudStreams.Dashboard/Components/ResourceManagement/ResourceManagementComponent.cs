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
using CloudStreams.Dashboard.Pages.CloudEvents.List;
using Microsoft.AspNetCore.Components;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Serialization;

namespace CloudStreams.Dashboard.Components;

/// <summary>
/// Represents the base class for all components used to manage <see cref="IResource"/>s
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to manage</typeparam>
public abstract class ResourceManagementComponent<TResource>
    : StatefulComponent<ResourceManagementComponentStore<TResource>, ResourceManagementComponentState<TResource>>
    where TResource : Resource, new()
{
    /// <summary>
    /// Gets/sets the service to build a bridge with the monaco interop extension
    /// </summary>
    [Inject]
    protected MonacoInterop? MonacoInterop { get; set; }

    /// <summary>
    /// Gets the service used to serialize/deserialize objects to/from JSON
    /// </summary>
    [Inject]
    protected IJsonSerializer Serializer { get; set; } = null!;

    /// <summary>
    /// Gets/sets the service used for JS interop
    /// </summary>
    [Inject]
    protected CommonJsInterop CommonJsInterop { get; set; } = default!;

    /// <summary>
    /// The list of displayed <see cref="Resource"/>s
    /// </summary>
    protected EquatableList<TResource>? Resources;

    /// <summary>
    /// The <see cref="Offcanvas"/> used to show the <see cref="Resource"/>'s details
    /// </summary>
    protected Offcanvas? DetailsOffCanvas;

    /// <summary>
    /// The <see cref="Offcanvas"/> used to edit the <see cref="Resource"/>
    /// </summary>
    protected Offcanvas? EditorOffCanvas;

    /// <summary>
    /// The <see cref="ConfirmDialog"/> used to confirm the <see cref="Resource"/>'s deletion
    /// </summary>
    protected ConfirmDialog? Dialog;

    /// <summary>
    /// The <see cref="Resource"/>'s <see cref="ResourceDefinition"/>
    /// </summary>
    protected ResourceDefinition? Definition;

    /// <summary>
    /// A boolean value that indicates whether data is currently being gathered
    /// </summary>
    protected bool Loading = false;

    /// <summary>
    /// Gets/sets the list of selected <see cref="Resource"/>s
    /// </summary>
    protected EquatableList<string> SelectedResourceNames { get; set; } = [];

    /// <summary>
    /// Gets/sets the checkbox used to (un)select all resources
    /// </summary>
    protected ElementReference? CheckboxAll { get; set; } = null;

    /// <summary>
    /// Gets/sets the search term to filter the resources with
    /// </summary>
    protected string? SearchTerm { get; set; }

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync().ConfigureAwait(false);

        Observable.CombineLatest(
            this.Store.Resources,
            this.Store.SelectedResourceNames,
            (resources, selectedResourceNames) => (resources, selectedResourceNames)
        ).SubscribeAsync(async (values) => {
            var (resources, selectedResourceNames) = values;
            this.OnStateChanged(_ =>
            {
                this.Resources = resources;
                this.SelectedResourceNames = selectedResourceNames;
            });
            if (this.CheckboxAll.HasValue)
            {

                if (selectedResourceNames.Count == 0)
                {
                    await this.CommonJsInterop.SetCheckboxStateAsync(this.CheckboxAll.Value, CheckboxState.Unchecked);
                }
                else if (selectedResourceNames.Count == (resources?.Count ?? 0))
                {
                    await this.CommonJsInterop.SetCheckboxStateAsync(this.CheckboxAll.Value, CheckboxState.Checked);
                }
                else
                {
                    await this.CommonJsInterop.SetCheckboxStateAsync(this.CheckboxAll.Value, CheckboxState.Indeterminate);
                }
            }
        }, cancellationToken: this.CancellationTokenSource.Token);
        this.Store.Loading.Subscribe(loading => this.OnStateChanged(_ => this.Loading = loading), token: this.CancellationTokenSource.Token);
        this.Store.Definition.SubscribeAsync(async definition =>
        {
            if (this.Definition != definition)
            {
                this.Definition = definition;
                if (this.Definition != null && this.MonacoInterop != null)
                {
                    await this.MonacoInterop.AddValidationSchemaAsync(this.Serializer.SerializeToText(this.Definition.Spec.Versions.First().Schema.OpenAPIV3Schema), $"https://cloud-streams.io/schemas/{typeof(TResource).Name.ToLower()}.json", $"{typeof(TResource).Name.ToLower()}").ConfigureAwait(false);
                }
            }
        }, cancellationToken: this.CancellationTokenSource.Token);
        this.Store.Resources.Subscribe(OnResourceCollectionChanged, token: this.CancellationTokenSource.Token);
        this.Store.SearchTerm.Subscribe(value => this.OnStateChanged(_ => this.SearchTerm = value), token: this.CancellationTokenSource.Token);
        await this.Store.GetResourceDefinitionAsync().ConfigureAwait(false);
        await this.Store.ListResourcesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Patches the <see cref="View"/>'s fields after a <see cref="CloudEventListStore"/>'s change
    /// </summary>
    /// <param name="patch">The patch to apply</param>
    private void OnStateChanged(Action<ResourceManagementComponent<TResource>> patch)
    {
        patch(this);
        this.StateHasChanged();
    }

    /// <summary>
    /// Updates the <see cref="ResourceManagementComponent{TResource}.Resources"/>
    /// </summary>
    /// <param name="resources"></param>
    protected void OnResourceCollectionChanged(EquatableList<TResource>? resources)
    {
        this.Resources = resources;
        this.StateHasChanged();
    }

    /// <summary>
    /// Handles the deletion of the targeted <see cref="Resource"/>
    /// </summary>
    /// <param name="resource">The <see cref="Resource"/> to delete</param>
    protected async Task OnDeleteResourceAsync(TResource resource)
    {
        if (this.Dialog == null) return;
        var confirmation = await this.Dialog.ShowAsync(
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
        await this.Store.DeleteResourceAsync(resource);
    }

    /// <summary>
    /// Opens the targeted <see cref="Resource"/>'s details
    /// </summary>
    /// <param name="resource">The <see cref="Resource"/> to show the details for</param>
    protected Task OnShowResourceDetailsAsync(TResource resource)
    {
        if (this.DetailsOffCanvas == null) return Task.CompletedTask;
        var parameters = new Dictionary<string, object>
        {
            { "Resource", resource }
        };
        return this.DetailsOffCanvas.ShowAsync<ResourceDetails<TResource>>(title: typeof(TResource).Name + " details", parameters: parameters);
    }

    /// <summary>
    /// Opens the targeted <see cref="Resource"/>'s edition
    /// </summary>
    /// <param name="resource">The <see cref="Resource"/> to edit</param>
    protected async Task OnShowResourceEditorAsync(TResource? resource = null)
    {
        if (this.EditorOffCanvas == null) return;
        var parameters = new Dictionary<string, object>
        {
            { "Resource", resource! }
        };
        string actionType = resource == null ? "creation" : "edition";
        await this.EditorOffCanvas.ShowAsync<ResourceEditor<TResource>>(title: typeof(TResource).Name + " " + actionType); // Force reset parameters to trigger OnParametersSetAsync
        await this.EditorOffCanvas.ShowAsync<ResourceEditor<TResource>>(title: typeof(TResource).Name + " " + actionType, parameters: parameters);
    }


    /// <summary>
    /// Handles the deletion of the selected <see cref="Resource"/>s
    /// </summary>
    protected async Task OnDeleteSelectedResourcesAsync()
    {
        if (this.Dialog == null) return;
        if (this.SelectedResourceNames.Count == 0) return;
        var confirmation = await this.Dialog.ShowAsync(
            title: $"Are you sure you want to delete {this.SelectedResourceNames.Count} resource{(this.SelectedResourceNames.Count > 1 ? "s" : "")}?",
            message1: $"The resource{(this.SelectedResourceNames.Count > 1 ? "s" : "")} will be permanently deleted. Are you sure you want to proceed ?",
            confirmDialogOptions: new ConfirmDialogOptions()
            {
                YesButtonColor = ButtonColor.Danger,
                YesButtonText = "Delete",
                NoButtonText = "Abort",
                IsVerticallyCentered = true
            }
        );
        if (!confirmation) return;
        await this.Store.DeleteSelectedResourcesAsync();
    }

    /// <summary>
    /// Exports the selected resources
    /// </summary>
    /// <returns></returns>
    protected async Task OnExportSelectedResourcesAsync()
    {
        if (this.SelectedResourceNames.Count == 0) return;
        await this.CommonJsInterop.VisitUrlsAsync(this.SelectedResourceNames.Select(name => $"/api/resources/v1/subscriptions/{name}/export").ToList());
    }

    /// <summary>
    /// Handles search input value changes
    /// </summary>
    /// <param name="e">the <see cref="ChangeEventArgs"/> to handle</param>
    protected void OnSearchInput(ChangeEventArgs e)
    {
        this.Store.SetSearchTerm(e.Value?.ToString());
    }

}