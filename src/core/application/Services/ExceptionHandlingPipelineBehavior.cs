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

using MediatR;
using System.Net;

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Represents the <see cref="IPipelineBehavior{TRequest, TResponse}"/> used to catch and transform uncaught <see cref="Exception"/>s into <see cref="Response"/>s
/// </summary>
/// <typeparam name="TRequest">The type of request to handle</typeparam>
/// <typeparam name="TResponse">The expected type of response</typeparam>
public class ExceptionHandlingPipelineBehavior<TRequest, TResponse>
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
        catch(HttpRequestException ex)
        {
            return new()
            {
                Status = (int)ex.StatusCode!,
                Title = ex.StatusCode.ToString(),
                Detail = ex.Message
            };
        }
        catch (Exception ex)
        {
            return new()
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = ex.GetType().Name.Replace(nameof(Exception), string.Empty),
                Detail = ex.Message
            };
        }
    }

}
