namespace CloudStreams.Gateway.Application.Commands.CloudEvents;

/// <summary>
/// Represents the <see cref="ICommand"/> used to consume an incoming <see cref="Core.Data.Models.CloudEvent"/>s
/// </summary>
public class ConsumeEventCommand
    : ICommand
{

    /// <summary>
    /// Initializes a new <see cref="ConsumeEventCommand"/>
    /// </summary>
    /// <param name="cloudEvent">The <see cref="CloudEvent"/> to consume</param>
    public ConsumeEventCommand(CloudEvent cloudEvent)
    {
        this.CloudEvent = cloudEvent;
    }

    /// <summary>
    /// Gets the <see cref="Core.Data.Models.CloudEvent"/> to consume
    /// </summary>
    public CloudEvent CloudEvent { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="ConsumeEventCommand"/>s
/// </summary>
public class ConsumeCloudEventCommandHandler
    : ICommandHandler<ConsumeEventCommand>
{

    readonly ICloudEventAdmissionControl _EventAdmissionControl;
    readonly ICloudEventStore _EventStore;

    /// <inheritdoc/>
    public ConsumeCloudEventCommandHandler(ICloudEventAdmissionControl eventAdmissionControl, ICloudEventStore eventStore)
    {
        this._EventAdmissionControl = eventAdmissionControl;
        this._EventStore = eventStore;
    }

    /// <inheritdoc/>
    public async Task<Response> Handle(ConsumeEventCommand command, CancellationToken cancellationToken)
    {
        var e = command.CloudEvent;
        var admissionResult = await this._EventAdmissionControl.EvaluateAsync(e, cancellationToken).ConfigureAwait(false);
        if (!admissionResult.IsSuccessStatusCode()) return admissionResult;
        if(!e.Time.HasValue) e.Time = DateTimeOffset.Now;
        await this._EventStore.AppendAsync(e, cancellationToken).ConfigureAwait(false);
        return this.Accepted();
    }

}
