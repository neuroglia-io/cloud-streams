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

using Json.Patch;
using Microsoft.Extensions.Logging;
using Neuroglia.Data.PatchModel.Services;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Represents a <see cref="RedisDatabase"/> that applies patches and replacements atomically
/// </summary>
/// <remarks>
/// The base implementation reads a resource, applies the patch, and writes the result back, with nothing to prevent another writer from changing the resource in
/// between: concurrent updates could overwrite each other, and the expected resource version, as well as JSON patch <c>test</c> operations, were checked against a
/// state that might no longer be current (see #161). This implementation writes with a Redis transaction that only commits if the resource is still exactly as read,
/// so that every update applies to the state it was checked against:
/// <list type="bullet">
/// <item>A patch sent without an expected resource version is applied again to the new state when the resource changes concurrently</item>
/// <item>A patch or a replacement sent with an expected resource version fails with <c>409 Conflict</c> as soon as the resource has changed</item>
/// <item>A JSON patch that cannot be applied, for example because a <c>test</c> operation fails, fails with <c>409 Conflict</c> and changes nothing</item>
/// </list>
/// </remarks>
/// <inheritdoc/>
public class AtomicRedisDatabase(ILoggerFactory loggerFactory, IConnectionMultiplexer redis, IJsonSerializer serializer, IEnumerable<IPatchHandler> patchHandlers)
    : RedisDatabase(loggerFactory, redis, serializer, patchHandlers)
{

    /// <summary>
    /// Gets the maximum amount of times an update is attempted when the resource keeps changing concurrently
    /// </summary>
    public const int MaxUpdateAttempts = 25;

    /// <summary>
    /// Gets the type of the problem returned when a JSON patch cannot be applied to the current state of a resource
    /// </summary>
    public static readonly Uri PatchConflictProblemType = new("https://cloud-streams.io/problems/patch-conflict");

    /// <summary>
    /// Gets the channel resource watch events are published to. Must match the one of the base implementation, which subscribes to it
    /// </summary>
    static readonly RedisChannel WatchEventChannel = new("resource-watch-events", RedisChannel.PatternMode.Literal);

    static readonly string MetadataProperty = nameof(IMetadata.Metadata).ToCamelCase();
    static readonly string SpecProperty = nameof(ISpec.Spec).ToCamelCase();
    static readonly string StatusProperty = nameof(IStatus.Status).ToCamelCase();
    static readonly string LabelsProperty = nameof(ResourceMetadata.Labels).ToCamelCase();
    static readonly string AnnotationsProperty = nameof(ResourceMetadata.Annotations).ToCamelCase();

    /// <inheritdoc/>
    public override Task<IResource> PatchResourceAsync(Patch patch, string group, string version, string plural, string name, string? @namespace = null, string? resourceVersion = null, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var reference = new ResourceReference(new(group, version, plural), name, @namespace);
        return this.UpdateAsync(this.BuildResourceKey(group, version, plural, name, @namespace), reference, resourceVersion, false, stored => this.ApplyPatchAsync(patch, stored, reference, IsSpecOrMetadataOperation, cancellationToken), dryRun, cancellationToken);
    }

    /// <inheritdoc/>
    public override Task<IResource> PatchSubResourceAsync(Patch patch, string group, string version, string plural, string name, string subResource, string? @namespace = null, string? resourceVersion = null, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentException.ThrowIfNullOrWhiteSpace(subResource);
        var reference = new SubResourceReference(new(group, version, plural), name, subResource, @namespace);
        return this.UpdateAsync(this.BuildResourceKey(group, version, plural, name, @namespace), reference, resourceVersion, false, stored => this.ApplyPatchAsync(patch, stored, reference, IsStatusOperation, cancellationToken), dryRun, cancellationToken);
    }

    /// <inheritdoc/>
    public override Task<IResource> ReplaceResourceAsync(IResource resource, string group, string version, string plural, string name, string? @namespace = null, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var reference = new ResourceReference(new(group, version, plural), name, @namespace);
        var replacement = resource.ConvertTo<Resource>()!;
        return this.UpdateAsync(this.BuildResourceKey(group, version, plural, name, @namespace), reference, replacement.Metadata.ResourceVersion, true, stored => this.ApplyReplacementAsync(replacement, stored.Resource, reference, IsSpecOrMetadataOperation, cancellationToken), dryRun, cancellationToken);
    }

    /// <inheritdoc/>
    public override Task<IResource> ReplaceSubResourceAsync(IResource resource, string group, string version, string plural, string name, string subResource, string? @namespace = null, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(subResource);
        var reference = new SubResourceReference(new(group, version, plural), name, subResource, @namespace);
        var replacement = resource.ConvertTo<Resource>()!;
        return this.UpdateAsync(this.BuildResourceKey(group, version, plural, name, @namespace), reference, replacement.Metadata.ResourceVersion, true, stored => this.ApplyReplacementAsync(replacement, stored.Resource, reference, IsStatusOperation, cancellationToken), dryRun, cancellationToken);
    }

    /// <summary>
    /// Updates the specified resource atomically
    /// </summary>
    /// <param name="key">The Redis key of the resource to update</param>
    /// <param name="reference">A reference to the resource to update</param>
    /// <param name="expectedVersion">The version the resource is expected to be at, if any</param>
    /// <param name="versionRequired">A boolean indicating whether or not the resource must be at the expected version even if none has been specified, which is the case of replacements</param>
    /// <param name="update">A function used to compute the updated resource from its current state</param>
    /// <param name="dryRun">A boolean indicating whether or not to only compute the update, without persisting it</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The updated resource</returns>
    /// <exception cref="ProblemDetailsException">The resource does not exist, is not at the expected version, has kept changing concurrently, or cannot be updated</exception>
    protected virtual async Task<IResource> UpdateAsync(string key, IResourceReference reference, string? expectedVersion, bool versionRequired, Func<StoredResource, Task<Resource>> update, bool dryRun, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await this.Database.StringGetAsync(key).ConfigureAwait(false);
            if (json.IsNullOrEmpty) throw new ProblemDetailsException(ResourceProblemDetails.ResourceNotFound(reference));
            var original = this.Serializer.Deserialize<Resource>(json.ToString())!;
            if ((versionRequired || !string.IsNullOrWhiteSpace(expectedVersion)) && !string.Equals(original.Metadata.ResourceVersion, expectedVersion, StringComparison.OrdinalIgnoreCase))
                throw new ProblemDetailsException(ResourceProblemDetails.ResourceOptimisticConcurrencyCheckFailed(reference, expectedVersion!, original.Metadata.ResourceVersion!));
            var updated = await update(new(json.ToString(), original)).ConfigureAwait(false);
            if (dryRun) return updated;
            updated.Metadata.ResourceVersion = this.ComputeResourceVersion(updated);
            var transaction = this.Database.CreateTransaction();
            // the write only commits if nobody has written the resource since it was read
            transaction.AddCondition(Condition.StringEqual(key, json));
            _ = transaction.StringSetAsync(key, this.Serializer.SerializeToText(updated));
            _ = transaction.PublishAsync(WatchEventChannel, this.Serializer.SerializeToText(new ResourceWatchEvent(ResourceWatchEventType.Updated, updated)));
            if (await transaction.ExecuteAsync().ConfigureAwait(false)) return updated;
            // the resource has changed in the meantime: an update that expects a version fails on the next read, any other one is applied again to the new state
            if (attempt >= MaxUpdateAttempts) throw new ProblemDetailsException(new(PatchConflictProblemType, "Conflict", (int)HttpStatusCode.Conflict, $"The resource '{reference}' kept changing concurrently, and could not be updated after {attempt} attempts"));
            // a random, exponentially growing delay keeps writers that lost the same race from colliding again
            await Task.Delay(Random.Shared.Next(1, Math.Min(200, 2 << Math.Min(attempt, 16))), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Applies the specified patch to the specified resource
    /// </summary>
    /// <param name="patch">The patch to apply</param>
    /// <param name="stored">The resource to patch, as stored</param>
    /// <param name="reference">A reference to the resource to patch</param>
    /// <param name="isAllowed">A function used to determine which of the resulting changes are allowed. Others are ignored, as they are by the base implementation</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The patched resource</returns>
    protected virtual async Task<Resource> ApplyPatchAsync(Patch patch, StoredResource stored, IResourceReference reference, Func<PatchOperation, bool> isAllowed, CancellationToken cancellationToken)
    {
        Resource patched;
        if (patch.Type == PatchType.JsonPatch)
        {
            // the generic handler returns the resource unchanged when an operation fails, which would make a failed 'test' operation indistinguishable from a no-op
            var jsonPatch = patch.Document as JsonPatch ?? this.Serializer.Deserialize<JsonPatch>(this.Serializer.SerializeToText(patch.Document))!;
            // the patch applies to the stored JSON itself, whose values compare like those of the patch, which are parsed too
            var result = jsonPatch.Apply(JsonNode.Parse(stored.Json));
            if (!result.IsSuccess) throw new ProblemDetailsException(new(PatchConflictProblemType, "Conflict", (int)HttpStatusCode.Conflict, $"The patch cannot be applied to the current state of the resource '{reference}': {result.Error}"));
            patched = this.Serializer.Deserialize<Resource>(result.Result!.ToJsonString())!;
        }
        else
        {
            var patchHandler = this.PatchHandlers.FirstOrDefault(h => h.Supports(patch.Type)) ?? throw new NullReferenceException($"Failed to find a registered service used to handle patches of type '{patch.Type}'");
            patched = (await patchHandler.ApplyPatchAsync(patch.Document, stored.Resource, cancellationToken).ConfigureAwait(false))!;
        }
        return await this.ApplyReplacementAsync(patched, stored.Resource, reference, isAllowed, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies the allowed differences between the specified replacement and the specified resource
    /// </summary>
    /// <param name="replacement">The resource to replace the original with</param>
    /// <param name="original">The resource to replace</param>
    /// <param name="reference">A reference to the resource to replace</param>
    /// <param name="isAllowed">A function used to determine which of the differences are allowed. Others are ignored</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The updated resource</returns>
    /// <exception cref="ProblemDetailsException">No allowed difference has been found</exception>
    protected virtual async Task<Resource> ApplyReplacementAsync(Resource replacement, Resource original, IResourceReference reference, Func<PatchOperation, bool> isAllowed, CancellationToken cancellationToken)
    {
        var diff = JsonPatchUtility.CreateJsonPatchFromDiff(original, replacement);
        var operations = diff.Operations.Where(isAllowed).ToList();
        if (operations.Count == 0) throw new ProblemDetailsException(ResourceProblemDetails.ResourceNotModified(reference));
        var patchHandler = this.PatchHandlers.FirstOrDefault(h => h.Supports(PatchType.JsonPatch)) ?? throw new NullReferenceException($"Failed to find a registered service used to handle patches of type '{PatchType.JsonPatch}'");
        var updated = (await patchHandler.ApplyPatchAsync(new JsonPatch(operations), original, cancellationToken).ConfigureAwait(false))!;
        if (operations.Any(o => o.Path.Count > 0 && o.Path[0] == SpecProperty)) updated.Metadata.Generation++;
        return updated;
    }

    /// <summary>
    /// Computes the version of the specified resource
    /// </summary>
    /// <param name="resource">The resource to compute the version of</param>
    /// <returns>The version of the specified resource</returns>
    /// <remarks>
    /// The version is a hash of the whole resource, metadata included. Unlike the hash of the base implementation, which is computed with <see cref="string.GetHashCode()"/>,
    /// it is the same in every process, and it changes when labels or annotations do
    /// </remarks>
    protected virtual string ComputeResourceVersion(Resource resource)
    {
        var resourceVersion = resource.Metadata.ResourceVersion;
        resource.Metadata.ResourceVersion = null;
        try { return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(this.Serializer.SerializeToText(resource))), 0, 8); }
        finally { resource.Metadata.ResourceVersion = resourceVersion; }
    }

    /// <summary>
    /// Represents a resource as read from Redis
    /// </summary>
    /// <param name="Json">The JSON the resource is stored as</param>
    /// <param name="Resource">The deserialized resource</param>
    protected record StoredResource(string Json, Resource Resource);

    static bool IsStatusOperation(PatchOperation operation) => operation.Path.Count > 0 && operation.Path[0] == StatusProperty;

    static bool IsSpecOrMetadataOperation(PatchOperation operation) => operation.Path.Count > 0
        && (operation.Path[0] == SpecProperty || (operation.Path[0] == MetadataProperty && operation.Path.Count > 1 && (operation.Path[1] == LabelsProperty || operation.Path[1] == AnnotationsProperty)));

}
