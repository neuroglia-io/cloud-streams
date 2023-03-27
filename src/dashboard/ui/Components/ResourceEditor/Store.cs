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

using CloudStreams.Dashboard.StateManagement;
using System.Reactive.Linq;
using CloudStreams.ResourceManagement.Api.Client.Services;
using JsonCons.Utilities;
using System.Text.Json;

namespace CloudStreams.Dashboard.Components.ResourceEditorStateManagement;

/// <summary>
/// Represents a <see cref="ResourceEditor{TResource}"/>'s form <see cref="ComponentStore{TState}"/>
/// </summary>
public class ResourceEditorStore<TResource>
    : ComponentStore<ResourceEditorState<TResource>>
    where TResource : Resource, new()
{
    /// <summary>
    /// The service used to interact with a Cloud Streams gateway's API
    /// </summary>
    private ICloudStreamsResourceManagementApiClient resourceManagementApi;

    /// <summary>
    /// The service used to facilitate the Monaco editor interactions
    /// </summary>
    private IMonacoEditorHelper monacoEditorHelper;

    /// <summary>
    /// Initializes a new <see cref="ResourceEditorStore{TResource}"/>
    /// </summary>
    /// <param name="resourceManagementApi">The service used to interact with a Cloud Streams gateway's API</param>
    /// <param name="monacoEditorHelper">The service used to facilitate the Monaco editor interactions</param>
    public ResourceEditorStore(ICloudStreamsResourceManagementApiClient resourceManagementApi, IMonacoEditorHelper monacoEditorHelper)
        : base(new())
    {
        this.resourceManagementApi = resourceManagementApi;
        this.monacoEditorHelper = monacoEditorHelper;
    }

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ResourceEditorState{TResource}.Resource"/> changes
    /// </summary>
    public IObservable<TResource?> Resource => this.Select(state => state.Resource).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ResourceEditorState{TResource}.Definition"/> changes
    /// </summary>
    public IObservable<ResourceDefinition?> Definition => this.Select(state => state.Definition).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ResourceEditorState{TResource}.TextEditorValue"/> changes
    /// </summary>
    public IObservable<string> TextEditorValue => this.Select(state => state.TextEditorValue).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ResourceEditorState{TResource}.Updating"/> changes
    /// </summary>
    public IObservable<bool> Updating => this.Select(state => state.Updating).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ResourceEditorState{TResource}.Saving"/> changes
    /// </summary>
    public IObservable<bool> Saving => this.Select(state => state.Saving).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ResourceEditorState{TResource}.ProblemType"/> changes
    /// </summary>
    public IObservable<Uri?> ProblemType => this.Select(state => state.ProblemType).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ResourceEditorState{TResource}.ProblemTitle"/> changes
    /// </summary>
    public IObservable<string> ProblemTitle => this.Select(state => state.ProblemTitle).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ResourceEditorState{TResource}.ProblemDetail"/> changes
    /// </summary>
    public IObservable<string> ProblemDetail => this.Select(state => state.ProblemDetail).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ResourceEditorState{TResource}.ProblemStatus"/> changes
    /// </summary>
    public IObservable<int> ProblemStatus => this.Select(state => state.ProblemStatus).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ResourceEditorState{TResource}.ProblemErrors"/> changes
    /// </summary>
    public IObservable<IDictionary<string, string[]>> ProblemErrors => this.Select(state => state.ProblemErrors).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe comptured <see cref="Core.Data.Models.ProblemDetails"/>
    /// </summary>
    public IObservable<ProblemDetails?> ProblemDetails => Observable.CombineLatest(
        this.ProblemType,
        this.ProblemTitle,
        this.ProblemStatus,
        this.ProblemDetail,
        this.ProblemErrors,
        (type, title, status, details, errors) =>
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }
            return new ProblemDetails(type ?? new Uri("unknown://"), title, status, details, null, errors, null);
        }
    );

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync().ConfigureAwait(false);
        this.Resource.Subscribe(resource =>
        {
            if (this.monacoEditorHelper.PreferedLanguage == PreferedLanguage.YAML)
            {
                this.SetEditorValue(Serializer.Yaml.Serialize(resource));
            }
            else
            {
                this.SetEditorValue(Serializer.Json.Serialize(resource, true));
            }
        }, token: this.CancellationTokenSource.Token);
    }

    /// <summary>
    /// Sets the state's <see cref="ResourceEditorState{TResource}.Resource"/>
    /// </summary>
    /// <param name="resource">The new <see cref="ResourceEditorState{TResource}.Resource"/> value</param>
    public void SetResource(TResource? resource)
    {
        if (resource != null)
        {
            this.Reduce(state => state with
            {
                Resource = resource
            });
            return;
        }
        this.Reduce(state => state with { 
            Resource = new() { Metadata = new() { Name = "new-" + typeof(TResource).Name.ToLower() } }
        });
    }

    /// <summary>
    /// Sets the state's <see cref="ResourceEditorState{TResource}.Definition"/>
    /// </summary>
    /// <param name="definition">The new <see cref="ResourceEditorState{TResource}.Definition"/> value</param>
    public void SetDefinition(ResourceDefinition? definition)
    {
        this.Reduce(state => state with
        {
            Definition = definition
        });
    }

    /// <summary>
    /// Sets the state's <see cref="ResourceEditorState{TResource}.TextEditorValue"/>
    /// </summary>
    /// <param name="textEditorValue">The new <see cref="ResourceEditorState{TResource}.TextEditorValue"/> value</param>
    public void SetEditorValue(string textEditorValue)
    {
        this.SetUpdating(true);
        this.Reduce(state => state with
        {
            TextEditorValue = textEditorValue
        });
        this.SetUpdating(false);
    }

    /// <summary>
    /// Sets the state's <see cref="ResourceEditorState{TResource}.Updating"/>
    /// </summary>
    /// <param name="updating">The new <see cref="ResourceEditorState{TResource}.Updating"/> value</param>
    public void SetUpdating(bool updating)
    {
        this.Reduce(state => state with
        {
            Updating = updating
        });
    }

    /// <summary>
    /// Sets the state's <see cref="ResourceEditorState{TResource}.Saving"/>
    /// </summary>
    /// <param name="saving">The new <see cref="ResourceEditorState{TResource}.Saving"/> value</param>
    public void SetSaving(bool saving)
    {
        this.Reduce(state => state with
        {
            Saving = saving
        });
    }

    /// <summary>
    /// Sets the state's <see cref="ResourceEditorState{TResource}" /> <see cref="ProblemDetails"/>'s related data
    /// </summary>
    /// <param name="problem">The <see cref="ProblemDetails"/> to populate the data with</param>
    public void SetProblemDetails(ProblemDetails? problem)
    {
        this.Reduce(state => state with
        {
            ProblemType = problem?.Type,
            ProblemTitle = problem?.Title ?? string.Empty,
            ProblemStatus = problem?.Status ?? 0,
            ProblemDetail = problem?.Detail ?? string.Empty,
            ProblemErrors = problem?.Errors ?? new Dictionary<string, string[]>()
        });
    }

    /// <summary>
    /// Changes the editor language
    /// </summary>
    /// <param name="language">The new editor's language</param>
    /// <returns></returns>
    public async Task ChangeTextEditorLanguageAsync(string language)
    {
        string textEditorValue = this.Get(state => state.TextEditorValue);
        try
        {
            string text = language == PreferedLanguage.YAML ?
                Serializer.Yaml.ConvertFromJson(textEditorValue) :
                Serializer.Yaml.ConvertToJson(textEditorValue, true);
            this.SetEditorValue(text);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            await monacoEditorHelper.ChangePreferedLanguageAsync(language == PreferedLanguage.YAML ? PreferedLanguage.JSON : PreferedLanguage.YAML);
        }
    }

    /// <summary>
    /// Creates or updates the current resource
    /// </summary>
    /// <returns></returns>
    public async Task SubmitResourceAsync()
    {
        TResource? resource = this.Get(state => state.Resource);
        if (resource?.Metadata?.Generation == null || resource?.Metadata?.Generation == 0)
        {
            await this.CreateResourceAsync();
        }
        else
        {
            await this.UpdateResourceAsync();
        }
    }

    /// <summary>
    /// Creates the current resource using the text editor value
    /// </summary>
    /// <returns></returns>
    public async Task CreateResourceAsync()
    {
        this.SetProblemDetails(null);
        this.SetSaving(true);
        string textEditorValue = this.Get(state => state.TextEditorValue);
        if (monacoEditorHelper.PreferedLanguage == PreferedLanguage.YAML)
        {
            textEditorValue = Serializer.Yaml.ConvertToJson(textEditorValue);
        }
        TResource? resource;
        try
        {
            resource = Serializer.Json.Deserialize<TResource>(textEditorValue);
            resource = await this.resourceManagementApi.Manage<TResource>().CreateAsync(resource!, this.CancellationTokenSource.Token);
            this.SetResource(resource);
        }
        catch (CloudStreamsException ex)
        {
            this.SetProblemDetails(ex.ProblemDetails);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString()); // todo: improve logging
        }
        this.SetSaving(false);
    }

    /// <summary>
    /// Updates the current resource using the text editor value
    /// </summary>
    /// <returns></returns>
    public async Task UpdateResourceAsync()
    {
        this.SetProblemDetails(null);
        this.SetSaving(true);
        TResource? resource = this.Get(state => state.Resource);
        if (resource == null)
        {
            return;
        }
        string textEditorValue = this.Get(state => state.TextEditorValue);
        if (monacoEditorHelper.PreferedLanguage == PreferedLanguage.YAML)
        {
            textEditorValue = Serializer.Yaml.ConvertToJson(textEditorValue);
        }
        JsonDocument jsonPatch = JsonPatch.FromDiff(Serializer.Json.SerializeToElement(resource)!.Value, Serializer.Json.SerializeToElement(Serializer.Json.Deserialize<TResource>(textEditorValue))!.Value);
        Json.Patch.JsonPatch? patch = Serializer.Json.Deserialize<Json.Patch.JsonPatch>(jsonPatch.RootElement);
        if (patch != null)
        {
            var resourcePatch = new ResourcePatch<TResource>(resource!, new Patch(PatchType.JsonPatch, jsonPatch));
            try
            {
                resource = await this.resourceManagementApi.Manage<TResource>().PatchAsync(resourcePatch, this.CancellationTokenSource.Token);
                this.SetResource(resource);
            }
            catch(CloudStreamsException ex)
            {
                this.SetProblemDetails(ex.ProblemDetails);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString()); // todo: improve logging
            }
        }
        this.SetSaving(false);
    }

}
