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

namespace CloudStreams.ResourceManagement.Application.Queries.Generic;

/// <summary>
/// Represents the <see cref="IQuery{TResult}"/> used to list 
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/>s to list</typeparam>
public class ListResourceQuery<TResource>
    : IQuery<IAsyncEnumerable<TResource>>
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Initializes a new <see cref="ListResourceQuery{TResource}"/>
    /// </summary>
    /// <param name="namespace">The namespace the resources to list belong to, if any</param>
    /// <param name="labelSelectors">An <see cref="IEnumerable{T}"/> containing the label to select the resources to list by</param>
    public ListResourceQuery(string? @namespace = null, IEnumerable<ResourceLabelSelector>? labelSelectors = null)
    {
        this.Namespace = @namespace;
        this.LabelSelectors = labelSelectors;
    }

    /// <summary>
    /// Gets the namespace the resources to list belong to, if any
    /// </summary>
    public string? Namespace { get; }

    /// <summary>
    /// Gets an <see cref="IEnumerable{T}"/> containing the label to select the resources to list by
    /// </summary>
    public IEnumerable<ResourceLabelSelector>? LabelSelectors { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="ListResourceQuery{TResource}"/> instances
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to list</typeparam>
public class ListResourceQueryHandler<TResource>
    : IQueryHandler<ListResourceQuery<TResource>, IAsyncEnumerable<TResource>>
     where TResource : class, IResource, new()
{

    IResourceRepository _ResourceRepository;

    /// <inheritdoc/>
    public ListResourceQueryHandler(IResourceRepository resourceRepository)
    {
        this._ResourceRepository = resourceRepository;
    }

    async Task<Response<IAsyncEnumerable<TResource>>> MediatR.IRequestHandler<ListResourceQuery<TResource>, Response<IAsyncEnumerable<TResource>>>.Handle(ListResourceQuery<TResource> query, CancellationToken cancellationToken)
    {
        var resources = await this._ResourceRepository.ListResourcesAsync<TResource>(query.Namespace, query.LabelSelectors, cancellationToken).ConfigureAwait(false);
        return this.Ok(resources!);
    }

}