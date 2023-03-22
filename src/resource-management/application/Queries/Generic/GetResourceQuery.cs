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
/// Represents the <see cref="IQuery{TResult}"/> used to get an existing <see cref="IResource"/>
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to get</typeparam>
public class GetResourceQuery<TResource>
    : IQuery<TResource>
    where TResource : class, IResource, new()
{

    /// <summary>
    /// Initializes a new <see cref="GetResourceQuery{TResource}"/>
    /// </summary>
    /// <param name="name">The name of the resource to get</param>
    /// <param name="namespace">The namespace the resource to get belongs to</param>
    public GetResourceQuery(string name, string? @namespace = null)
    {
        Name = name;
        Namespace = @namespace;
    }

    /// <summary>
    /// Gets the name of the resource to get
    /// </summary>
    public virtual string Name { get; }

    /// <summary>
    /// Gets the namespace the resource to get belongs to
    /// </summary>
    public virtual string? Namespace { get; }

}

/// <summary>
/// Represents the service used to handle <see cref="GetResourceQuery{TResource}"/> instances
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to get</typeparam>
public class GetResourceQueryHandler<TResource>
    : IQueryHandler<GetResourceQuery<TResource>, TResource>
    where TResource : class, IResource, new()
{

    IResourceRepository _ResourceRepository;

    /// <inheritdoc/>
    public GetResourceQueryHandler(IResourceRepository resourceRepository)
    {
        this._ResourceRepository = resourceRepository;
    }

    async Task<Response<TResource>> MediatR.IRequestHandler<GetResourceQuery<TResource>, Response<TResource>>.Handle(GetResourceQuery<TResource> query, CancellationToken cancellationToken)
    {
        var resource = await this._ResourceRepository.GetResourceAsync<TResource>(query.Name, query.Namespace, cancellationToken).ConfigureAwait(false);
        if (resource == null) return Response.ResourceNotFound<TResource>(query.Name, query.Namespace);
        return this.Ok(resource);
    }

}
