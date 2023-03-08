using BlazorMonaco;
using System.ComponentModel;

namespace CloudStreams.Dashboard.Components;

public delegate Task PreferedLanguageChangedEventHandler(string newLanguage);

/// <summary>
/// Represents a service used to facilitate the Monaco editor configuration
/// </summary>
public interface IMonacoEditorHelper
{
    /// <summary>
    /// The prefered editor language
    /// </summary>
    string PreferedLanguage { get; }

    /// <summary>
    /// Emits when the editor language changes
    /// </summary>
    event PreferedLanguageChangedEventHandler? PreferedLanguageChanged;

    /// <summary>
    /// A function used to facilitate the construction of <see cref="StandaloneEditorConstructionOptions"/> 
    /// </summary>
    /// <param name="value">The text of the editor</param>
    /// <param name="readOnly">Defines if the editor should be in read only</param>
    /// <param name="language">The default prefered language</param>
    /// <returns></returns>
    Func<StandaloneCodeEditor, StandaloneEditorConstructionOptions> GetStandaloneEditorConstructionOptions(string value = "", bool readOnly = false, string language = "json");

    /// <summary>
    /// A function used to facilitate the construction of <see cref="DiffEditorConstructionOptions"/> 
    /// </summary>
    /// <param name="readOnly">Defines if the editor should be in read only</param>
    /// <returns></returns>
    Func<StandaloneDiffEditor, DiffEditorConstructionOptions> GetDiffEditorConstructionOptions(bool readOnly = true);
    
    /// <summary>
    /// Changes the prefered editor language
    /// </summary>
    /// <param name="language">The new language to use</param>
    /// <returns></returns>
    Task ChangePreferedLanguageAsync(string language);

}
