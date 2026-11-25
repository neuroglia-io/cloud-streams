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

using Microsoft.JSInterop;

namespace CloudStreams.Dashboard.Services;

/// <summary>
/// Exposes the dashboard's color themes
/// </summary>
public static class Themes
{

    /// <summary>
    /// Gets the preference that follows the system's theme
    /// </summary>
    public const string Auto = "auto";

    /// <summary>
    /// Gets the light theme
    /// </summary>
    public const string Light = "light";

    /// <summary>
    /// Gets the dark theme
    /// </summary>
    public const string Dark = "dark";

}

/// <summary>
/// Represents the service used to get and change the dashboard's color theme (see #229)
/// </summary>
/// <param name="jsRuntime">The service used to interop with <c>js/theme.js</c>, which applies the theme</param>
/// <remarks>
/// The theme follows the system's by default, and the user's choice, once made, is remembered by the browser. The theme in effect is applied before the first paint, by
/// <c>js/theme.js</c>, which notifies this service whenever it changes, including when the system's changes
/// </remarks>
public sealed class ThemeService(IJSRuntime jsRuntime)
    : IAsyncDisposable
{

    DotNetObjectReference<ThemeService>? _reference;

    /// <summary>
    /// Gets the theme the user chose: <see cref="Themes.Auto"/>, <see cref="Themes.Light"/> or <see cref="Themes.Dark"/>
    /// </summary>
    public string Preference { get; private set; } = Themes.Auto;

    /// <summary>
    /// Gets the theme in effect: <see cref="Themes.Light"/> or <see cref="Themes.Dark"/>
    /// </summary>
    public string Theme { get; private set; } = Themes.Dark;

    /// <summary>
    /// Gets the Monaco editor theme that matches the theme in effect
    /// </summary>
    public string EditorTheme => this.Theme == Themes.Light ? "vs" : "vs-dark";

    /// <summary>
    /// Occurs when the theme in effect, or the user's preference, has changed
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Reads the theme in effect, and starts observing its changes
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public async Task InitializeAsync()
    {
        this.Preference = await jsRuntime.InvokeAsync<string>("cloudStreamsTheme.getPreference").ConfigureAwait(false);
        this.Theme = await jsRuntime.InvokeAsync<string>("cloudStreamsTheme.getTheme").ConfigureAwait(false);
        this._reference ??= DotNetObjectReference.Create(this);
        await jsRuntime.InvokeVoidAsync("cloudStreamsTheme.observe", this._reference).ConfigureAwait(false);
    }

    /// <summary>
    /// Remembers and applies the specified theme
    /// </summary>
    /// <param name="preference">The theme to apply: <see cref="Themes.Auto"/>, <see cref="Themes.Light"/> or <see cref="Themes.Dark"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public async Task SetPreferenceAsync(string preference)
    {
        this.Preference = preference is Themes.Light or Themes.Dark ? preference : Themes.Auto;
        this.Theme = await jsRuntime.InvokeAsync<string>("cloudStreamsTheme.setPreference", this.Preference).ConfigureAwait(false);
        this.Changed?.Invoke();
    }

    /// <summary>
    /// Handles a change of the theme in effect, notified by <c>js/theme.js</c>
    /// </summary>
    /// <param name="theme">The theme in effect</param>
    [JSInvokable]
    public void OnThemeChanged(string theme)
    {
        if (theme == this.Theme) return;
        this.Theme = theme == Themes.Light ? Themes.Light : Themes.Dark;
        this.Changed?.Invoke();
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        this._reference?.Dispose();
        return ValueTask.CompletedTask;
    }

}
