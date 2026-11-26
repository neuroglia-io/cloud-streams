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

using CloudStreams.Core;
using CloudStreams.Core.Api;
using CloudStreams.Gateway.Application.Commands.CloudEvents;
using CloudStreams.Gateway.Api.Services;
using CloudStreams.Gateway.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Neuroglia.Mediation;
using Neuroglia.Serialization;
using System.Globalization;

namespace CloudStreams.Gateway.Controllers;

/// <summary>
/// Represents the API controller used to manage events
/// </summary>
/// <param name="mediator">The service used to mediate calls</param>
/// <param name="configuration">The service that holds the configuration of the gateway</param>
/// <param name="publishers">The service that decides whether a request may publish to a gateway</param>
/// <param name="serializer">The service used to deserialize cloud events</param>
[Route("api/events")]
public class EventsController(IMediator mediator, IGatewayConfiguration configuration, IPublisherAuthorizer publishers, IJsonSerializer serializer)
    : Controller
{

    /// <summary>
    /// Gets the type of the problem that describes a request whose content type is not supported
    /// </summary>
    public static readonly Uri UnsupportedMediaTypeProblemType = new("https://cloud-streams.io/problems/unsupported-media-type");

    /// <summary>
    /// Gets the type of the problem that describes a request that is too large
    /// </summary>
    public static readonly Uri PayloadTooLargeProblemType = new("https://cloud-streams.io/problems/payload-too-large");

    /// <summary>
    /// Gets the type of the problem that describes a request whose body is not a cloud event, or a batch of cloud events
    /// </summary>
    public static readonly Uri MalformedProblemType = new("https://cloud-streams.io/problems/malformed-cloud-event");

    /// <summary>
    /// Gets the type of the problem that describes a request made while the gateway's configuration is being loaded
    /// </summary>
    public static readonly Uri GatewayUnavailableProblemType = new("https://cloud-streams.io/problems/gateway-unavailable");

    /// <summary>
    /// Gets the type of the problem that describes a request made by an unknown publisher to a gateway that requires one
    /// </summary>
    public static readonly Uri UnauthorizedProblemType = new("https://cloud-streams.io/problems/unauthorized-publisher");

    /// <summary>
    /// Gets the type of the problem that describes a request made by a publisher that may not publish to the gateway
    /// </summary>
    public static readonly Uri ForbiddenProblemType = new("https://cloud-streams.io/problems/forbidden-publisher");

    /// <summary>
    /// Publishes the cloud event, or the batch of cloud events, described by the request, in any content mode of the CloudEvents HTTP binding:
    /// <list type="bullet">
    /// <item><description>structured: the body is a cloud event, of type <c>application/cloudevents+json</c>;</description></item>
    /// <item><description>batched: the body is an array of cloud events, of type <c>application/cloudevents-batch+json</c>, each admitted individually;</description></item>
    /// <item><description>binary: the body is the cloud event's data, whose context attributes are carried by <c>ce-</c> prefixed headers.</description></item>
    /// </list>
    /// The cloud events belong to the namespace of the gateway (see #215)
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/>: <c>204 No Content</c> once a cloud event has been accepted, or <c>200 OK</c> with the result of each cloud event of a batch</returns>
    [HttpPost("pub")]
    [Authorize(Policy = CloudStreamsAuthenticationDefaults.IngestionPolicy)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(IEnumerable<CloudEventIngestionResult>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.ServiceUnavailable)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.Forbidden)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.RequestEntityTooLarge)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.UnsupportedMediaType)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.TooManyRequests)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public virtual async Task<IActionResult> PublishCloudEvent(CancellationToken cancellationToken = default)
    {
        // until the gateway's configuration has been loaded, its policies are unknown: admitting events would bypass authorization and validation (see #142)
        var target = configuration.Gateway;
        if (target == null)
        {
            this.SetRetryAfter(TimeSpan.FromSeconds(5));
            return ProblemResult(GatewayUnavailableProblemType, HttpStatusCode.ServiceUnavailable, "The gateway is starting: its configuration has not been loaded yet. Please retry later");
        }
        var authorization = await publishers.AuthorizeAsync(this.HttpContext, target, cancellationToken).ConfigureAwait(false);
        if (!authorization.Allowed) return ProblemResult(authorization.Status == (int)HttpStatusCode.Unauthorized ? UnauthorizedProblemType : ForbiddenProblemType, (HttpStatusCode)authorization.Status, authorization.Reason ?? "The publisher may not publish to the gateway");
        var ingestion = target.Spec.Ingestion;
        var mode = CloudEventHttpBinding.GetContentMode(this.Request.ContentType, this.Request.Headers.ContainsKey(CloudEventHttpBinding.SpecVersionHeader));
        if (mode == CloudEventContentMode.Unsupported) return ProblemResult(UnsupportedMediaTypeProblemType, HttpStatusCode.UnsupportedMediaType,
            $"Cloud events must be published as '{CloudEventContentType.Json}', as '{CloudEventHttpBinding.BatchContentType}', or in the binary content mode, which requires the '{CloudEventHttpBinding.SpecVersionHeader}' header");
        var body = await this.ReadBodyAsync(ingestion?.MaxRequestBodySize, cancellationToken).ConfigureAwait(false);
        if (body == null) return ProblemResult(PayloadTooLargeProblemType, HttpStatusCode.RequestEntityTooLarge, $"The request's body must not exceed {ingestion!.MaxRequestBodySize} bytes");
        if (mode == CloudEventContentMode.Batch) return await this.PublishBatchAsync(body, target, authorization, cancellationToken).ConfigureAwait(false);
        CloudEvent e;
        try
        {
            e = mode == CloudEventContentMode.Structured
                ? CloudEventHttpBinding.ReadStructured(body, serializer)
                : CloudEventHttpBinding.ReadBinary(this.Request.ContentType, this.Request.Headers.Select(h => new KeyValuePair<string, string?>(h.Key, h.Value.ToString())), body, serializer);
        }
        catch (FormatException ex)
        {
            return ProblemResult(MalformedProblemType, HttpStatusCode.BadRequest, ex.Message);
        }
        var result = await this.IngestAsync(e, target, authorization, cancellationToken).ConfigureAwait(false);
        this.SetRetryAfter(GetRetryAfter(result));
        // the first error describes why the event has been refused: publishers get its detail rather than a generic validation problem
        if (result.Status is < 200 or >= 300) return this.Problem(result);
        // the gateway always answers '204 No Content' on success, so that a subscription bridging two instances never mistakes it for a deferred acknowledgement.
        // Whether the event has been stored, ignored by the split-horizon policy or deduplicated is conveyed by a header (see #179)
        this.Response.Headers[CloudStreamsHeaders.Outcome] = GetOutcome(result.Status);
        return this.NoContent();
    }

    /// <summary>
    /// Publishes the specified batch of cloud events, one at a time and in order. Each cloud event is admitted individually, so that one that is refused does not
    /// prevent the others from being ingested
    /// </summary>
    /// <param name="body">The request's body</param>
    /// <param name="gateway">The gateway the cloud events are published to</param>
    /// <param name="authorization">The authorization of the publisher</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/> that describes the result of each cloud event</returns>
    protected virtual async Task<IActionResult> PublishBatchAsync(byte[] body, Core.Resources.Gateway gateway, PublisherAuthorization authorization, CancellationToken cancellationToken)
    {
        var ingestion = gateway.Spec.Ingestion;
        IReadOnlyList<System.Text.Json.Nodes.JsonNode?> batch;
        try { batch = CloudEventHttpBinding.ReadBatch(body); }
        catch (FormatException ex) { return ProblemResult(MalformedProblemType, HttpStatusCode.BadRequest, ex.Message); }
        if (ingestion?.MaxBatchSize is int maxBatchSize && batch.Count > maxBatchSize) return ProblemResult(PayloadTooLargeProblemType, HttpStatusCode.RequestEntityTooLarge, $"A batch must not contain more than {maxBatchSize} cloud events");
        var results = new List<CloudEventIngestionResult>(batch.Count);
        TimeSpan? retryAfter = null;
        for (var index = 0; index < batch.Count; index++)
        {
            CloudEvent e;
            try { e = CloudEventHttpBinding.ReadEvent(batch[index], serializer, $"The cloud event at index {index}"); }
            catch (FormatException ex)
            {
                results.Add(new() { Index = index, Status = (int)HttpStatusCode.BadRequest, Problem = new(MalformedProblemType, "Bad Request", (int)HttpStatusCode.BadRequest, ex.Message) });
                continue;
            }
            var result = await this.IngestAsync(e, gateway, authorization, cancellationToken).ConfigureAwait(false);
            if (GetRetryAfter(result) is TimeSpan delay && (retryAfter == null || delay > retryAfter)) retryAfter = delay;
            var succeeded = result.Status is >= 200 and < 300;
            results.Add(new()
            {
                Index = index,
                Id = e.Id,
                Source = e.Source?.OriginalString,
                Status = result.Status,
                Outcome = succeeded ? GetOutcome(result.Status) : null,
                Problem = succeeded ? null : ToProblem(result)
            });
        }
        this.SetRetryAfter(retryAfter);
        return this.Ok(results);
    }

    /// <summary>
    /// Ingests the specified cloud event
    /// </summary>
    /// <param name="e">The cloud event to ingest</param>
    /// <param name="gateway">The gateway the cloud event is published to</param>
    /// <param name="authorization">The authorization of the publisher</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The result of the ingestion</returns>
    protected virtual Task<IOperationResult> IngestAsync(CloudEvent e, Core.Resources.Gateway gateway, PublisherAuthorization authorization, CancellationToken cancellationToken)
    {
        return mediator.ExecuteAsync(new ConsumeEventCommand(e, gateway, authorization.AllowedSources, authorization.Publisher), cancellationToken);
    }

    /// <summary>
    /// Reads the request's body, unless it exceeds the specified size
    /// </summary>
    /// <param name="maxSize">The maximum size, in bytes, of the request's body, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The request's body, or null if it exceeds the specified size</returns>
    protected virtual async Task<byte[]?> ReadBodyAsync(long? maxSize, CancellationToken cancellationToken)
    {
        if (maxSize.HasValue)
        {
            if (this.Request.ContentLength > maxSize) return null;
            // the server stops reading as soon as the limit is exceeded, rather than once the whole body has been buffered
            var feature = this.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (feature != null && !feature.IsReadOnly) feature.MaxRequestBodySize = maxSize;
        }
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        try
        {
            int read;
            while ((read = await this.Request.Body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (maxSize.HasValue && buffer.Length + read > maxSize) return null;
                buffer.Write(chunk, 0, read);
            }
        }
        catch (Microsoft.AspNetCore.Http.BadHttpRequestException ex) when (ex.StatusCode == (int)HttpStatusCode.RequestEntityTooLarge)
        {
            return null;
        }
        return buffer.ToArray();
    }

    /// <summary>
    /// Sets the <c>Retry-After</c> header, if a delay is specified
    /// </summary>
    /// <param name="retryAfter">The amount of time after which to retry, if any</param>
    protected virtual void SetRetryAfter(TimeSpan? retryAfter)
    {
        if (retryAfter.HasValue) this.Response.Headers.RetryAfter = Math.Max(1, (long)Math.Ceiling(retryAfter.Value.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets the amount of time after which a cloud event refused with the specified result may be published again, if any
    /// </summary>
    static TimeSpan? GetRetryAfter(IOperationResult result) => result switch
    {
        IngestionResult { RetryAfter: TimeSpan retryAfter } => retryAfter,
        // the gateway is starting: its configuration should be loaded shortly
        { Status: (int)HttpStatusCode.ServiceUnavailable } => TimeSpan.FromSeconds(5),
        _ => null
    };

    /// <summary>
    /// Gets the outcome of a cloud event that has been accepted with the specified status
    /// </summary>
    static string GetOutcome(int status) => status switch
    {
        (int)HttpStatusCode.Accepted => CloudStreamsHeaders.Outcomes.Ignored,
        (int)HttpStatusCode.OK => CloudStreamsHeaders.Outcomes.Duplicate,
        _ => CloudStreamsHeaders.Outcomes.Stored
    };

    /// <summary>
    /// Describes the specified failed result with problem details, which are those of its first error, if any
    /// </summary>
    static Neuroglia.ProblemDetails ToProblem(IOperationResult result)
    {
        var error = result.Errors?.FirstOrDefault();
        return error == null
            ? new(new Uri($"https://httpstatuses.io/{result.Status}"), ReasonPhrases.GetReasonPhrase(result.Status), result.Status)
            : new(error.Type, error.Title, error.Status, error.Detail, error.Instance, error.Errors?.ToDictionary(e => e.Key, e => e.Value));
    }

    /// <summary>
    /// Creates a new <see cref="IActionResult"/> that describes the specified problem
    /// </summary>
    static ObjectResult ProblemResult(Uri type, HttpStatusCode status, string detail) => new(new Neuroglia.ProblemDetails(type, ReasonPhrases.GetReasonPhrase((int)status), (int)status, detail))
    {
        StatusCode = (int)status,
        ContentTypes = { "application/problem+json" }
    };

}
