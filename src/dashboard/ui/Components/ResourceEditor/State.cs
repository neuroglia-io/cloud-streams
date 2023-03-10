using Microsoft.AspNetCore.Components;

namespace CloudStreams.Dashboard.Components.ResourceEditorStateManagement;

/// <summary>
/// Represents the state of the <see cref="ResourceEditor{TResource}"/>'s component
/// </summary>
public record ResourceEditorState<TResource>
    where TResource : class, IResource, new()
{
    /// <summary>
    /// Gets/sets the resource to display details about
    /// </summary>
    public TResource? Resource { get; set; } = null;

    /// <summary>
    /// Gets/sets the definition of the displayed resource
    /// </summary>
    public ResourceDefinition? Definition { get; set; } = null;

    /// <summary>
    /// Gets/sets the content of the text editor
    /// </summary>
    public string TextEditorValue { get; set; } = string.Empty;

    /// <summary>
    /// Gets/sets a boolean indicating if the text editor is being updated
    /// </summary>
    public bool Updating { get; set; } = false;

    /// <summary>
    /// Gets/sets a boolean indicating if the resource is being saved
    /// </summary>
    public bool Saving { get; set; } = false;

    /// <summary>
    /// Gets/sets the list of errors that occured when trying to save the resource, if any
    /// </summary>
    public IDictionary<string, string[]> Errors { get; set; } = new Dictionary<string, string[]>();
}
