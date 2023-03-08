using CloudStreams.Dashboard.StateManagement;
using System.Reactive.Linq;
using CloudStreams.ResourceManagement.Api.Client.Services;

namespace CloudStreams.Dashboard.Components.ResourceDetailsStateManagement;

/// <summary>
/// Represents a <see cref="ResourceDetails{TResource}"/>'s form <see cref="ComponentStore{TState}"/>
/// </summary>
public class ResourceDetailsStore<TResource>
    : ComponentStore<ResourceDetailsState<TResource>>
    where TResource : class, IResource, new()
{
    /// <summary>
    /// The service used to interact with a Cloud Streams gateway's API
    /// </summary>
    private ICloudStreamsResourceManagementApiClient cloudStreamsResourceManagementApi;

    /// <summary>
    /// The service used to facilitate the Monaco editor interactions
    /// </summary>
    private IMonacoEditorHelper monacoEditorHelper;

    /// <summary>
    /// Initializes a new <see cref="ResourceDetailsStore{TResource}"/>
    /// </summary>
    /// <param name="cloudStreamsResourceManagementApi">The service used to interact with a Cloud Streams gateway's API</param>
    /// <param name="monacoEditorHelper">The service used to facilitate the Monaco editor interactions</param>
    public ResourceDetailsStore(ICloudStreamsResourceManagementApiClient cloudStreamsResourceManagementApi, IMonacoEditorHelper monacoEditorHelper)
        : base(new())
    {
        this.cloudStreamsResourceManagementApi = cloudStreamsResourceManagementApi;
        this.monacoEditorHelper = monacoEditorHelper;
    }

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ResourceDetailsState{TResource}.Resource"/> changes
    /// </summary>
    public IObservable<TResource?> Resource => this.Select(state => state.Resource).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ResourceDetailsState{TResource}.Definition"/> changes
    /// </summary>
    public IObservable<ResourceDefinition?> Definition => this.Select(state => state.Definition).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ResourceDetailsState{TResource}.ReadOnly"/> changes
    /// </summary>
    public IObservable<bool> ReadOnly => this.Select(state => state.ReadOnly).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ResourceDetailsState{TResource}.TextEditorValue"/> changes
    /// </summary>
    public IObservable<string> TextEditorValue => this.Select(state => state.TextEditorValue).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ResourceDetailsState{TResource}.Updating"/> changes
    /// </summary>
    public IObservable<bool> Updating => this.Select(state => state.Updating).DistinctUntilChanged();

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ResourceDetailsState{TResource}.Saving"/> changes
    /// </summary>
    public IObservable<bool> Saving => this.Select(state => state.Saving).DistinctUntilChanged();

    /// <summary>
    /// Sets the state's <see cref="ResourceDetailsState{TResource}.Resource"/>
    /// </summary>
    /// <param name="resource">The new <see cref="ResourceDetailsState{TResource}.Resource"/> value</param>
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
    /// Sets the state's <see cref="ResourceDetailsState{TResource}.Definition"/>
    /// </summary>
    /// <param name="definition">The new <see cref="ResourceDetailsState{TResource}.Definition"/> value</param>
    public void SetDefinition(ResourceDefinition? definition)
    {
        this.Reduce(state => state with
        {
            Definition = definition
        });
    }

    /// <summary>
    /// Sets the state's <see cref="ResourceDetailsState{TResource}.ReadOnly"/>
    /// </summary>
    /// <param name="readOnly">The new <see cref="ResourceDetailsState{TResource}.ReadOnly"/> value</param>
    public void SetReadOnly(bool readOnly)
    {
        this.Reduce(state => state with
        {
            ReadOnly = readOnly
        });
    }

    /// <summary>
    /// Toggles the state's <see cref="ResourceDetailsState{TResource}.ReadOnly"/>
    /// </summary>
    public void ToggleReadOnly()
    {
        bool readOnly = !this.Get(state => state.ReadOnly);
        this.Reduce(state => state with
        {
            ReadOnly = readOnly
        });
    }

    /// <summary>
    /// Sets the state's <see cref="ResourceDetailsState{TResource}.TextEditorValue"/>
    /// </summary>
    /// <param name="textEditorValue">The new <see cref="ResourceDetailsState{TResource}.TextEditorValue"/> value</param>
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
    /// Sets the state's <see cref="ResourceDetailsState{TResource}.Updating"/>
    /// </summary>
    /// <param name="updating">The new <see cref="ResourceDetailsState{TResource}.Updating"/> value</param>
    public void SetUpdating(bool updating)
    {
        this.Reduce(state => state with
        {
            Updating = updating
        });
    }

    /// <summary>
    /// Sets the state's <see cref="ResourceDetailsState{TResource}.Saving"/>
    /// </summary>
    /// <param name="saving">The new <see cref="ResourceDetailsState{TResource}.Saving"/> value</param>
    public void SetSaving(bool saving)
    {
        this.Reduce(state => state with
        {
            Saving = saving
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

}
