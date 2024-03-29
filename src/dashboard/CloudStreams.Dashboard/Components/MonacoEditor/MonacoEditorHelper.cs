﻿// Copyright © 2023-Present The Cloud Streams Authors
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
            Value = value,
            TabSize = 2
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
