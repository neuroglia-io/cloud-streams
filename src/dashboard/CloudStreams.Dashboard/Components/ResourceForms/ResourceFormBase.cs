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
using Microsoft.AspNetCore.Components;
using Neuroglia;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Serialization;

namespace CloudStreams.Dashboard.Components.ResourceForms;

/// <summary>
/// Defines what the layout of a resource form needs to know about it, whatever the type of the resource it edits
/// </summary>
public interface IResourceForm
{

    /// <summary>
    /// Gets the prefix of the ids of the form's elements
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets a boolean indicating whether the form creates a resource, rather than editing one
    /// </summary>
    bool IsNew { get; }

    /// <summary>
    /// Gets the titles of the steps of the form, the last of which is the review
    /// </summary>
    IReadOnlyList<string> Steps { get; }

    /// <summary>
    /// Gets the index of the current step
    /// </summary>
    int CurrentStep { get; }

    /// <summary>
    /// Gets a boolean indicating whether the review is shown
    /// </summary>
    bool IsReview { get; }

    /// <summary>
    /// Gets a boolean indicating whether the YAML view is shown, rather than the form
    /// </summary>
    bool ShowingYaml { get; }

    /// <summary>
    /// Gets/sets the YAML document edited in the YAML view
    /// </summary>
    string Yaml { get; set; }

    /// <summary>
    /// Gets the reason why the YAML document cannot be applied, if any
    /// </summary>
    string? YamlError { get; }

    /// <summary>
    /// Gets the errors of the fields, keyed by the ids of the fields
    /// </summary>
    IReadOnlyDictionary<string, string> Errors { get; }

    /// <summary>
    /// Gets the problem the server reported, if any
    /// </summary>
    string? Problem { get; }

    /// <summary>
    /// Gets the changes to save, when editing a resource
    /// </summary>
    IReadOnlyList<TextDiffLine>? Diff { get; }

    /// <summary>
    /// Gets a plain-language summary of what is about to be saved
    /// </summary>
    RenderFragment Summary { get; }

    /// <summary>
    /// Gets a boolean indicating whether the form is waiting for the server
    /// </summary>
    bool Busy { get; }

    /// <summary>
    /// Gets a boolean indicating whether the advanced editor can be opened instead
    /// </summary>
    bool CanOpenEditor { get; }

    /// <summary>
    /// Shows the YAML view
    /// </summary>
    void ShowYaml();

    /// <summary>
    /// Applies the YAML document, and shows the form again
    /// </summary>
    void ShowForm();

    /// <summary>
    /// Goes to the specified step, if the steps before it are valid
    /// </summary>
    /// <param name="step">The index of the step to go to</param>
    Task GoToAsync(int step);

    /// <summary>
    /// Goes to the next step
    /// </summary>
    Task NextAsync();

    /// <summary>
    /// Goes back to the previous step
    /// </summary>
    void Back();

    /// <summary>
    /// Saves the resource
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// Closes the form without saving
    /// </summary>
    Task CancelAsync();

    /// <summary>
    /// Opens the advanced editor instead, with what has been edited so far
    /// </summary>
    Task OpenEditorAsync();

}

