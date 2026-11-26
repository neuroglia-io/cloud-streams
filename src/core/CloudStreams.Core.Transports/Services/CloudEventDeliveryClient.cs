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

using CloudStreams.Core.Resources;
using CloudStreams.Core.Transports.Authentication;
using Neuroglia.Eventing.CloudEvents;
using System.Net;

namespace CloudStreams.Core.Transports.Services;

/// <summary>
/// Represents where cloud events are delivered: an endpoint, or the name of a <see cref="Resources.Sink"/>
/// </summary>
/// <param name="Endpoint">The endpoint to deliver to, if any</param>
/// <param name="Sink">The name of the sink to deliver to, if any</param>
public sealed record DeliveryTarget(Endpoint? Endpoint, string? Sink)
{

    /// <summary>
    /// Gets the target of the specified subscriber
    /// </summary>
    public static DeliveryTarget Of(Subscriber subscriber) => new(subscriber.Endpoint, subscriber.Sink);

    /// <summary>
    /// Gets the target of the specified dead-letter policy
    /// </summary>
    public static DeliveryTarget Of(DeadLetterPolicy deadLetter) => new(deadLetter.Endpoint, deadLetter.Sink);

    /// <inheritdoc/>
    public override string ToString() => this.Sink != null ? $"sink '{this.Sink}'" : $"'{this.Endpoint}'";

}

