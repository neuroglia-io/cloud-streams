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

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CloudStreams.Dashboard.Services;

/// <summary>
/// The service used to build a bridge with JS interop
/// </summary>
/// <param name="jsRuntime">The service used to interop with JS</param>
public class CommonJsInterop(IJSRuntime jsRuntime)
    : IAsyncDisposable
{
    /// <summary>
    /// A reference to the js interop module
    /// </summary>
    readonly Lazy<Task<IJSObjectReference>> moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/common-interop.js").AsTask());

    /// <summary>
    /// Sets a checkbox tri-state
    /// </summary>
    /// <param name="checkbox">The <see cref="ElementReference"/> of the checkbox</param>
    /// <param name="state">The <see cref="CheckboxState"/> to set</param>
    /// <returns>A <see cref="ValueTask"/></returns>
    public async ValueTask SetCheckboxStateAsync(ElementReference checkbox, CheckboxState state)
    {
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("setCheckboxState", checkbox, state);
    }

    /// <summary>
    /// Visites the provided urls in a new window
    /// </summary>
    /// <param name="urls">The urls to generate the click for</param>
    /// <returns>A <see cref="ValueTask"/></returns>
    public async ValueTask VisitUrlsAsync(List<string> urls)
    {
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("visitUrls", urls);
    }

    /// <summary>
    /// Copies the specified text to the clipboard
    /// </summary>
    /// <param name="text">The text to copy</param>
    /// <returns>A <see cref="ValueTask"/></returns>
    public async ValueTask CopyToClipboardAsync(string text)
    {
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("copyToClipboard", text);
    }

    /// <summary>
    /// Downloads the specified content as a file
    /// </summary>
    /// <param name="fileName">The name of the file to download</param>
    /// <param name="contentType">The media type of the file</param>
    /// <param name="content">The content of the file</param>
    /// <returns>A <see cref="ValueTask"/></returns>
    public async ValueTask DownloadFileAsync(string fileName, string contentType, byte[] content)
    {
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("downloadFile", fileName, contentType, Convert.ToBase64String(content));
    }

    /// <summary>
    /// Moves the focus to the element with the specified id, if any
    /// </summary>
    /// <param name="id">The id of the element to focus</param>
    /// <returns>A <see cref="ValueTask"/></returns>
    public async ValueTask FocusAsync(string id)
    {
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("focusElement", id);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (moduleTask.IsValueCreated)
        {
            var module = await moduleTask.Value;
            await module.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}
