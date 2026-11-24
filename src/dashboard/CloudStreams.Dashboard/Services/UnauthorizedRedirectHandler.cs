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

namespace CloudStreams.Dashboard.Services;

/// <summary>
/// Represents a <see cref="DelegatingHandler"/> that sends users back to sign in when the server reports that their session has expired
/// </summary>
/// <remarks>
/// When the dashboard requires users to sign in, the server answers API calls made with an expired session with <c>401 Unauthorized</c>. Rather than letting every
/// call fail silently, the user is sent to the sign-in endpoint, which brings them back to the page they were on (see #197)
/// </remarks>
/// <param name="navigationManager">The service used to manage navigation</param>
public class UnauthorizedRedirectHandler(NavigationManager navigationManager)
    : DelegatingHandler
{

    int _redirecting;

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && Interlocked.Exchange(ref this._redirecting, 1) == 0)
        {
            var returnUrl = "/" + navigationManager.ToBaseRelativePath(navigationManager.Uri);
            navigationManager.NavigateTo($"auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}", forceLoad: true);
        }
        return response;
    }

}
