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

using CloudStreams.Core.Application.Services;
using CloudStreams.Gateway.Application.Services;
using Neuroglia.Mediation;
using System.ComponentModel.DataAnnotations;

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
    public ConsumeEventCommand(CloudEvent cloudEvent)
    {
        this.CloudEvent = cloudEvent;
    }
    
    /// <summary>
    /// Gets the <see cref="CloudEvent"/> to consume
    /// </summary>
    [Required]
    public CloudEvent CloudEvent { get; }

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
        var e = command.CloudEvent;
        var admissionResult = await eventAdmissionControl.EvaluateAsync(e, cancellationToken).ConfigureAwait(false);
        if (admissionResult.Data == null || !admissionResult.IsSuccess()) return admissionResult;
        await eventStore.AppendAsync(admissionResult.Data, cancellationToken).ConfigureAwait(false);
        metrics.IncrementTotalIngestedEvents();
        return new OperationResult((int)HttpStatusCode.Accepted);
    }

}