/// <summary>
/// Represents the base class of the forms used to create and edit resources (see #260)
/// </summary>
/// <typeparam name="TResource">The type of the resource to create or edit</typeparam>
/// <remarks>
/// The form and the YAML view edit the same resource. Each step is validated before the next one, the whole resource is validated by the server, without saving
/// it, before the review, and the review shows the changes an edition makes
/// </remarks>
public abstract class ResourceFormBase<TResource>
    : ComponentBase, IResourceForm
    where TResource : Resource, new()
{

    readonly Dictionary<string, string> _errors = [];
    string? _focus;
    TResource? _original;
    bool _initialized;

    /// <summary>
    /// Gets the client of the Cloud Streams API
    /// </summary>
    [Inject] protected ICloudStreamsCoreApiClient Api { get; set; } = null!;

    /// <summary>
    /// Gets the service used to serialize JSON
    /// </summary>
    [Inject] protected IJsonSerializer JsonSerializer { get; set; } = null!;

    /// <summary>
    /// Gets the service used to serialize YAML
    /// </summary>
    [Inject] protected IYamlSerializer YamlSerializer { get; set; } = null!;

    /// <summary>
    /// Gets the service used to move the focus
    /// </summary>
    [Inject] protected CommonJsInterop JsInterop { get; set; } = null!;

    /// <summary>
    /// Gets the service that holds the namespace the dashboard shows the resources of, which new resources are created in by default (see #215)
    /// </summary>
    [Inject] protected NamespaceContext NamespaceContext { get; set; } = null!;

    /// <summary>
    /// Gets a boolean indicating whether or not the edited resources belong to namespaces
    /// </summary>
    protected static bool IsNamespaced { get; } = CloudStreamsDefaults.Resources.Definitions.AsEnumerable().Any(d => d.Spec.Names.Kind == new TResource().Definition.Kind && d.Spec.Scope == ResourceScope.Namespaced);

    /// <summary>
    /// Gets the namespace of the edited resource, if it is namespaced
    /// </summary>
    /// <returns>The namespace of the edited resource</returns>
    protected string? GetNamespace() => IsNamespaced ? this.Model?.GetNamespace() ?? this.NamespaceContext.ForNewResources : null;

    /// <summary>
    /// Gets/sets the resource to edit, if any. Without one, the form creates a resource
    /// </summary>
    [Parameter] public TResource? Resource { get; set; }

    /// <summary>
    /// Gets/sets the callback invoked once the resource has been saved
    /// </summary>
    [Parameter] public EventCallback<TResource> OnSaved { get; set; }

    /// <summary>
    /// Gets/sets the callback invoked when the form is closed without saving
    /// </summary>
    [Parameter] public EventCallback OnCancel { get; set; }

    /// <summary>
    /// Gets/sets the callback invoked to open the advanced editor instead, with what has been edited so far, if any
    /// </summary>
    [Parameter] public EventCallback<TResource> OnOpenEditor { get; set; }

    /// <inheritdoc/>
    public virtual string Id => typeof(TResource).Name.ToLowerInvariant() + "-form";

    /// <summary>
    /// Gets the resource being edited
    /// </summary>
    protected TResource Model { get; private set; } = null!;

    /// <inheritdoc/>
    public bool IsNew => this.Resource == null;

    /// <summary>
    /// Gets the titles of the steps of the form, without the review
    /// </summary>
    protected abstract IReadOnlyList<string> FormSteps { get; }

    /// <inheritdoc/>
    public IReadOnlyList<string> Steps => [.. this.FormSteps, "Review"];

    /// <inheritdoc/>
    public int CurrentStep { get; private set; }

    /// <inheritdoc/>
    public bool IsReview => this.CurrentStep == this.Steps.Count - 1;

    /// <inheritdoc/>
    public bool ShowingYaml { get; private set; }

    /// <inheritdoc/>
    public string Yaml { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string? YamlError { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Errors => this._errors;

    /// <inheritdoc/>
    public string? Problem { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyList<TextDiffLine>? Diff { get; private set; }

    /// <inheritdoc/>
    public abstract RenderFragment Summary { get; }

    /// <inheritdoc/>
    public bool Busy { get; private set; }

    /// <inheritdoc/>
    public bool CanOpenEditor => this.OnOpenEditor.HasDelegate;

    /// <summary>
    /// Creates the resource a new form starts with
    /// </summary>
    /// <returns>A new resource, with sensible defaults</returns>
    protected abstract TResource CreateDefault();

    /// <summary>
    /// Validates the specified step of the form
    /// </summary>
    /// <param name="step">The index of the step to validate</param>
    /// <param name="errors">The errors found, keyed by the ids of the fields</param>
    protected abstract void Validate(int step, IDictionary<string, string> errors);

    /// <summary>
    /// Handles the replacement of the resource being edited, when the form opens and when the YAML view is applied: the choices the form shows are derived from it again
    /// </summary>
    protected virtual void OnModelReplaced() { }

    /// <summary>
    /// Adjusts the resource before it is validated by the server and saved, e.g. to remove the parts of the spec that the form leaves empty
    /// </summary>
    /// <param name="resource">The resource to adjust</param>
    protected virtual void Normalize(TResource resource) { }

    /// <summary>
    /// Gets the id of the element of the specified field
    /// </summary>
    /// <param name="field">The name of the field</param>
    /// <returns>The id of the element of the field</returns>
    protected string FieldId(string field) => $"{this.Id}-{field}";

    /// <summary>
    /// Gets the error of the specified field, if any
    /// </summary>
    /// <param name="field">The name of the field</param>
    /// <returns>The error of the field, if any</returns>
    protected string? ErrorOf(string field) => this._errors.GetValueOrDefault(this.FieldId(field));

    /// <inheritdoc/>
    /// <remarks>The resource is copied when the form is initialized, before its first render, which may happen while a derived form is still loading what it needs</remarks>
    protected override void OnInitialized()
    {
        base.OnInitialized();
        if (this._initialized) return;
        this._initialized = true;
        this._original = this.Resource;
        this.Model = this.Resource == null ? this.CreateDefault() : ResourceFormDocuments.Clone(this.Resource, this.JsonSerializer);
        if (IsNamespaced && string.IsNullOrWhiteSpace(this.Model.GetNamespace())) this.Model.Metadata.Namespace = this.NamespaceContext.ForNewResources;
        this.OnModelReplaced();
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (this._focus == null) return;
        var focus = this._focus;
        this._focus = null;
        // moving the focus is a convenience, which must never break the form, e.g. when the browser still runs an older version of the script
        try { await this.JsInterop.FocusAsync(focus); }
        catch (Microsoft.JSInterop.JSException) { }
    }

    /// <summary>
    /// Gets the id of the heading of the current step
    /// </summary>
    public string StepTitleId => $"{this.Id}-step-title";

    /// <inheritdoc/>
    public void ShowYaml()
    {
        this.Yaml = ResourceFormDocuments.ToYaml(this.Prepare(), this.JsonSerializer, this.YamlSerializer);
        this.YamlError = null;
        this.ShowingYaml = true;
        this.StateHasChanged();
    }

    /// <inheritdoc/>
    public void ShowForm()
    {
        if (!this.TryApplyYaml()) return;
        this.ShowingYaml = false;
        this.StateHasChanged();
    }

    /// <inheritdoc/>
    public async Task GoToAsync(int step)
    {
        if (this.ShowingYaml && !this.TryApplyYaml()) return;
        this.ShowingYaml = false;
        if (step <= this.CurrentStep)
        {
            this.CurrentStep = Math.Max(0, step);
            this.Problem = null;
            this.StateHasChanged();
            return;
        }
        // the steps between the current one and the requested one must be valid, so that the review is never reached with invalid fields
        for (var i = this.CurrentStep; i < Math.Min(step, this.FormSteps.Count); i++)
        {
            if (!this.ValidateStep(i))
            {
                this.CurrentStep = i;
                this.StateHasChanged();
                return;
            }
        }
        if (step >= this.Steps.Count - 1) await this.ReviewAsync();
        else this.CurrentStep = step;
        // the heading of the step is announced, unless an invalid field took the focus
        this._focus ??= this.StepTitleId;
        this.StateHasChanged();
    }

    /// <inheritdoc/>
    public Task NextAsync() => this.GoToAsync(this.CurrentStep + 1);

    /// <inheritdoc/>
    public void Back()
    {
        if (this.CurrentStep == 0) return;
        this.CurrentStep--;
        this._focus = this.StepTitleId;
        this.Problem = null;
        this.StateHasChanged();
    }

    /// <summary>
    /// Validates the resource with the server, without saving it, and shows the review
    /// </summary>
    protected virtual async Task ReviewAsync()
    {
        this.Problem = null;
        this.Diff = null;
        this.Busy = true;
        this.StateHasChanged();
        try
        {
            var resource = this.Prepare();
            if (this._original == null)
            {
                this.Yaml = ResourceFormDocuments.ToYaml(resource, this.JsonSerializer, this.YamlSerializer);
                await this.Api.Manage<TResource>().CreateAsync(resource, true);
            }
            else
            {
                this.Diff = ResourceFormDocuments.Diff(this._original, resource, this.JsonSerializer, this.YamlSerializer);
                var patch = ResourceFormDocuments.GetPatch(this._original, resource, this.JsonSerializer);
                if (patch != null) await this.Api.Manage<TResource>().PatchAsync(patch, resource.GetName(), resource.GetNamespace(), true);
            }
            this.CurrentStep = this.Steps.Count - 1;
        }
        catch (Exception ex)
        {
            this.Problem = this.ApplyServerErrors(ex);
        }
        finally
        {
            this.Busy = false;
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync()
    {
        this.Problem = null;
        this.Busy = true;
        this.StateHasChanged();
        try
        {
            var resource = this.Prepare();
            TResource saved;
            if (this._original == null) saved = await this.Api.Manage<TResource>().CreateAsync(resource);
            else
            {
                var patch = ResourceFormDocuments.GetPatch(this._original, resource, this.JsonSerializer);
                saved = patch == null ? this._original : await this.Api.Manage<TResource>().PatchAsync(patch, resource.GetName(), resource.GetNamespace());
            }
            await this.OnSaved.InvokeAsync(saved);
        }
        catch (Exception ex)
        {
            this.Problem = Describe(ex);
        }
        finally
        {
            this.Busy = false;
            this.StateHasChanged();
        }
    }

    /// <inheritdoc/>
    public Task CancelAsync() => this.OnCancel.InvokeAsync();

    /// <inheritdoc/>
    public async Task OpenEditorAsync()
    {
        if (this.ShowingYaml && !this.TryApplyYaml()) return;
        await this.OnOpenEditor.InvokeAsync(this.Prepare());
    }

    /// <summary>
    /// Gets a copy of the resource being edited, as it is to be validated and saved
    /// </summary>
    /// <returns>The resource to validate and save</returns>
    protected TResource Prepare()
    {
        var resource = ResourceFormDocuments.Clone(this.Model, this.JsonSerializer);
        this.Normalize(resource);
        return resource;
    }

    /// <summary>
    /// Validates the specified step, and records its errors
    /// </summary>
    /// <param name="step">The index of the step to validate</param>
    /// <returns>A boolean indicating whether the step is valid</returns>
    protected bool ValidateStep(int step)
    {
        foreach (var key in this._errors.Keys.Where(k => k.StartsWith(this.Id + "-", StringComparison.Ordinal)).ToList()) this._errors.Remove(key);
        var errors = new Dictionary<string, string>();
        this.Validate(step, errors);
        foreach (var (field, error) in errors) this._errors[this.FieldId(field)] = error;
        // the first invalid field gets the focus, so that keyboard and screen reader users land on what to correct
        if (errors.Count > 0) this._focus = this.FieldId(errors.Keys.First());
        return errors.Count == 0;
    }

    bool TryApplyYaml()
    {
        if (!this.ShowingYaml) return true;
        try
        {
            this.Model = ResourceFormDocuments.Apply(this.Model, this.Yaml, this.IsNew, this.JsonSerializer, this.YamlSerializer);
            this.OnModelReplaced();
            this.YamlError = null;
            return true;
        }
        catch (FormatException ex)
        {
            this.YamlError = ex.Message;
            this.StateHasChanged();
            return false;
        }
    }

    /// <summary>
    /// Gets the fields that the paths of the server's validation errors map to, with the index of the step of each, e.g. <c>spec.subscriber.uri</c> to the subscriber's uri
    /// </summary>
    protected virtual IReadOnlyDictionary<string, (string Field, int Step)> ServerFields { get; } = new Dictionary<string, (string Field, int Step)>();

    /// <summary>
    /// Shows the validation errors the server reported next to the fields they concern, on the first step that has one
    /// </summary>
    /// <param name="ex">The exception that describes why the server refused the resource</param>
    /// <returns>A description of the errors that concern no known field, if any</returns>
    string? ApplyServerErrors(Exception ex)
    {
        if (ex is not ProblemDetailsException problem || problem.Problem.Errors == null || problem.Problem.Errors.Count == 0) return Describe(ex);
        var unmapped = new List<string>();
        int? step = null;
        foreach (var (path, messages) in problem.Problem.Errors)
        {
            // the errors of the items of a list are keyed by their index, e.g. spec.filter.attributes[0], and shown with the list
            var key = System.Text.RegularExpressions.Regex.Replace(path.Trim(), @"\[\d+\]", string.Empty);
            if (this.TryGetServerField(key, out var field))
            {
                this._errors[this.FieldId(field.Field)] = string.Join(" ", messages);
                step = step.HasValue ? Math.Min(step.Value, field.Step) : field.Step;
            }
            else unmapped.AddRange(messages.Select(m => string.IsNullOrWhiteSpace(key) ? m : $"{key}: {m}"));
        }
        if (step.HasValue) this.CurrentStep = step.Value;
        if (this._errors.Count > 0) this._focus = this._errors.Keys.First();
        if (unmapped.Count > 0) return $"{problem.Problem.Detail ?? problem.Problem.Title} {string.Join(" ", unmapped)}".Trim();
        return step.HasValue ? null : Describe(ex);
    }

    /// <summary>
    /// Gets the field the specified path maps to, or, failing that, the field its closest parent maps to, e.g. <c>spec.ingestion.rateLimits.limit</c> to the rate limits
    /// </summary>
    /// <param name="path">The path of the field, without the indexes of list items</param>
    /// <param name="field">The field the path maps to, if any</param>
    /// <returns>A boolean indicating whether the path maps to a field</returns>
    bool TryGetServerField(string path, out (string Field, int Step) field)
    {
        for (var candidate = path; !string.IsNullOrEmpty(candidate); candidate = candidate.Contains('.') ? candidate[..candidate.LastIndexOf('.')] : string.Empty)
        {
            if (this.ServerFields.TryGetValue(candidate, out field)) return true;
        }
        field = default;
        return false;
    }

    static string Describe(Exception ex)
    {
        if (ex is not ProblemDetailsException problem) return ex.Message;
        var detail = problem.Problem.Detail ?? problem.Problem.Title;
        var errors = problem.Problem.Errors?.SelectMany(e => e.Value.Select(message => string.IsNullOrWhiteSpace(e.Key) ? message : $"{e.Key}: {message}")).ToList();
        return errors?.Count > 0 ? $"{detail} {string.Join(" ", errors)}" : detail ?? ex.Message;
    }

}
