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
    protected ConsumeEventCommand() { this.CloudEvent = null!; }
    
    /// <summary>
    /// Initializes a new <see cref="ConsumeEventCommand"/>
    /// </summary>
    /// <param name="cloudEvent">The <see cref="Neuroglia.Eventing.CloudEvents.CloudEvent"/> to consume</param>
    /// <param name="allowedSources">The patterns of the sources the publisher may publish cloud events of, or null if it may publish cloud events of any source</param>
    public ConsumeEventCommand(CloudEvent cloudEvent, IReadOnlyCollection<string>? allowedSources = null)
    {
        this.CloudEvent = cloudEvent;
        this.AllowedSources = allowedSources;
    }

    /// <summary>
    /// Gets the <see cref="CloudEvent"/> to consume
    /// </summary>
    [Required]
    public CloudEvent CloudEvent { get; }

    /// <summary>
    /// Gets the patterns of the sources the publisher may publish cloud events of, or null if it may publish cloud events of any source
    /// </summary>
    public IReadOnlyCollection<string>? AllowedSources { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="ConsumeEventCommand"/>s
/// </summary>
/// <inheritdoc/>
public class ConsumeCloudEventCommandHandler(ICloudEventAdmissionControl eventAdmissionControl, IGatewayMetrics metrics, ICloudEventStore eventStore)
        : ICommandHandler<ConsumeEventCommand>
{

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
            var admissionResult = await eventAdmissionControl.EvaluateAsync(e, command.AllowedSources, cancellationToken).ConfigureAwait(false);
            if (admissionResult.Data == null || !admissionResult.IsSuccess())
            {
                outcome = GetOutcome(admissionResult.Status);
                return admissionResult;
            }
            try
            {
                await eventStore.AppendAsync(admissionResult.Data, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                metrics.IncrementTotalAppendFailures(e.Source);
                throw;
            }
            metrics.IncrementTotalIngestedEvents(e.Source);
            outcome = IngestionOutcome.Stored;
            // '202 Accepted' is reserved for events that have been accepted but ignored (see CloudEventAdmissionControl)
            return new OperationResult((int)HttpStatusCode.Created);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested) metrics.RecordIngestionDuration(outcome, Stopwatch.GetElapsedTime(startedAt));
        }
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
