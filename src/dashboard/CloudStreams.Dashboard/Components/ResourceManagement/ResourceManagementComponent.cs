// Copyright © 2023-Present The Cloud Streams Authors
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
using Hylo;
using Microsoft.AspNetCore.Components;

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
    /// The list of displayed <see cref="Resource"/>s
    /// </summary>
    protected List<TResource>? resources;
    /// <summary>
    /// The <see cref="Offcanvas"/> used to show the <see cref="Resource"/>'s details
    /// </summary>
    protected Offcanvas? detailsOffcanvas;
    /// <summary>
    /// The <see cref="Offcanvas"/> used to edit the <see cref="Resource"/>
    /// </summary>
    protected Offcanvas? editorOffcanvas;
    /// <summary>
    /// The <see cref="ConfirmDialog"/> used to confirm the <see cref="Resource"/>'s deletion
    /// </summary>
    protected ConfirmDialog? dialog;
    /// <summary>
    /// The <see cref="Resource"/>'s <see cref="ResourceDefinition"/>
    /// </summary>
    protected ResourceDefinition? definition;
    /// <summary>
    /// A boolean value that indicates whether data is currently being gathered
    /// </summary>
    protected bool loading = false;

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync().ConfigureAwait(false);
        this.Store.Loading.Subscribe(loading => this.OnStateChanged(cmp => cmp.loading = loading), token: this.CancellationTokenSource.Token);
        this.Store.Definition.SubscribeAsync(async definition =>
        {
            if (this.definition != definition)
            {
                this.definition = definition;
                if (this.definition != null && this.MonacoInterop != null)
                {
                    await this.MonacoInterop.AddValidationSchemaAsync(Serializer.Json.Serialize(this.definition.Spec.Versions.First().Schema.OpenAPIV3Schema), $"https://cloud-streams.io/schemas/{typeof(TResource).Name.ToLower()}.json", $"{typeof(TResource).Name.ToLower()}").ConfigureAwait(false);
                }
            }
        }, cancellationToken: this.CancellationTokenSource.Token);
        this.Store.Resources.Subscribe(OnResourceCollectionChanged, token: this.CancellationTokenSource.Token);
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
    /// Updates the <see cref="ResourceManagementComponent{TResource}.resources"/>
    /// </summary>
    /// <param name="resources"></param>
    protected void OnResourceCollectionChanged(List<TResource>? resources)
    {
        if (resources == null) this.resources = null;
        else this.resources = resources;
        this.StateHasChanged();
    }

    /// <summary>
    /// Handles the deletion of the targeted <see cref="Resource"/>
    /// </summary>
    /// <param name="resource">The <see cref="Resource"/> to delete</param>
    protected async Task OnDeleteResourceAsync(TResource resource)
    {
        if (this.dialog == null) return;
        var confirmation = await this.dialog.ShowAsync(
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
        if (this.detailsOffcanvas == null) return Task.CompletedTask;
        var parameters = new Dictionary<string, object>
        {
            { "Resource", resource }
        };
        return this.detailsOffcanvas.ShowAsync<ResourceDetails<TResource>>(title: typeof(TResource).Name + " details", parameters: parameters);
    }

    /// <summary>
    /// Opens the targeted <see cref="Resource"/>'s edition
    /// </summary>
    /// <param name="resource">The <see cref="Resource"/> to edit</param>
    protected Task OnShowResourceEditorAsync(TResource? resource = null)
    {
        if (this.editorOffcanvas == null) return Task.CompletedTask;
        var parameters = new Dictionary<string, object>
        {
            { "Resource", resource! }
        };
        string actionType = resource == null ? "creation" : "edition";
        return this.editorOffcanvas.ShowAsync<ResourceEditor<TResource>>(title: typeof(TResource).Name + " " + actionType, parameters: parameters);
    }

}