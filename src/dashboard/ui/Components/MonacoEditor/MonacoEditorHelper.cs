using BlazorMonaco;

namespace CloudStreams.Dashboard.Components;

/// <inheritdoc />
public class MonacoEditorHelper
    : IMonacoEditorHelper
{
    /// <inheritdoc />
    public string PreferedLanguage { get; protected set; } = "yaml";

    /// <inheritdoc />
    public event PreferedLanguageChangedEventHandler? PreferedLanguageChanged;

    /// <inheritdoc />
    public Func<StandaloneCodeEditor, StandaloneEditorConstructionOptions> GetStandaloneEditorConstructionOptions(string value = "", bool readOnly = false, string language = "yaml") {
        return (StandaloneCodeEditor editor) => new StandaloneEditorConstructionOptions
        {
            Theme = "vs-dark",
            AutomaticLayout = true,
            Minimap = new EditorMinimapOptions { Enabled = false },
            Language = language,
            ReadOnly = readOnly,
            Value = value
        };
    }

    /// <inheritdoc />
    public Func<StandaloneDiffEditor, DiffEditorConstructionOptions> GetDiffEditorConstructionOptions(bool readOnly = true)
    {
        return (StandaloneDiffEditor editor) => new DiffEditorConstructionOptions
        {
            AutomaticLayout = true,
            Minimap = new EditorMinimapOptions { Enabled = false },
            ReadOnly = readOnly
        };
    }

    /// <inheritdoc />
    public async Task ChangePreferedLanguageAsync(string language)
    {
        if (!string.IsNullOrEmpty(language) && language != this.PreferedLanguage)
        {
            this.PreferedLanguage = language;
            await this.OnPreferedLanguageChangeAsync(language);
        }
    }

    /// <inheritdoc />
    protected async Task OnPreferedLanguageChangeAsync(string language)
    {
        if (this.PreferedLanguageChanged != null)
        {
            await this.PreferedLanguageChanged.Invoke(language);
        }
        await Task.CompletedTask;
    }
}
