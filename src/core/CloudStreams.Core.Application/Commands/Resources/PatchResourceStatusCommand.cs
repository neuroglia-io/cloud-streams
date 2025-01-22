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

namespace CloudStreams.Core.Application.Commands.Resources;

/// <summary>
/// Represents the <see cref="ICommand"/> used to patch the status of an existing <see cref="IResource"/>
/// </summary>
public class PatchResourceStatusCommand
    : Command<IResource>
{

    /// <summary>
    /// Initializes a new <see cref="PatchResourceStatusCommand"/>
    /// </summary>
    protected PatchResourceStatusCommand() { }

    /// <summary>
    /// Initializes a new <see cref="PatchResourceStatusCommand"/>
    /// </summary>
    /// <param name="group">The API group the resource to patch belongs to</param>
    /// <param name="version">The version of the resource to patch</param>
    /// <param name="plural">The plural name of the type of resource to patch</param>
    /// <param name="name">The name of the <see cref="IResource"/> to patch</param>
    /// <param name="namespace">The namespace the <see cref="IResource"/> to patch belongs to</param>
    /// <param name="patch">The patch to apply</param>
    /// <param name="dryRun">A boolean indicating whether or not to persist changes</param>
    public PatchResourceStatusCommand(string group, string version, string plural, string name, string? @namespace, Patch patch, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(group)) throw new ArgumentNullException(nameof(group));
        if (string.IsNullOrWhiteSpace(version)) throw new ArgumentNullException(nameof(version));
        if (string.IsNullOrWhiteSpace(plural)) throw new ArgumentNullException(nameof(plural));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        this.Group = group;
        this.Version = version;
        this.Plural = plural;
        this.Name = name;
        this.Namespace = @namespace;
        this.Patch = patch ?? throw new ArgumentNullException(nameof(patch));
        this.DryRun = dryRun;
    }

    /// <summary>
    /// Gets the API group the resource to patch belongs to
    /// </summary>
    public string Group { get; } = null!;

    /// <summary>
    /// Gets the version of the resource to patch
    /// </summary>
    public string Version { get; } = null!;

    /// <summary>
    /// Gets the plural name of the type of resource to patch
    /// </summary>
    public string Plural { get; } = null!;

    /// <summary>
    /// Gets the name of the <see cref="IResource"/> to patch
    /// </summary>
    public string Name { get; } = null!;

    /// <summary>
    /// Gets the name of the <see cref="IResource"/> to patch
    /// </summary>
    public string? Namespace { get; }

    /// <summary>
    /// Gets the patch to apply
    /// </summary>
    public Patch Patch { get; } = null!;

    /// <summary>
    /// Gets a boolean indicating whether or not to persist changes
    /// </summary>
    public bool DryRun { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="PatchResourceStatusCommand"/>s
/// </summary>
/// <param name="repository">The service used to manage <see cref="IResource"/>s</param>
public class PatchResourceStatusCommandHandler(IResourceRepository repository)
    : ICommandHandler<PatchResourceStatusCommand, IResource>
{

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<IResource>> HandleAsync(PatchResourceStatusCommand command, CancellationToken cancellationToken)
    {
        var resource = await repository.PatchSubResourceAsync(command.Patch, command.Group, command.Version, command.Plural, command.Name, "status", command.Namespace, null, command.DryRun, cancellationToken).ConfigureAwait(false);
        return this.Ok(resource);
    }

}