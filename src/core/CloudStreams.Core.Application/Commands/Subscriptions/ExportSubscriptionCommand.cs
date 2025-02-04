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

using Neuroglia.Collections;
using Neuroglia.Serialization;

namespace CloudStreams.Core.Application.Commands.Subscriptions;

/// <summary>
/// Represents the command used to export a <see cref="Subscription"/>
/// </summary>
public class ExportSubscriptionCommand
    : Command<Stream>
{

    /// <summary>
    /// Initializes a new <see cref="ExportSubscriptionCommand"/>
    /// </summary>
    protected ExportSubscriptionCommand() { }

    /// <summary>
    /// Initializes a new <see cref="ExportSubscriptionCommand"/>
    /// </summary>
    /// <param name="name">The name of the <see cref="IResource"/> to patch</param>
    public ExportSubscriptionCommand(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        this.Name = name;
    }

    /// <summary>
    /// Gets the name of the <see cref="Subscription"/> to export
    /// </summary>
    public string Name { get; } = null!;

}

/// <summary>
/// Represents the service used to handle <see cref="ExportSubscriptionCommand"/>s
/// </summary>
/// <param name="resources">The <see cref="IResourceRepository"/> used to manage the application's <see cref="Resource"/>s</param>
/// <param name="yamlSerializer">The service used to serialize/deserialize data to/from YAML</param>
public class ExportSubscriptionCommandHandler(IResourceRepository resources, IYamlSerializer yamlSerializer)
    : ICommandHandler<ExportSubscriptionCommand, Stream>
{

    /// <summary>
    /// Gets the <see cref="IResourceRepository"/> used to manage the application's <see cref="Resource"/>s
    /// </summary>
    protected IResourceRepository Resources { get; } = resources;

    /// <summary>
    /// Gets the service used to serialize/deserialize data to/from YAML
    /// </summary>
    protected IYamlSerializer YamlSerializer { get; } = yamlSerializer;

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<Stream>> HandleAsync(ExportSubscriptionCommand command, CancellationToken cancellationToken = default)
    {
        var subscription = await this.Resources.GetAsync<Subscription>(command.Name, null, cancellationToken).ConfigureAwait(false);
        if (subscription == null) return this.NotFound();
        var map = new Dictionary<string, object>()
        {
            { "apiVersion", subscription.ApiVersion },
            { "kind", subscription.Kind },
            { "metadata",  new { name = subscription.GetName() }},
            { "spec", subscription.Spec }
        };
        return this.Ok(new MemoryStream(this.YamlSerializer.SerializeToByteArray(map)!));
    }

}