/// <summary>
/// Represents the service used to deliver cloud events, or batches of cloud events, to subscribers and dead-letter services: to endpoints or sinks, over HTTP or
/// over the transport the scheme of their address selects, authenticated as their endpoint configures (see #163, #218)
/// </summary>
/// <param name="httpClient">The service used to perform HTTP requests</param>
/// <param name="sinks">The transports, other than HTTP, cloud events can be delivered over</param>
/// <param name="credentials">The service used to resolve the credentials of endpoints, if any</param>
/// <param name="endpoints">The service used to resolve the endpoints of sinks, if any</param>
/// <remarks>
/// Whatever the transport, a failed delivery is reported as an <see cref="HttpRequestException"/>: with the status code the subscriber responded with, if it refused the
/// cloud events, or without one if it could not be reached, or if its sink, its secret or its token could not be resolved, which retry policies always retry.
/// Retries, circuit breakers and dead letters therefore work the same with every transport
/// </remarks>
public class CloudEventDeliveryClient(HttpClient httpClient, IEnumerable<ICloudEventSink>? sinks = null, ICredentialResolver? credentials = null, IEndpointResolver? endpoints = null)
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
    /// Delivers the specified cloud event
    /// </summary>
    /// <param name="target">Where to deliver the cloud event</param>
    /// <param name="e">The cloud event to deliver</param>
    /// <param name="headers">The headers to send</param>
    /// <param name="description">A description of the recipient, used in error messages</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>, which may bound the duration of the delivery</param>
    /// <returns>The success status code the recipient responded with, or <see cref="HttpStatusCode.OK"/> if the transport has no status codes</returns>
    /// <exception cref="HttpRequestException">The recipient could not be reached, or refused the cloud event</exception>
    public virtual Task<HttpStatusCode> SendAsync(DeliveryTarget target, CloudEvent e, IEnumerable<KeyValuePair<string, string>> headers, string description, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(e);
        return this.DeliverAsync(target, [e], false, headers, description, cancellationToken);
    }

    /// <summary>
    /// Delivers the specified cloud events, as a batch
    /// </summary>
    /// <param name="target">Where to deliver the cloud events</param>
    /// <param name="events">The cloud events to deliver, in order</param>
    /// <param name="headers">The headers to send</param>
    /// <param name="description">A description of the recipient, used in error messages</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>, which may bound the duration of the delivery</param>
    /// <returns>The success status code the recipient responded with, or <see cref="HttpStatusCode.OK"/> if the transport has no status codes</returns>
    /// <exception cref="HttpRequestException">The recipient could not be reached, or refused the cloud events</exception>
    /// <remarks>Over HTTP, the batch is posted as <c>application/cloudevents-batch+json</c>. Over transports without batches, each cloud event is a message of its own</remarks>
    public virtual Task<HttpStatusCode> SendBatchAsync(DeliveryTarget target, IReadOnlyList<CloudEvent> events, IEnumerable<KeyValuePair<string, string>> headers, string description, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        return this.DeliverAsync(target, events, true, headers, description, cancellationToken);
    }

    /// <summary>
    /// Delivers the specified cloud events
    /// </summary>
    protected virtual async Task<HttpStatusCode> DeliverAsync(DeliveryTarget target, IReadOnlyList<CloudEvent> events, bool batch, IEnumerable<KeyValuePair<string, string>> headers, string description, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(headers);
        Endpoint endpoint;
        TransportCredentials? resolvedCredentials;
        try
        {
            endpoint = target.Endpoint ?? (target.Sink == null ? throw new InvalidOperationException($"{description} has neither an endpoint nor a sink")
                : endpoints == null ? throw new InvalidOperationException($"The sink '{target.Sink}' cannot be resolved here")
                : await endpoints.GetSinkEndpointAsync(target.Sink, cancellationToken).ConfigureAwait(false));
            resolvedCredentials = endpoint.Authentication == null ? null
                : credentials == null ? throw new InvalidOperationException("Authenticated endpoints cannot be delivered to here")
                : await credentials.ResolveAsync(endpoint.Authentication, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // a missing sink, secret or token may be fixed by an operator: the delivery fails, and is retried, rather than faulting the subscription at once
            throw new HttpRequestException($"{description} at {target} could not be delivered to: {ex.Message}", ex);
        }
        var uri = endpoint.Uri;
        if (!TransportAddress.IsTransport(uri))
        {
            using var content = batch ? events.ToBatchHttpContent() : events[0].ToHttpContent();
            using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };
            foreach (var (name, value) in headers) request.Headers.TryAddWithoutValidation(name, value);
            try { if (resolvedCredentials != null) await resolvedCredentials.AuthenticateAsync(request, cancellationToken).ConfigureAwait(false); }
            catch (Exception ex) when (ex is not OperationCanceledException) { throw new HttpRequestException($"{description} at {target} could not be authenticated to: {ex.Message}", ex); }
            return await this.SendAsync(request, description, cancellationToken).ConfigureAwait(false);
        }
        if (!TransportAddress.TryParse(uri, false, out var address, out var error)) throw new HttpRequestException($"{description} has an invalid address '{uri}': {error}");
        var sink = this.Sinks.FirstOrDefault(s => s.Schemes.Contains(address.Scheme)) ?? throw new HttpRequestException($"The '{address.Scheme}' transport, which {description.ToLowerInvariant()} at '{uri}' uses, is not supported here");
        try
        {
            await sink.SendAsync(address, resolvedCredentials, events, [.. headers], cancellationToken).ConfigureAwait(false);
            return HttpStatusCode.OK;
        }
        catch (Exception ex) when (ex is not HttpRequestException && ex is not OperationCanceledException)
        {
            throw new HttpRequestException($"{description} at '{uri}' could not be delivered to: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sends the specified HTTP request
    /// </summary>
    /// <param name="request">The request to send</param>
    /// <param name="description">A description of the recipient, used in error messages</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>, which may bound the duration of the request</param>
    /// <returns>The success status code the recipient responded with</returns>
    /// <exception cref="HttpRequestException">The recipient could not be reached, or responded with a non-success status code, which the exception carries</exception>
    protected virtual async Task<HttpStatusCode> SendAsync(HttpRequestMessage request, string description, CancellationToken cancellationToken)
    {
        using var response = await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode) return response.StatusCode;
        var reason = $"{description} responded with a non-success status code '{response.StatusCode}'";
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(responseContent)) reason = $"{reason}: {responseContent}";
        throw new HttpRequestException(reason, null, response.StatusCode);
    }

}
