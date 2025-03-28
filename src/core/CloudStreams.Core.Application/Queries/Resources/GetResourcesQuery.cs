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

namespace CloudStreams.Core.Application.Queries.Resources;

/// <summary>
/// Represents the <see cref="IQuery{TResult}"/> used to retrieves <see cref="IResource"/>s of the specified type
/// </summary>
public class GetResourcesQuery
    : Query<IAsyncEnumerable<IResource>>
{

    /// <summary>
    /// Initializes a new <see cref="GetResourcesQuery"/>
    /// </summary>
    /// <param name="group">The API group the resources to get belongs to</param>
    /// <param name="version">The version of the resources to get</param>
    /// <param name="plural">The plural name of the type of resources to get</param>
    /// <param name="namespace">The namespace the <see cref="IResource"/>s to get belong to, if any</param>
    /// <param name="labelSelectors">A <see cref="List{T}"/> containing the <see cref="LabelSelector"/>s used to filter <see cref="IResource"/>s by</param>
    public GetResourcesQuery(string group, string version, string plural, string? @namespace, List<LabelSelector>? labelSelectors)
    {
        if (string.IsNullOrWhiteSpace(group)) throw new ArgumentNullException(nameof(group));
        if (string.IsNullOrWhiteSpace(version)) throw new ArgumentNullException(nameof(version));
        if (string.IsNullOrWhiteSpace(plural)) throw new ArgumentNullException(nameof(plural));
        this.Group = group;
        this.Version = version;
        this.Plural = plural;
        this.Namespace = @namespace;
        this.LabelSelectors = labelSelectors;
    }

    /// <summary>
    /// Gets the API group the resources to get belongs to
    /// </summary>
    public string Group { get; }

    /// <summary>
    /// Gets the version of the resources to get
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets the plural name of the type of resources to get
    /// </summary>
    public string Plural { get; }

    /// <summary>
    /// Gets the namespace the <see cref="IResource"/>s to get belong to, if any
    /// </summary>
    public string? Namespace { get; }

    /// <summary>
    /// Gets a <see cref="List{T}"/> containing the <see cref="LabelSelector"/>s used to filter <see cref="IResource"/>s by
    /// </summary>
    public List<LabelSelector>? LabelSelectors { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="GetResourcesQuery"/> instances
/// </summary>
/// <param name="repository">The service used to manage <see cref="IResource"/>s</param>
public class GetResourcesQueryHandler(IResourceRepository repository)
    : IQueryHandler<GetResourcesQuery, IAsyncEnumerable<IResource>>
{

    /// <inheritdoc/>
    public virtual Task<IOperationResult<IAsyncEnumerable<IResource>>> HandleAsync(GetResourcesQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(this.Ok(repository.GetAllAsync(query.Group, query.Version, query.Plural, query.Namespace, query.LabelSelectors, cancellationToken)));
    }

}