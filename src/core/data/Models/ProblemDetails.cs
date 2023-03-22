namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to describe a problem, as defined by <see href="https://www.rfc-editor.org/rfc/rfc7807">RFC 7807</see>
/// </summary>
[DataContract]
public class ProblemDetails
    : IExtensible
{

    /// <summary>
    /// Initialize a new <see cref="ProblemDetails"/>
    /// </summary>
    public ProblemDetails() { }

    /// <summary>
    /// Initialize a new <see cref="ProblemDetails"/>
    /// </summary>
    /// <param name="type">An uri that reference the type of the described problem</param>
    /// <param name="title">A short, human-readable summary of the problem type.It SHOULD NOT change from occurrence to occurrence of the problem, except for purposes of localization</param>
    /// <param name="status">The status code produced by the described problem</param>
    /// <param name="detail">A human-readable explanation specific to this occurrence of the problem</param>
    /// <param name="instance">A <see cref="Uri"/> reference that identifies the specific occurrence of the problem.It may or may not yield further information if dereferenced</param>
    /// <param name="errors">An optional collection containing error messages mapped per error code</param>
    /// <param name="extensionData">A mapping containing problem details extension data, if any</param>
    public ProblemDetails(Uri type, string title, int status, string? detail = null, Uri? instance = null, IDictionary<string, string[]>? errors = null, IDictionary<string, object>? extensionData = null)
    {
        this.Type = type ?? throw new ArgumentNullException(nameof(type));
        this.Title = title ?? throw new ArgumentNullException(nameof(title));
        this.Status = status;
        this.Detail = detail;
        this.Instance = instance;
        this.Errors = errors;
        this.ExtensionData = extensionData;
    }

    /// <summary>
    /// Gets/sets an uri that reference the type of the described problem.
    /// </summary>
    [DataMember(Order = 1, Name = "type"), JsonPropertyName("type"), YamlMember(Alias = "type")]
    public virtual Uri? Type { get; set; } = null!;

    /// <summary>
    /// Gets/sets a short, human-readable summary of the problem type.It SHOULD NOT change from occurrence to occurrence of the problem, except for purposes of localization.
    /// </summary>
    [DataMember(Order = 2, Name = "title"), JsonPropertyName("title"), YamlMember(Alias = "title")]
    public virtual string? Title { get; set; } = null!;

    /// <summary>
    /// Gets/sets the status code produced by the described problem
    /// </summary>
    [DataMember(Order = 3, Name = "status"), JsonPropertyName("status"), YamlMember(Alias = "status")]
    public virtual int Status { get; set; }

    /// <summary>
    /// Gets/sets a human-readable explanation specific to this occurrence of the problem.
    /// </summary>
    [DataMember(Order = 4, Name = "detail"), JsonPropertyName("detail"), YamlMember(Alias = "detail")]
    public virtual string? Detail { get; set; }

    /// <summary>
    /// Gets/sets a <see cref="Uri"/> reference that identifies the specific occurrence of the problem.It may or may not yield further information if dereferenced.
    /// </summary>
    [DataMember(Order = 5, Name = "instance"), JsonPropertyName("instance"), YamlMember(Alias = "instance")]
    public virtual Uri? Instance { get; set; }

    /// <summary>
    /// Gets/sets an optional collection containing error messages mapped per error code
    /// </summary>
    [DataMember(Order = 6, Name = "errors"), JsonPropertyName("errors"), YamlMember(Alias = "errors")]
    public virtual IDictionary<string, string[]>? Errors { get; set; }

    /// <summary>
    /// Gets/sets a mapping containing problem details extension data, if any
    /// </summary>
    [DataMember(Order = 7, Name = "extensionData"), JsonExtensionData]
    public virtual IDictionary<string, object>? ExtensionData { get; set; }

    /// <inheritdoc/>
    public override string ToString() => this.Detail ?? Environment.NewLine + string.Join(Environment.NewLine, this.Errors?.Select(e => string.Join(Environment.NewLine, e.Value.Select(v => $"{e.Key}: {v}")))!);

}