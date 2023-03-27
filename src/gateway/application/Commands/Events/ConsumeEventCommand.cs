// Copyright © 2023-Present The Cloud Streams Authors
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
    [Required]
    public CloudEvent CloudEvent { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="ConsumeEventCommand"/>s
/// </summary>
public class ConsumeCloudEventCommandHandler
    : ICommandHandler<ConsumeEventCommand>
{

    readonly ICloudEventAdmissionControl _EventAdmissionControl;
    readonly IGatewayMetrics _Metrics;
    readonly ICloudEventStore _EventStore;

    /// <inheritdoc/>
    public ConsumeCloudEventCommandHandler(ICloudEventAdmissionControl eventAdmissionControl, IGatewayMetrics metrics, ICloudEventStore eventStore)
    {
        this._EventAdmissionControl = eventAdmissionControl;
        this._Metrics = metrics;
        this._EventStore = eventStore;
    }

    /// <inheritdoc/>
    public async Task<Response> Handle(ConsumeEventCommand command, CancellationToken cancellationToken)
    {
        var e = command.CloudEvent;
        var admissionResult = await this._EventAdmissionControl.EvaluateAsync(e, cancellationToken).ConfigureAwait(false);
        if (!admissionResult.IsSuccessStatusCode()) return admissionResult;
        await this._EventStore.AppendAsync(admissionResult.Content!, cancellationToken).ConfigureAwait(false);
        this._Metrics.IncrementTotalIngestedEvents();
        return this.Accepted();
    }

}
