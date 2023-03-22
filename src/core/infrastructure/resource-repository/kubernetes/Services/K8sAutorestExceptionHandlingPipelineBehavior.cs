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
using k8s.Autorest;
using k8s.Models;
using MediatR;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents the <see cref="IPipelineBehavior{TRequest, TResponse}"/> used to catch and transform <see cref="HttpOperationException"/>s into <see cref="Response"/>s
/// </summary>
/// <typeparam name="TRequest">The type of request to handle</typeparam>
/// <typeparam name="TResponse">The expected type of response</typeparam>
public class K8sAutorestExceptionHandlingPipelineBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
    where TResponse : Response, new()
{

    /// <inheritdoc/>
    public virtual async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch(HttpOperationException ex)
        {
            if (!string.IsNullOrWhiteSpace(ex.Response.Content))
            {
                var status = Serializer.Json.Deserialize<V1Status>(ex.Response.Content);
                if (status != null) return status.ToResponse<TResponse>();
            }
            return new()
            {
                Status = (int)ex.Response.StatusCode,
                Title = ex.Response.StatusCode.ToString(),
                Detail = ex.Response.Content
            };
        }
    }

}