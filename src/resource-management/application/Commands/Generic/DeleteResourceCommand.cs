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

namespace CloudStreams.ResourceManagement.Application.Commands.Generic;

/// <summary>
/// Represents the <see cref="ICommand"/> used to delete an existsing <see cref="IResource"/>
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to delete</typeparam>
public class DeleteResourceCommand<TResource>
    : ICommand
{

    /// <summary>
    /// Initializes a new <see cref="DeleteResourceCommand{TResource}"/>
    /// </summary>
    /// <param name="name">The name of the <see cref="IResource"/> to delete</param>
    /// <param name="namespace">The namespace of the <see cref="IResource"/> to delete</param>
    public DeleteResourceCommand(string name, string? @namespace = null)
    {
        this.Name = name;
        this.Namespace = @namespace;
    }

    /// <summary>
    /// Gets the name of the <see cref="IResource"/> to delete
    /// </summary>
    public virtual string Name { get; }

    /// <summary>
    /// Gets the namespace of the <see cref="IResource"/> to delete
    /// </summary>
    public virtual string? Namespace { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="DeleteResourceCommandHandler{TResource}"/>s
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to put</typeparam>
public class DeleteResourceCommandHandler<TResource>
    : ICommandHandler<DeleteResourceCommand<TResource>>
    where TResource : class, IResource, new()
{

    readonly IResourceRepository _ResourceRepository;

    /// <inheritdoc/>
    public DeleteResourceCommandHandler(IResourceRepository resourceRepository)
    {
        this._ResourceRepository = resourceRepository;
    }

    async Task<Response> MediatR.IRequestHandler<DeleteResourceCommand<TResource>, Response>.Handle(DeleteResourceCommand<TResource> command, CancellationToken cancellationToken)
    {
        await this._ResourceRepository.DeleteResourceAsync<TResource>(command.Name, command.Namespace, cancellationToken).ConfigureAwait(false);
        return this.Ok();
    }

}