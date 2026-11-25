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
using JsonCons.Utilities;
using Neuroglia.Data;
using Neuroglia.Serialization;
using Neuroglia.Serialization.Yaml;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CloudStreams.Dashboard.Components.ResourceEditorStateManagement;

/// <summary>
/// Represents a <see cref="ResourceEditor{TResource}"/>'s form <see cref="ComponentStore{TState}"/>
/// </summary>
/// <remarks>
/// Initializes a new <see cref="ResourceEditorStore{TResource}"/>
/// </remarks>
/// <param name="resourceManagementApi">The service used to interact with a Cloud Streams gateway's API</param>
/// <param name="monacoEditorHelper">The service used to facilitate the Monaco editor interactions</param>
/// <param name="jsonSerializer">The The service used to serialize/deserialize objects to/from JSON</param>
/// <param name="yamlSerializer">The service used to serialize/deserialize objects to/from YAML</param>
public class ResourceEditorStore<TResource>(ICloudStreamsCoreApiClient resourceManagementApi, IMonacoEditorHelper monacoEditorHelper, IJsonSerializer jsonSerializer, IYamlSerializer yamlSerializer)
    : ComponentStore<ResourceEditorState<TResource>>(new())
    where TResource : Resource, new()
{

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
    /// Gets an <see cref="IObservable{T}"/> used to observe <see cref="ResourceEditorState{TResource}.SuccessMessage"/> changes
    /// </summary>
    public IObservable<string?> SuccessMessage => this.Select(state => state.SuccessMessage).DistinctUntilChanged();

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
    /// Gets an <see cref="IObservable{T}"/> used to observe computed <see cref="Neuroglia.ProblemDetails"/>
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
            if (monacoEditorHelper.PreferredLanguage == PreferredLanguage.YAML)
            {
                this.SetEditorValue(YamlSerializer.Default.Serialize(resource));
            }
            else
            {
                // the editor shows JSON when JSON is preferred, since that is how its value is parsed on save (see #193)
                var json = resource == null ? string.Empty : JsonNode.Parse(jsonSerializer.SerializeToText(resource))!.ToJsonString(IndentedJsonOptions);
                this.SetEditorValue(json);
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
            ProblemErrors = problem?.Errors?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? [],
            SuccessMessage = null,

        });
    }

    /// <summary>
    /// Changes the editor language
    /// </summary>
    /// <param name="language">The new editor's language</param>
    /// <returns></returns>
    public async Task ChangeTextEditorLanguageAsync(string language)
    {
        var textEditorValue = this.Get(state => state.TextEditorValue);
        try
        {
            var text = language == PreferredLanguage.YAML ?
                yamlSerializer.ConvertFromJson(textEditorValue) :
                yamlSerializer.ConvertToJson(textEditorValue);
            this.SetEditorValue(text);
        }
        catch (Exception ex)
        {
            await monacoEditorHelper.ChangePreferredLanguageAsync(language == PreferredLanguage.YAML ? PreferredLanguage.JSON : PreferredLanguage.YAML);
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
        var textEditorValue = this.Get(state => state.TextEditorValue);
        try
        {
            if (monacoEditorHelper.PreferredLanguage == PreferredLanguage.YAML) textEditorValue = yamlSerializer.ConvertToJson(textEditorValue);
            TResource? resource;
            try
            {
                resource = jsonSerializer.Deserialize<TResource>(textEditorValue);
                if (!this.ValidateMappedProperties(textEditorValue, resource)) return;
                resource = await resourceManagementApi.Manage<TResource>().CreateAsync(resource!, this.CancellationTokenSource.Token);
                this.SetResource(resource);
                this.SetSuccessMessage($"The {typeof(TResource).Name.ToLower()} '{resource.GetName()}' has been created");
            }
            catch (ProblemDetailsException ex)
            {
                this.SetProblemDetails(ex.Problem);
            }
        }
        catch (Exception ex)
        {
            this.SetProblemDetails(new ProblemDetails(
                ProblemTypes.SchemaValidationFailed,
                "Creation failed",
                400,
                ex.Message
            ));
        }
        finally
        {
            this.SetSaving(false);
        }
    }

    /// <summary>
    /// Updates the current resource using the text editor value
    /// </summary>
    /// <returns></returns>
    /// <remarks>
    /// Only the resource's spec, labels and annotations can be patched: the rest of its metadata is managed by the server and its status by the controllers. The
    /// patch is therefore computed from those alone, otherwise any status change made since the editor was opened (such as an acknowledged offset) would end up in it
    /// and be rejected (see #109), while label and annotation changes would be silently dropped (see #193)
    /// </remarks>
    public async Task UpdateResourceAsync()
    {
        this.CancelReview();
        this.SetProblemDetails(null);
        var resource = this.Get(state => state.Resource);
        if (resource == null) return;
        this.SetSaving(true);
        try
        {
            var textEditorValue = this.Get(state => state.TextEditorValue);
            if (monacoEditorHelper.PreferredLanguage == PreferredLanguage.YAML) textEditorValue = yamlSerializer.ConvertToJson(textEditorValue);
            var updatedResource = jsonSerializer.Deserialize<TResource>(textEditorValue);
            if (!this.ValidateMappedProperties(textEditorValue, updatedResource)) return;
            var jsonPatch = JsonPatch.FromDiff(this.GetEditableDocument(resource), this.GetEditableDocument(updatedResource));
            if (jsonPatch.RootElement.GetArrayLength() < 1)
            {
                this.SetSuccessMessage("Neither the specification, the labels nor the annotations have changed: there is nothing to update");
                return;
            }
            try
            {
                resource = await resourceManagementApi.Manage<TResource>().PatchAsync(new Patch(PatchType.JsonPatch, jsonPatch), resource.GetName(), resource.GetNamespace(), this.CancellationTokenSource.Token);
                this.SetResource(resource);
                this.SetSuccessMessage($"The {typeof(TResource).Name.ToLower()} '{resource.GetName()}' has been updated");
            }
            catch (ProblemDetailsException ex)
            {
                this.SetProblemDetails(ex.Problem);
            }
        }
        catch (Exception ex)
        {
            this.SetProblemDetails(new ProblemDetails(
                ProblemTypes.SchemaValidationFailed,
                "Update failed",
                400,
                ex.Message
            ));
        }
        finally
        {
            this.SetSaving(false);
        }
    }

    /// <summary>
    /// Gets a <see cref="JsonElement"/> that only contains the parts of the specified resource users may edit: its spec, labels and annotations
    /// </summary>
    /// <param name="resource">The resource to get the editable document of</param>
    /// <returns>A new <see cref="JsonElement"/> of the form <c>{ "metadata": { "labels": ..., "annotations": ... }, "spec": ... }</c></returns>
    JsonElement GetEditableDocument(TResource? resource) => JsonDocument.Parse(ResourceDocuments.GetEditableDocument(resource, jsonSerializer).ToJsonString()).RootElement;

    /// <summary>
    /// Compares the editable parts of the resource to the editor's, and sets the differences as the changes to review before they are saved (see #227)
    /// </summary>
    /// <remarks>Nothing is set if the editor's value is invalid, which is reported, or if nothing changed, which the success message says</remarks>
    public void ReviewChanges()
    {
        this.SetProblemDetails(null);
        var resource = this.Get(state => state.Resource);
        if (resource == null) return;
        try
        {
            var textEditorValue = this.Get(state => state.TextEditorValue);
            if (monacoEditorHelper.PreferredLanguage == PreferredLanguage.YAML) textEditorValue = yamlSerializer.ConvertToJson(textEditorValue);
            var updatedResource = jsonSerializer.Deserialize<TResource>(textEditorValue);
            if (!this.ValidateMappedProperties(textEditorValue, updatedResource)) return;
            var original = ResourceDocuments.ToYaml(ResourceDocuments.GetEditableDocument(resource, jsonSerializer), yamlSerializer);
            var modified = ResourceDocuments.ToYaml(ResourceDocuments.GetEditableDocument(updatedResource, jsonSerializer), yamlSerializer);
            var changes = TextDiff.Compute(original, modified);
            if (changes.All(l => l.Kind == TextDiffLineKind.Unchanged))
            {
                this.SetSuccessMessage("Neither the specification, the labels nor the annotations have changed: there is nothing to update");
                return;
            }
            this.Reduce(state => state with { PendingChanges = changes });
        }
        catch (Exception ex)
        {
            this.SetProblemDetails(new ProblemDetails(ProblemTypes.SchemaValidationFailed, "Invalid resource", 400, ex.Message));
        }
    }

    /// <summary>
    /// Discards the changes under review, to go back to editing
    /// </summary>
    public void CancelReview() => this.Reduce(state => state with { PendingChanges = null });

    /// <summary>
    /// Gets an <see cref="IObservable{T}"/> used to observe the changes to review before they are saved
    /// </summary>
    public IObservable<IReadOnlyList<TextDiffLine>?> PendingChanges => this.Select(state => state.PendingChanges).DistinctUntilChanged();

    /// <summary>
    /// Gets the options used to format JSON in the editor
    /// </summary>
    static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Ensures that every property of the specified editor value is mapped by the resource it has been deserialized into, and reports the ones that are not
    /// </summary>
    /// <param name="json">The JSON the resource has been deserialized from</param>
    /// <param name="resource">The deserialized resource</param>
    /// <returns>A boolean indicating whether or not all properties have been mapped</returns>
    /// <remarks>
    /// The editor value is deserialized into a strongly typed resource before being sent, which silently discards any property the resource does not define,
    /// such as one declared at the wrong level. Reporting them avoids creating or updating a resource that differs from what the user wrote (see #127)
    /// </remarks>
    bool ValidateMappedProperties(string json, TResource? resource)
    {
        var unmapped = FindUnmappedProperties(JsonNode.Parse(json), resource == null ? null : JsonNode.Parse(jsonSerializer.SerializeToElement(resource)!.Value.GetRawText()), string.Empty).ToList();
        if (unmapped.Count < 1) return true;
        this.SetProblemDetails(new ProblemDetails(
            ProblemTypes.SchemaValidationFailed,
            "Unsupported properties",
            400,
            $"The following properties are not supported by the {typeof(TResource).Name.ToLower()} definition, and would be ignored. Please check that they are declared at the right level",
            null,
            new Dictionary<string, string[]>() { { "Unsupported properties", [.. unmapped] } }));
        return false;
    }

    /// <summary>
    /// Finds the properties of the specified source node that are missing from the specified mapped node
    /// </summary>
    /// <param name="source">The node, as written by the user</param>
    /// <param name="mapped">The node, as mapped by the resource</param>
    /// <param name="path">The path of the nodes</param>
    /// <returns>A new <see cref="IEnumerable{T}"/> containing the paths of all unmapped properties</returns>
    static IEnumerable<string> FindUnmappedProperties(JsonNode? source, JsonNode? mapped, string path)
    {
        if (source is JsonObject sourceObject)
        {
            var mappedObject = mapped as JsonObject;
            foreach (var property in sourceObject)
            {
                if (IsEmptyOrDefault(property.Value)) continue;
                var propertyPath = string.IsNullOrEmpty(path) ? property.Key : $"{path}.{property.Key}";
                if (mappedObject == null || !mappedObject.TryGetPropertyValue(property.Key, out var mappedValue) || mappedValue == null) yield return propertyPath;
                else foreach (var unmapped in FindUnmappedProperties(property.Value, mappedValue, propertyPath)) yield return unmapped;
            }
        }
        else if (source is JsonArray sourceArray && mapped is JsonArray mappedArray)
        {
            for (var i = 0; i < Math.Min(sourceArray.Count, mappedArray.Count); i++)
            {
                foreach (var unmapped in FindUnmappedProperties(sourceArray[i], mappedArray[i], $"{path}[{i}]")) yield return unmapped;
            }
        }
    }

    /// <summary>
    /// Determines whether or not the specified node is empty or holds a default value, which serializers may legitimately omit
    /// </summary>
    static bool IsEmptyOrDefault(JsonNode? node) => node switch
    {
        null => true,
        JsonObject o => o.Count == 0,
        JsonArray a => a.Count == 0,
        JsonValue v => v.GetValueKind() switch
        {
            JsonValueKind.False or JsonValueKind.Null => true,
            JsonValueKind.Number => v.ToJsonString() is "0" or "0.0",
            JsonValueKind.String => string.IsNullOrEmpty(v.GetValue<string>()),
            _ => false
        },
        _ => false
    };

    /// <summary>
    /// Sets the message to display once the resource has been successfully saved
    /// </summary>
    /// <param name="message">The message to display</param>
    public void SetSuccessMessage(string? message)
    {
        this.Reduce(state => state with
        {
            SuccessMessage = message
        });
    }

}
