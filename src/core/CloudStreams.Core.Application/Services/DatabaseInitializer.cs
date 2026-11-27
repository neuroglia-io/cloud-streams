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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Neuroglia.Serialization;

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Represents the service used to initialize the Cloud Streams resource database
/// </summary>
/// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
/// <param name="serviceProvider">The current <see cref="IServiceProvider"/></param>
public class DatabaseInitializer(ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
    : Neuroglia.Data.Infrastructure.ResourceOriented.Services.DatabaseInitializer(loggerFactory, serviceProvider)
{

    readonly ILogger _logger = loggerFactory.CreateLogger<DatabaseInitializer>();

    /// <inheritdoc/>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var database = this.ServiceProvider.GetRequiredService<IDatabase>();
        try
        {
            await database.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ProblemDetailsException ex) when (IsConflict(ex))
        {
            this._logger.LogDebug("The database has been initialized concurrently by another application");
        }
        await this.SeedAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Definitions stored by an earlier version are replaced when they differ, so that their schema and scope follow the application's. Resources of a kind that
    /// became namespaced, which earlier versions stored as cluster resources, are moved to the default namespace (see #215)
    /// </remarks>
    protected override async Task SeedAsync(CancellationToken cancellationToken)
    {
        var database = this.ServiceProvider.GetRequiredService<IDatabase>();
        var serializer = this.ServiceProvider.GetRequiredService<IJsonSerializer>();
        await this.EnsureDefaultNamespaceAsync(database, cancellationToken).ConfigureAwait(false);
        foreach (var definition in CloudStreamsDefaults.Resources.Definitions.AsEnumerable())
        {
            try
            {
                await database.CreateResourceAsync(definition, cancellationToken: cancellationToken).ConfigureAwait(false);
                continue;
            }
            catch (ProblemDetailsException ex) when (IsConflict(ex))
            {
                this._logger.LogDebug("The definition of the resource '{definition}' already exists", definition.GetName());
            }
            try
            {
                await this.UpdateDefinitionAsync(database, serializer, definition, cancellationToken).ConfigureAwait(false);
                if (definition.Spec.Scope == ResourceScope.Namespaced) await this.MoveClusterResourcesAsync(database, definition, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                this._logger.LogError(ex, "Failed to update the definition of the resource '{definition}'", definition.GetName());
            }
        }
    }

    /// <summary>
    /// Ensures that the default namespace exists, which earlier versions did not use
    /// </summary>
    /// <param name="database">The <see cref="IDatabase"/> to initialize</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task EnsureDefaultNamespaceAsync(IDatabase database, CancellationToken cancellationToken)
    {
        try
        {
            if (await database.GetResourceAsync<Namespace>(Namespace.DefaultNamespaceName, null, cancellationToken).ConfigureAwait(false) != null) return;
            await database.CreateNamespaceAsync(Namespace.DefaultNamespaceName, false, cancellationToken).ConfigureAwait(false);
        }
        catch (ProblemDetailsException ex) when (IsConflict(ex)) { }
    }

    /// <summary>
    /// Replaces the stored definition of the specified resource if it differs from the application's
    /// </summary>
    /// <param name="database">The <see cref="IDatabase"/> to update</param>
    /// <param name="serializer">The service used to serialize definitions</param>
    /// <param name="definition">The application's definition</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task UpdateDefinitionAsync(IDatabase database, IJsonSerializer serializer, ResourceDefinition definition, CancellationToken cancellationToken)
    {
        var stored = await database.GetDefinitionAsync(definition.Spec.Group, definition.Spec.Names.Plural, cancellationToken).ConfigureAwait(false);
        if (stored == null) return;
        if (serializer.SerializeToText(stored.Spec) == serializer.SerializeToText(definition.Spec)) return;
        var replacement = serializer.Deserialize<ResourceDefinition>(serializer.SerializeToText(definition))!;
        replacement.Metadata.ResourceVersion = stored.Metadata.ResourceVersion;
        try
        {
            await database.ReplaceResourceAsync(replacement, ResourceDefinition.ResourceGroup, ResourceDefinition.ResourceVersion, ResourceDefinition.ResourcePlural, definition.GetName(), null, false, cancellationToken).ConfigureAwait(false);
            this._logger.LogInformation("Updated the definition of the resource '{definition}'", definition.GetName());
        }
        catch (ProblemDetailsException ex) when (ex.Problem.Status is (int)HttpStatusCode.Conflict or (int)HttpStatusCode.NotModified)
        {
            this._logger.LogDebug("The definition of the resource '{definition}' has been updated concurrently", definition.GetName());
        }
    }

    /// <summary>
    /// Moves the resources of a namespaced kind that are stored as cluster resources to the default namespace
    /// </summary>
    /// <param name="database">The <see cref="IDatabase"/> to update</param>
    /// <param name="definition">The definition of the resources to move</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task MoveClusterResourcesAsync(IDatabase database, ResourceDefinition definition, CancellationToken cancellationToken)
    {
        var group = definition.Spec.Group;
        var version = definition.Spec.Versions.First(v => v.Storage).Name;
        var plural = definition.Spec.Names.Plural;
        var orphans = new List<IResource>();
        await foreach (var resource in database.GetResourcesAsync(group, version, plural, null, null, cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(resource.GetNamespace())) orphans.Add(resource);
        }
        foreach (var orphan in orphans)
        {
            var moved = orphan.ConvertTo<Resource>()!;
            moved.Metadata.Namespace = Namespace.DefaultNamespaceName;
            try { await database.CreateResourceAsync(moved, group, version, plural, Namespace.DefaultNamespaceName, false, cancellationToken).ConfigureAwait(false); }
            catch (ProblemDetailsException ex) when (IsConflict(ex)) { /* moved by another application in the meantime */ }
            try { await database.DeleteResourceAsync(group, version, plural, orphan.GetName(), null, false, cancellationToken).ConfigureAwait(false); }
            catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.NotFound) { }
            this._logger.LogInformation("Moved the {kind} '{name}' to the namespace '{namespace}'", definition.Spec.Names.Kind, orphan.GetName(), Namespace.DefaultNamespaceName);
        }
    }

    static bool IsConflict(ProblemDetailsException ex) => ex.Problem.IsConflict();

}
