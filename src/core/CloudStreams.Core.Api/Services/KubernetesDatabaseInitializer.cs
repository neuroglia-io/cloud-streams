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

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Represents the service used to initialize the Kubernetes store of resources (see #275, #276)
/// </summary>
/// <remarks>
/// The definitions of the resources are the CustomResourceDefinitions the Helm chart installs, which the application never creates nor updates, since it should not
/// hold the right to: it checks that they are installed, and fails fast otherwise. Resources stored in Redis are not migrated
/// </remarks>
/// <param name="loggerFactory">The service used to create <see cref="ILogger"/>s</param>
/// <param name="serviceProvider">The current <see cref="IServiceProvider"/></param>
public class KubernetesDatabaseInitializer(ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
    : Application.Services.DatabaseInitializer(loggerFactory, serviceProvider)
{

    /// <inheritdoc/>
    protected override async Task SeedAsync(CancellationToken cancellationToken)
    {
        var database = this.ServiceProvider.GetRequiredService<IDatabase>();
        var missing = new List<string>();
        foreach (var definition in CloudStreamsDefaults.Resources.Definitions.AsEnumerable())
        {
            if (await database.GetResourceAsync(ResourceDefinition.ResourceGroup, ResourceDefinition.ResourceVersion, ResourceDefinition.ResourcePlural, definition.GetName(), null, cancellationToken).ConfigureAwait(false) == null) missing.Add(definition.GetName());
        }
        if (missing.Count > 0) throw new InvalidOperationException($"The CustomResourceDefinitions {string.Join(", ", missing.Select(m => $"'{m}'"))} are not installed. "
            + "Install the Helm chart of Cloud Streams, or apply the CRDs of its 'crds' directory with 'kubectl apply -f', before starting Cloud Streams with the Kubernetes store");
        // a process that only serves some namespaces leaves the default one alone, unless it serves it
        if (database is KubernetesResourceDatabase kubernetes && !kubernetes.IsServed(Namespace.DefaultNamespaceName)) return;
        await this.EnsureDefaultNamespaceAsync(database, cancellationToken).ConfigureAwait(false);
    }

}
