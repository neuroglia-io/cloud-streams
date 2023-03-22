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

using CloudStreams.Core.Data.Models;
using CloudStreams.Core.Infrastructure.Services;
using Json.Schema.Generation;

namespace CloudStreams.ResourceManagement.Application.Commands.Generic;

/// <summary>
/// Represents the <see cref="ICommand"/> used to put a <see cref="IResource"/>
/// </summary>
public class PutResourceCommand<TResource>
    : ICommand<TResource>
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Initializes a new <see cref="PutResourceCommand{TResource}"/>
    /// </summary>
    /// <param name="resource">The resource to put</param>
    public PutResourceCommand(TResource resource)
    {
        this.Resource = resource;
    }

    /// <summary>
    /// Gets the resource to put
    /// </summary>
    [Required]
    public TResource Resource { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="PutResourceCommand{TResource}"/>s
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to put</typeparam>
public class PutResourceCommandHandler<TResource>
    : ICommandHandler<PutResourceCommand<TResource>, TResource>
    where TResource : class, IResource, new()
{

    readonly IResourceRepository _ResourceRepository;

    /// <inheritdoc/>
    public PutResourceCommandHandler(IResourceRepository resourceRepository)
    {
        this._ResourceRepository = resourceRepository;
    }

    async Task<Response<TResource>> MediatR.IRequestHandler<PutResourceCommand<TResource>, Response<TResource>>.Handle(PutResourceCommand<TResource> command, CancellationToken cancellationToken)
    {
        var resource = await this._ResourceRepository.UpdateResourceAsync(command.Resource, cancellationToken).ConfigureAwait(false);
        return this.Ok(resource);
    }

}
