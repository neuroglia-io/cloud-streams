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

namespace CloudStreams.Broker.Application.Services.Delivery;

/// <summary>
/// Represents the service used to post cloud events, or batches of cloud events, to subscribers and dead-letter services (see #163)
/// </summary>
/// <param name="httpClient">The service used to perform HTTP requests</param>
public class SubscriberClient(HttpClient httpClient)
{

    /// <summary>
    /// Gets the service used to perform HTTP requests
    /// </summary>
    protected HttpClient HttpClient { get; } = httpClient;

    /// <summary>
    /// Posts the specified content to the specified service
    /// </summary>
    /// <param name="uri">The uri of the service to post the content to</param>
    /// <param name="content">The content to post</param>
    /// <param name="headers">The headers to send</param>
    /// <param name="description">A description of the service, used in error messages</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>, which may bound the duration of the request</param>
    /// <returns>The success status code the service responded with</returns>
    /// <exception cref="HttpRequestException">The service could not be reached, or responded with a non-success status code, which the exception carries</exception>
    public virtual async Task<HttpStatusCode> PostAsync(Uri uri, HttpContent content, IEnumerable<KeyValuePair<string, string>> headers, string description, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(headers);
        using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };
        foreach (var (name, value) in headers) request.Headers.TryAddWithoutValidation(name, value);
        using var response = await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode) return response.StatusCode;
        var reason = $"{description} responded with a non-success status code '{response.StatusCode}'";
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(responseContent)) reason = $"{reason}: {responseContent}";
        throw new HttpRequestException(reason, null, response.StatusCode);
    }

}
