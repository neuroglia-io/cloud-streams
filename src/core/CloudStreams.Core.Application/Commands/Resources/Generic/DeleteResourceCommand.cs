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

namespace CloudStreams.Core.Application.Commands.Resources.Generic;

/// <summary>
/// Represents the <see cref="ICommand"/> used to delete an existing <see cref="IResource"/>
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to create</typeparam>
public class DeleteResourceCommand<TResource>
    : Command<TResource>
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Initializes a new <see cref="DeleteResourceCommand{TResource}"/>
    /// </summary>
    /// <param name="name">The name of the <see cref="IResource"/> to delete</param>
    /// <param name="namespace">The namespace the <see cref="IResource"/> to delete belongs to</param>
    /// <param name="dryRun">A boolean indicating whether or not to persist changes</param>
    public DeleteResourceCommand(string name, string? @namespace, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        Name = name;
        Namespace = @namespace;
        DryRun = dryRun;
    }

    /// <summary>
    /// Gets the name of the <see cref="IResource"/> to delete
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the namespace the <see cref="IResource"/> to delete belongs to
    /// </summary>
    public string? Namespace { get; }

    /// <summary>
    /// Gets a boolean indicating whether or not to persist changes
    /// </summary>
    public bool DryRun { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="DeleteResourceCommand"/>s
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to create</typeparam>
/// <param name="repository">The service used to manage <see cref="IResource"/>s</param>
public class DeleteResourceCommandHandler<TResource>(IResourceRepository repository)
    : ICommandHandler<DeleteResourceCommand<TResource>, TResource>
    where TResource : class, IResource, new()
{

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<TResource>> HandleAsync(DeleteResourceCommand<TResource> command, CancellationToken cancellationToken)
    {
        var resource = await repository.RemoveAsync<TResource>(command.Name, command.Namespace, command.DryRun, cancellationToken).ConfigureAwait(false);
        return this.Ok(resource);
    }

}