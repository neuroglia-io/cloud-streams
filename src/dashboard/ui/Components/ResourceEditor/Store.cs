﻿using CloudStreams.Dashboard.StateManagement;
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
    where TResource : class, IResource, new()
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
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ResourceEditorState{TResource}.Errors"/> changes
    /// </summary>
    public IObservable<IDictionary<string, string[]>> Errors => this.Select(state => state.Errors).DistinctUntilChanged();

    /// <summary>
    /// Sets the state's <see cref="ResourceEditorState{TResource}.Resource"/>
    /// </summary>
    /// <param name="resource">The new <see cref="ResourceEditorState{TResource}.Resource"/> value</param>
    public void SetResource(TResource? resource)
    {
        this.Reduce(state => state with
        {
            Resource = resource
        });
        if (this.monacoEditorHelper.PreferedLanguage == PreferedLanguage.YAML)
        {
            this.SetEditorValue(Serializer.Yaml.Serialize(resource));
        }
        else
        {
            this.SetEditorValue(Serializer.Json.Serialize(resource, true));
        }
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
    /// Sets the state's <see cref="ResourceEditorState{TResource}.Errors"/>
    /// </summary>
    /// <param name="errors">The new <see cref="ResourceEditorState{TResource}.Errors"/> value</param>
    public void SetErrors(IDictionary<string, string[]> errors)
    {
        this.Reduce(state => state with
        {
            Errors = errors
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
    /// Updates the current resource using the text editor value
    /// </summary>
    /// <returns></returns>
    public async Task UpdateResourceAsync()
    {
        this.SetErrors(new Dictionary<string, string[]>());
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
                if (ex.ProblemDetails?.Errors != null && ex.ProblemDetails.Errors.Any())
                {
                    this.SetErrors(ex.ProblemDetails.Errors);
                }
            }
            catch (Exception ex)
            {

            }
        }
        this.SetSaving(false);
    }

}