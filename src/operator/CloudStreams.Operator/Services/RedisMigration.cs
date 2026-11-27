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

using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Data.Infrastructure.ResourceOriented.Services;
using Neuroglia.Serialization;

namespace CloudStreams.Operator.Services;

/// <summary>
/// Copies the namespaces and the resources of the Redis store into the Kubernetes store, with their status, so that subscriptions resume from their offsets: the
/// one-off upgrade from the Redis mode to the Kubernetes mode (see #283)
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="source">The Redis store to copy the resources of</param>
/// <param name="target">The Kubernetes store to copy the resources into</param>
/// <param name="serializer">The service used to serialize and deserialize JSON</param>
public class RedisMigration(ILogger<RedisMigration> logger, IDatabase source, IDatabase target, IJsonSerializer serializer)
{

    readonly HashSet<string> _namespacesToCreate = [];

    /// <summary>
    /// Gets the kinds of resources to copy, in the order they're copied: namespaces first, and the resources others refer to before them
    /// </summary>
    public static IReadOnlyList<ResourceDefinitionInfo> Definitions { get; } =
    [
        new Namespace().Definition,
        new Schema().Definition,
        new Sink().Definition,
        new Feed().Definition,
        new Gateway().Definition,
        new Broker().Definition,
        new Subscription().Definition
    ];

    /// <summary>
    /// Copies the namespaces and the resources of the Redis store into the Kubernetes store. Resources that already exist there are left as they are
    /// </summary>
    /// <param name="dryRun">A boolean indicating whether or not to only check what would be copied</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The number of copied resources, and the number of those that already existed</returns>
    public virtual async Task<(int Copied, int Existing)> RunAsync(bool dryRun, CancellationToken cancellationToken = default)
    {
        var copied = 0;
        var existing = 0;
        foreach (var definition in Definitions)
        {
            await foreach (var resource in source.GetResourcesAsync(definition.Group, definition.Version, definition.Plural, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (await this.CopyAsync(resource, definition, dryRun, cancellationToken).ConfigureAwait(false)) copied++;
                else existing++;
            }
        }
        return (copied, existing);
    }

    /// <summary>
    /// Copies the specified resource, and its status, unless it already exists
    /// </summary>
    /// <param name="resource">The resource to copy</param>
    /// <param name="definition">The definition of the resource</param>
    /// <param name="dryRun">A boolean indicating whether or not to only check whether the resource would be copied</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A boolean indicating whether the resource was, or would be, copied</returns>
    protected virtual async Task<bool> CopyAsync(IResource resource, ResourceDefinitionInfo definition, bool dryRun, CancellationToken cancellationToken)
    {
        var name = resource.GetName();
        var @namespace = resource.GetNamespace();
        var description = string.IsNullOrWhiteSpace(@namespace) ? $"{definition.Kind.ToLowerInvariant()} '{name}'" : $"{definition.Kind.ToLowerInvariant()} '{name}' of the namespace '{@namespace}'";
        if (await target.GetResourceAsync(definition.Group, definition.Version, definition.Plural, name, @namespace, cancellationToken).ConfigureAwait(false) != null)
        {
            logger.LogInformation("The {resource} already exists: skipped", description);
            return false;
        }
        // only the name, the namespace, the labels and the annotations are copied of the metadata: the rest belongs to the store
        var node = JsonNode.Parse(serializer.SerializeToText((object)resource))!.AsObject();
        var metadata = node["metadata"]!.AsObject();
        foreach (var field in metadata.Select(f => f.Key).Where(k => k is not ("name" or "namespace" or "labels" or "annotations")).ToList()) metadata.Remove(field);
        var status = node["status"]?.DeepClone();
        node.Remove("status");
        var isNamespace = definition.Plural == NamespaceDefinition.ResourcePlural;
        if (dryRun && isNamespace) this._namespacesToCreate.Add(name);
        // a dry run doesn't create namespaces: the API server would refuse to check the creation of a resource in one it would create
        if (dryRun && !isNamespace && @namespace != null && this._namespacesToCreate.Contains(@namespace))
        {
            logger.LogInformation("The {resource} would be copied", description);
            return true;
        }
        await target.CreateResourceAsync(serializer.Deserialize<Resource>(node.ToJsonString())!, definition.Group, definition.Version, definition.Plural, @namespace, dryRun, cancellationToken).ConfigureAwait(false);
        if (status != null && !dryRun && !isNamespace)
        {
            // the status holds the offsets of subscriptions, which they resume from
            var created = await target.GetResourceAsync(definition.Group, definition.Version, definition.Plural, name, @namespace, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"The {description} could not be read once copied");
            var withStatus = JsonNode.Parse(serializer.SerializeToText((object)created))!.AsObject();
            withStatus["status"] = status;
            await target.ReplaceSubResourceAsync(serializer.Deserialize<Resource>(withStatus.ToJsonString())!, definition.Group, definition.Version, definition.Plural, name, "status", @namespace, false, cancellationToken).ConfigureAwait(false);
        }
        logger.LogInformation(dryRun ? "The {resource} would be copied" : "Copied the {resource}", description);
        return true;
    }

}
