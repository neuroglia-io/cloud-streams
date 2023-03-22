namespace CloudStreams.Dashboard.Components;

/// <summary>
/// See https://microsoft.github.io/monaco-editor/docs.html#enums/MarkerSeverity.html
/// </summary>
public enum MonacoEditorMarkerSeverity
{
    /// <summary>
    /// Hint
    /// </summary>
    Hint = 1,
    /// <summary>
    /// Info
    /// </summary>
    Info = 2,
    /// <summary>
    /// Warning
    /// </summary>
    Warning = 4,
    /// <summary>
    /// Error
    /// </summary>
    Error = 8,
}
