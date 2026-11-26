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
using CloudStreams.Core.Application.Services;
using CloudStreams.Gateway.Application.Services;
using Microsoft.Extensions.Logging;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Mediation;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace CloudStreams.Gateway.Application.Commands.CloudEvents;

/// <summary>
/// Represents the <see cref="ICommand"/> used to consume an incoming <see cref="Neuroglia.Eventing.CloudEvents.CloudEvent"/>s
/// </summary>
public class ConsumeEventCommand
    : Command
{

    /// <summary>
    /// Initializes a new <see cref="ConsumeEventCommand"/>
    /// </summary>
    protected ConsumeEventCommand() { this.CloudEvent = null!; this.Gateway = null!; }

    /// <summary>
    /// Initializes a new <see cref="ConsumeEventCommand"/>
    /// </summary>
    /// <param name="cloudEvent">The <see cref="Neuroglia.Eventing.CloudEvents.CloudEvent"/> to consume</param>
    /// <param name="gateway">The gateway the cloud event is published to, whose policies apply, and whose namespace the cloud event belongs to</param>
    /// <param name="allowedSources">The patterns of the sources the publisher may publish cloud events of, or null if it may publish cloud events of any source</param>
    /// <param name="publisher">The name the publisher authenticated with, if any, which partitions the rate limits that apply to publishers</param>
    public ConsumeEventCommand(CloudEvent cloudEvent, Core.Resources.Gateway gateway, IReadOnlyCollection<string>? allowedSources = null, string? publisher = null)
    {
        this.CloudEvent = cloudEvent;
        this.Gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        this.AllowedSources = allowedSources;
        this.Publisher = publisher;
    }

    /// <summary>
    /// Gets the <see cref="CloudEvent"/> to consume
    /// </summary>
    [Required]
    public CloudEvent CloudEvent { get; }

    /// <summary>
    /// Gets the gateway the cloud event is published to
    /// </summary>
    public Core.Resources.Gateway Gateway { get; }

    /// <summary>
    /// Gets the patterns of the sources the publisher may publish cloud events of, or null if it may publish cloud events of any source
    /// </summary>
    public IReadOnlyCollection<string>? AllowedSources { get; }

    /// <summary>
    /// Gets the name the publisher authenticated with, if any
    /// </summary>
    public string? Publisher { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="ConsumeEventCommand"/>s
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="eventAdmissionControl">The service used to admit cloud events</param>
/// <param name="metrics">The service used to record the gateway's metrics</param>
/// <param name="eventStore">The service used to store cloud events</param>
/// <param name="rateLimiter">The service used to limit the rate at which cloud events are published</param>
/// <param name="deduplicator">The service used to deduplicate cloud events</param>
/// <remarks>
/// A cloud event is first counted against the gateway's rate limits, then admitted, then deduplicated, and only then stored: a refused cloud event therefore counts
/// against the limits, but is never remembered as stored (see #209 and #210)
/// </remarks>
public class ConsumeCloudEventCommandHandler(ILogger<ConsumeCloudEventCommandHandler> logger, ICloudEventAdmissionControl eventAdmissionControl, IGatewayMetrics metrics, ICloudEventStore eventStore,
    ICloudEventRateLimiter rateLimiter, ICloudEventDeduplicator deduplicator)
        : ICommandHandler<ConsumeEventCommand>
{

    /// <summary>
    /// Gets the type of the problem that describes a cloud event refused because it exceeds a rate limit
    /// </summary>
    public static readonly Uri RateLimitedProblemType = new("https://cloud-streams.io/problems/rate-limited");

    /// <summary>
    /// Gets the type of the problem that describes a cloud event published again while it is still being ingested
    /// </summary>
    public static readonly Uri IngestionInProgressProblemType = new("https://cloud-streams.io/problems/ingestion-in-progress");

    /// <summary>
    /// Gets the amount of time after which a cloud event that is still being ingested may be published again
    /// </summary>
    public static readonly TimeSpan IngestionInProgressRetryAfter = TimeSpan.FromSeconds(1);

    /// <inheritdoc/>
    public async Task<IOperationResult> HandleAsync(ConsumeEventCommand command, CancellationToken cancellationToken)
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity("ConsumeEvent");
        activity?.SetTag("event.id", command.CloudEvent.Id);
        activity?.SetTag("event.source", command.CloudEvent.Source.ToString());
        activity?.SetTag("event.type", command.CloudEvent.Type);
        if(!string.IsNullOrWhiteSpace(command.CloudEvent.Subject)) activity?.SetTag("event.subject", command.CloudEvent.Subject);
        var e = command.CloudEvent;
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = IngestionOutcome.Failed;
        try
        {
            var gateway = command.Gateway;
            var scope = gateway?.GetQualifiedName();
            var ingestion = gateway?.Spec.Ingestion;
            if (scope != null && ingestion?.RateLimits is { Count: > 0 } rateLimits)
            {
                var decision = await rateLimiter.EvaluateAsync(scope, e, command.Publisher, rateLimits, cancellationToken).ConfigureAwait(false);
                if (!decision.Allowed)
                {
                    metrics.IncrementTotalRejectedEvents(RejectionReason.RateLimit);
                    outcome = IngestionOutcome.Rejected;
                    return new IngestionResult((int)HttpStatusCode.TooManyRequests, decision.RetryAfter, new Error(RateLimitedProblemType, "Too Many Requests", (int)HttpStatusCode.TooManyRequests,
                        $"The cloud event exceeds the gateway's rate limits. Retry in {Math.Ceiling((decision.RetryAfter ?? TimeSpan.Zero).TotalSeconds)} seconds"));
                }
            }
            var admissionResult = await eventAdmissionControl.EvaluateAsync(e, gateway, command.AllowedSources, cancellationToken).ConfigureAwait(false);
            if (admissionResult.Data == null || !admissionResult.IsSuccess())
            {
                outcome = GetOutcome(admissionResult.Status);
                return admissionResult;
            }
            var deduplication = scope == null ? null : ingestion?.Deduplication;
            if (deduplication != null)
            {
                switch (await deduplicator.TryReserveAsync(scope!, e, cancellationToken).ConfigureAwait(false))
                {
                    case DeduplicationOutcome.Duplicate:
                        metrics.IncrementTotalDuplicateEvents(e.Source);
                        outcome = IngestionOutcome.Duplicate;
                        // a duplicate is answered as a success, as the publisher's retry would have been if the first attempt had failed
                        return new IngestionResult((int)HttpStatusCode.OK);
                    case DeduplicationOutcome.InFlight:
                        return new IngestionResult((int)HttpStatusCode.ServiceUnavailable, IngestionInProgressRetryAfter, new Error(IngestionInProgressProblemType, "Service Unavailable", (int)HttpStatusCode.ServiceUnavailable,
                            $"The cloud event with source '{e.Source.OriginalString}' and id '{e.Id}' is being ingested. Retry to find out whether it has been stored"));
                }
            }
            try
            {
                await eventStore.AppendAsync(admissionResult.Data, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                if (!cancellationToken.IsCancellationRequested) metrics.IncrementTotalAppendFailures(e.Source);
                // the cloud event has not been stored: it must not be mistaken for a duplicate when it is published again
                if (deduplication != null) await this.ReleaseAsync(scope!, e).ConfigureAwait(false);
                throw;
            }
            metrics.IncrementTotalIngestedEvents(e.Source);
            outcome = IngestionOutcome.Stored;
            if (deduplication != null) await this.ConfirmAsync(scope!, e, deduplication.Window.ToTimeSpan()).ConfigureAwait(false);
            // '202 Accepted' is reserved for events that have been accepted but ignored (see CloudEventAdmissionControl)
            return new OperationResult((int)HttpStatusCode.Created);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested) metrics.RecordIngestionDuration(outcome, Stopwatch.GetElapsedTime(startedAt));
        }
    }

    /// <summary>
    /// Confirms that the specified cloud event has been stored. The cloud event has been stored already: failing to remember it only lets a duplicate through
    /// </summary>
    async Task ConfirmAsync(string scope, CloudEvent e, TimeSpan window)
    {
        try { await deduplicator.ConfirmAsync(scope, e, window, CancellationToken.None).ConfigureAwait(false); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to remember the cloud event with source '{source}' and id '{id}': a duplicate published within the deduplication window would be stored again", e.Source, e.Id); }
    }

    /// <summary>
    /// Releases the reservation of the specified cloud event, which failed to be stored. Failing to do so delays its retries until the reservation expires
    /// </summary>
    async Task ReleaseAsync(string scope, CloudEvent e)
    {
        try { await deduplicator.ReleaseAsync(scope, e, CancellationToken.None).ConfigureAwait(false); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to release the cloud event with source '{source}' and id '{id}': it will be refused as in progress until its reservation expires", e.Source, e.Id); }
    }

    /// <summary>
    /// Gets the outcome of an ingestion the admission control did not admit
    /// </summary>
    /// <param name="status">The status code of the admission</param>
    /// <returns>The outcome of the ingestion. See <see cref="IngestionOutcome"/></returns>
    static string GetOutcome(int status) => status switch
    {
        (int)HttpStatusCode.Accepted => IngestionOutcome.Ignored,
        (int)HttpStatusCode.BadRequest => IngestionOutcome.Invalid,
        (int)HttpStatusCode.Unauthorized or (int)HttpStatusCode.Forbidden or (int)HttpStatusCode.Conflict => IngestionOutcome.Rejected,
        _ => IngestionOutcome.Failed
    };

}
