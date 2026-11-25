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
using Neuroglia;
using Neuroglia.Eventing.CloudEvents;
using Neuroglia.Mediation;
using System.Security.Claims;

namespace CloudStreams.Gateway.Api.Services;

/// <summary>
/// Represents the <see cref="ICloudEventPublisher"/> that ingests cloud events the way the gateway's ingestion endpoint does: rate limits, admission and deduplication apply
/// </summary>
/// <param name="mediator">The service used to mediate calls</param>
/// <param name="authenticationOptions">The options used to configure authentication, which determine the sources publishers may publish cloud events of</param>
public class MediatorCloudEventPublisher(IMediator mediator, CloudStreamsAuthenticationOptions authenticationOptions)
    : ICloudEventPublisher
{

    /// <inheritdoc/>
    public virtual Task<IOperationResult> PublishAsync(CloudEvent e, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(user);
        var publisher = user.Identity?.IsAuthenticated == true ? user.Identity.Name : null;
        return mediator.ExecuteAsync(new ConsumeEventCommand(e, user.GetAllowedSources(authenticationOptions.Ingestion), publisher), cancellationToken);
    }

}
