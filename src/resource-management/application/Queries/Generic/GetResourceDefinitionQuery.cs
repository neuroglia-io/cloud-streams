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
/// Represents the <see cref="IQuery{TResult}"/> used to get the definition of the specified <see cref="IResource"/> type
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to get the definition of</typeparam>
public class GetResourceDefinitionQuery<TResource>
    : IQuery<IResourceDefinition>
    where TResource : class, IResource, new()
{



}

/// <summary>
/// Represents the service used to handle <see cref="GetResourceDefinitionQuery{TResource}"/> instances
/// </summary>
/// <typeparam name="TResource">The type of <see cref="IResource"/> to get</typeparam>
public class GetResourceDefinitionQueryHandler<TResource>
    : IQueryHandler<GetResourceDefinitionQuery<TResource>, IResourceDefinition>
    where TResource : class, IResource, new()
{

    IResourceRepository _ResourceRepository;

    /// <inheritdoc/>
    public GetResourceDefinitionQueryHandler(IResourceRepository resourceRepository)
    {
        this._ResourceRepository = resourceRepository;
    }

    async Task<Response<IResourceDefinition>> MediatR.IRequestHandler<GetResourceDefinitionQuery<TResource>, Response<IResourceDefinition>>.Handle(GetResourceDefinitionQuery<TResource> query, CancellationToken cancellationToken)
    {
        var definition = await this._ResourceRepository.GetResourceDefinitionAsync<TResource>(cancellationToken).ConfigureAwait(false);
        if (definition == null) return Response.ResourceDefinitionNotFound<TResource, IResourceDefinition>();
        return this.Ok(definition);
    }

}
