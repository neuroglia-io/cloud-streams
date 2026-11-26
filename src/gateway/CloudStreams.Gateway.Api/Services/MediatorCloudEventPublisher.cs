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

using CloudStreams.Core.Api;
using CloudStreams.Core.Api.Configuration;
using CloudStreams.Core.Application.Services;
using CloudStreams.Gateway.Application.Commands.CloudEvents;
using CloudStreams.Gateway.Application.Services;
using System.Net;
using Neuroglia;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Eventing.CloudEvents;
using Neuroglia.Mediation;
using System.Security.Claims;

namespace CloudStreams.Gateway.Api.Services;

/// <summary>
/// Represents the <see cref="ICloudEventPublisher"/> that ingests cloud events the way the gateway's ingestion endpoint does: rate limits, admission and deduplication apply
/// </summary>
/// <param name="mediator">The service used to mediate calls</param>
/// <param name="authenticationOptions">The options used to configure authentication, which determine the sources publishers may publish cloud events of</param>
/// <param name="configuration">The service that holds the gateway's configuration</param>
public class MediatorCloudEventPublisher(IMediator mediator, CloudStreamsAuthenticationOptions authenticationOptions, IGatewayConfiguration configuration)
    : ICloudEventPublisher
{

    /// <inheritdoc/>
    public virtual Task<IOperationResult> PublishAsync(CloudEvent e, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(user);
        var publisher = user.Identity?.IsAuthenticated == true ? user.Identity.Name : null;
        var target = configuration.Gateway;
        if (target == null) return Task.FromResult<IOperationResult>(new OperationResult((int)HttpStatusCode.ServiceUnavailable, errors: [new Error(new Uri("https://cloud-streams.io/problems/gateway-unavailable"), "Service Unavailable", (int)HttpStatusCode.ServiceUnavailable, "The gateway is starting: its configuration has not been loaded yet. Please retry later")]));
        // the cloud event belongs to the gateway's namespace, which the user must be allowed to operate (see #215)
        if (authenticationOptions.Dashboard.GetAccessLevel(user, target.GetNamespace()) < CloudStreamsAccessLevel.Operate)
            return Task.FromResult<IOperationResult>(new OperationResult((int)HttpStatusCode.Forbidden, errors: [new Error(new Uri("https://cloud-streams.io/problems/forbidden"), "Forbidden", (int)HttpStatusCode.Forbidden, $"Publishing to the gateway of the namespace '{target.GetNamespace()}' requires the operator role in that namespace")]));
        return mediator.ExecuteAsync(new ConsumeEventCommand(e, target, user.GetAllowedSources(authenticationOptions.Ingestion), publisher), cancellationToken);
    }

}
