using CloudStreams.Core.Infrastructure.Services;
using CloudStreams.Gateway.Services;
using Neuroglia.Mediation;
using System.ComponentModel.DataAnnotations;

namespace CloudStreams.Gateway.Commands.CloudEvents;

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
