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
/// Represents the <see cref="ICommand"/> used to replace an existing <see cref="IResource"/>
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to replace</typeparam>
/// <remarks>
/// Initializes a new <see cref="ReplaceResourceCommand{TResource}"/>
/// </remarks>
/// <param name="resource">The updated <see cref="IResource"/> to replace</param>
/// <param name="dryRun">A boolean indicating whether or not to persist changes</param>
public class ReplaceResourceCommand<TResource>(TResource resource, bool dryRun)
    : Command<TResource>
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Gets the updated <see cref="IResource"/> to replace
    /// </summary>
    public TResource Resource { get; } = resource;

    /// <summary>
    /// Gets a boolean indicating whether or not to persist changes
    /// </summary>
    public bool DryRun { get; } = dryRun;

}


/// <summary>
/// Represents the service used to handle <see cref="ReplaceResourceCommand"/>s
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to replace</typeparam>
/// <param name="repository">The service used to manage <see cref="IResource"/>s</param>
public class ReplaceResourceCommandHandler<TResource>(IResourceRepository repository)
    : ICommandHandler<ReplaceResourceCommand<TResource>, TResource>
    where TResource : class, IResource, new()
{

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<TResource>> HandleAsync(ReplaceResourceCommand<TResource> command, CancellationToken cancellationToken)
    {
        var resource = await repository.ReplaceAsync(command.Resource, command.DryRun, cancellationToken).ConfigureAwait(false);
        return new OperationResult<TResource>((int)HttpStatusCode.OK, resource);
    }

}