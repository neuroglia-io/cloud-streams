namespace CloudStreams.Dashboard.Components;

/// <inheritdoc />
public class MonacoEditorMarker 
    : IMonacoEditorMarker
{
    /// <inheritdoc />
    public string? Code { get; set; }
    /// <inheritdoc />
    public int EndColumn { get; set; }
    /// <inheritdoc />
    public int EndLineNumber { get; set; }
    /// <inheritdoc />
    public string Message { get; set; } = "";
    /// <inheritdoc />
    public string Owner { get; set; } = "";
    /// <inheritdoc />
    public IEnumerable<Object>? RelatedInformation { get; set; }
    /// <inheritdoc />
    public Object Resource { get; set; } = "";
    /// <inheritdoc />
    public int Severity { get; set; }
    /// <inheritdoc />
    public int StartColumn { get; set; }
    /// <inheritdoc />
    public int StartLineNumber { get; set; }
    /// <inheritdoc />
    public IEnumerable<Object>? Tags { get; set; }
    /// <inheritdoc />
    public string? ModelVersionId { get; set; }
    /// <inheritdoc />
    public string? Source { get; set; }
}
