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

using CloudStreams.Core.Data;
using CloudStreams.Core.Infrastructure;
using CloudStreams.Core.Infrastructure.Services;
using CloudStreams.Gateway.Application.Services;
using Hylo.Api.Application;
using System.ComponentModel.DataAnnotations;

namespace CloudStreams.Gateway.Application.Commands.CloudEvents;

/// <summary>
/// Represents the <see cref="ICommand"/> used to consume an incoming <see cref="Core.Data.CloudEvent"/>s
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
    /// Gets the <see cref="Core.Data.CloudEvent"/> to consume
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
    readonly IEventStoreProvider _EventStoreProvider;

    /// <inheritdoc/>
    public ConsumeCloudEventCommandHandler(ICloudEventAdmissionControl eventAdmissionControl, IGatewayMetrics metrics, IEventStoreProvider eventStoreProvider)
    {
        this._EventAdmissionControl = eventAdmissionControl;
        this._Metrics = metrics;
        this._EventStoreProvider = eventStoreProvider;
    }

    /// <inheritdoc/>
    public async Task<ApiResponse> Handle(ConsumeEventCommand command, CancellationToken cancellationToken)
    {
        var e = command.CloudEvent;
        var admissionResult = await this._EventAdmissionControl.EvaluateAsync(e, cancellationToken).ConfigureAwait(false);
        if (!admissionResult.IsSuccessStatusCode()) return new ApiResponse(admissionResult.Type!, admissionResult.Title!, admissionResult.Status, admissionResult.Detail, admissionResult.Instance, admissionResult.Errors?.ToDictionary(e => e.Key, e => e.Value), admissionResult.ExtensionData);
        await this._EventStoreProvider.GetEventStore().AppendAsync(admissionResult.Content!, cancellationToken).ConfigureAwait(false);
        this._Metrics.IncrementTotalIngestedEvents();
        return ApiResponse.Accepted();
    }

}
