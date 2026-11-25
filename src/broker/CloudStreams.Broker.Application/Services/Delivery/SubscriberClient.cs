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

using CloudStreams.Core.Transports.Services;

namespace CloudStreams.Broker.Application.Services.Delivery;

/// <summary>
/// Represents the service used to deliver cloud events, or batches of cloud events, to subscribers and dead-letter services, over HTTP or over the transport the scheme
/// of their address selects (see #163, #218)
/// </summary>
/// <param name="httpClient">The service used to perform HTTP requests</param>
/// <param name="sinks">The transports, other than HTTP, cloud events can be delivered over</param>
/// <remarks>
/// Whatever the transport, a failed delivery is reported as an <see cref="HttpRequestException"/>: with the status code the subscriber responded with, if it refused the
/// cloud events, or without one if it could not be reached, which retry policies always retry. Retries, circuit breakers and dead letters therefore work the same
/// with every transport
/// </remarks>
public class SubscriberClient(HttpClient httpClient, IEnumerable<ICloudEventSink>? sinks = null)
{

    /// <summary>
    /// Gets the service used to perform HTTP requests
    /// </summary>
    protected HttpClient HttpClient { get; } = httpClient;

    /// <summary>
    /// Gets the transports, other than HTTP, cloud events can be delivered over
    /// </summary>
    protected IReadOnlyList<ICloudEventSink> Sinks { get; } = sinks?.ToList() ?? [];

    /// <summary>
    /// Delivers the specified cloud event to the specified address
    /// </summary>
    /// <param name="uri">The address to deliver the cloud event to</param>
    /// <param name="e">The cloud event to deliver</param>
    /// <param name="headers">The headers to send</param>
    /// <param name="description">A description of the recipient, used in error messages</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>, which may bound the duration of the delivery</param>
    /// <returns>The success status code the recipient responded with, or <see cref="HttpStatusCode.OK"/> if the transport has no status codes</returns>
    /// <exception cref="HttpRequestException">The recipient could not be reached, or refused the cloud event</exception>
    public virtual Task<HttpStatusCode> SendAsync(Uri uri, CloudEvent e, IEnumerable<KeyValuePair<string, string>> headers, string description, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(e);
        return this.DeliverAsync(uri, [e], false, headers, description, cancellationToken);
    }

    /// <summary>
    /// Delivers the specified cloud events, as a batch, to the specified address
    /// </summary>
    /// <param name="uri">The address to deliver the cloud events to</param>
    /// <param name="events">The cloud events to deliver, in order</param>
    /// <param name="headers">The headers to send</param>
    /// <param name="description">A description of the recipient, used in error messages</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>, which may bound the duration of the delivery</param>
    /// <returns>The success status code the recipient responded with, or <see cref="HttpStatusCode.OK"/> if the transport has no status codes</returns>
    /// <exception cref="HttpRequestException">The recipient could not be reached, or refused the cloud events</exception>
    /// <remarks>Over HTTP, the batch is posted as <c>application/cloudevents-batch+json</c>. Over transports without batches, each cloud event is a message of its own</remarks>
    public virtual Task<HttpStatusCode> SendBatchAsync(Uri uri, IReadOnlyList<CloudEvent> events, IEnumerable<KeyValuePair<string, string>> headers, string description, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        return this.DeliverAsync(uri, events, true, headers, description, cancellationToken);
    }

    /// <summary>
    /// Delivers the specified cloud events to the specified address
    /// </summary>
    /// <param name="uri">The address to deliver the cloud events to</param>
    /// <param name="events">The cloud events to deliver, in order</param>
    /// <param name="batch">A boolean indicating whether the cloud events are delivered as a batch, over HTTP</param>
    /// <param name="headers">The headers to send</param>
    /// <param name="description">A description of the recipient, used in error messages</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>, which may bound the duration of the delivery</param>
    /// <returns>The success status code the recipient responded with</returns>
    protected virtual async Task<HttpStatusCode> DeliverAsync(Uri uri, IReadOnlyList<CloudEvent> events, bool batch, IEnumerable<KeyValuePair<string, string>> headers, string description, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(headers);
        if (!TransportAddress.IsTransport(uri))
        {
            using var content = batch ? events.ToBatchHttpContent() : events[0].ToHttpContent();
            return await this.PostAsync(uri, content, headers, description, cancellationToken).ConfigureAwait(false);
        }
        if (!TransportAddress.TryParse(uri, false, out var address, out var error)) throw new HttpRequestException($"{description} has an invalid address '{uri}': {error}");
        var sink = this.Sinks.FirstOrDefault(s => s.Schemes.Contains(address.Scheme)) ?? throw new HttpRequestException($"The broker does not support the '{address.Scheme}' transport, which {description.ToLowerInvariant()} at '{uri}' uses");
        try
        {
            await sink.SendAsync(address, events, [.. headers], cancellationToken).ConfigureAwait(false);
            return HttpStatusCode.OK;
        }
        catch (Exception ex) when (ex is not HttpRequestException && ex is not OperationCanceledException)
        {
            throw new HttpRequestException($"{description} at '{uri}' could not be delivered to: {ex.Message}", ex);
        }
    }

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
