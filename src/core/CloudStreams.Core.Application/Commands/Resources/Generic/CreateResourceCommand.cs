﻿// Copyright © 2024-Present The Cloud Streams Authors
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

namespace CloudStreams.Core.Application.Commands.Resources.Generic;

/// <summary>
/// Represents the command used to create a new <see cref="IResource"/>
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to create</typeparam>
/// <remarks>
/// Initializes a new <see cref="CreateResourceCommand{TResource}"/>
/// </remarks>
/// <param name="resource">The resource to create</param>
/// <param name="dryRun">A boolean indicating whether or not to persist the changes induces by the command</param>
public class CreateResourceCommand<TResource>(TResource resource, bool dryRun)
    : Command<TResource>
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Gets the resource to create
    /// </summary>
    public TResource Resource { get; } = resource ?? throw new ArgumentNullException(nameof(resource));

    /// <summary>
    /// Gets a boolean indicating whether or not to persist the changes induces by the command
    /// </summary>
    public bool DryRun { get; } = dryRun;

}

/// <summary>
/// Represents the service used to handle <see cref="CreateResourceCommand"/>s
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to create</typeparam>
/// <param name="repository">The service used to manage <see cref="IResource"/>s</param>
public class CreateResourceCommandHandler<TResource>(IResourceRepository repository)
    : ICommandHandler<CreateResourceCommand<TResource>, TResource>
    where TResource : class, IResource, new()
{

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<TResource>> HandleAsync(CreateResourceCommand<TResource> command, CancellationToken cancellationToken)
    {
        var resource = await repository.AddAsync(command.Resource, command.DryRun, cancellationToken);
        return new OperationResult<TResource>((int)HttpStatusCode.Created, resource);
    }

}
