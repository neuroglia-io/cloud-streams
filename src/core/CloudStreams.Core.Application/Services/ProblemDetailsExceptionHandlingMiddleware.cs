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

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Represents an <see cref="IMiddleware{TRequest, TResult}"/> used to handle <see cref="ProblemDetailsException"/>s during the execution of an <see cref="IRequest"/>
/// </summary>
/// <typeparam name="TRequest">The type of <see cref="IRequest"/> to handle</typeparam>
/// <typeparam name="TResult">The type of expected <see cref="IOperationResult"/></typeparam>
public class ProblemDetailsExceptionHandlingMiddleware<TRequest, TResult>
    : IMiddleware<TRequest, TResult>
    where TRequest : IRequest<TResult>
    where TResult : IOperationResult
{

    /// <inheritdoc/>
    public virtual async Task<TResult> HandleAsync(TRequest request, RequestHandlerDelegate<TResult> next, CancellationToken cancellationToken = default)
    {
        try
        {
            return await next();
        }
        catch (ProblemDetailsException ex)
        {
            if (!TryCreateErrorResponse(ex.Problem, out var response)) throw;
            return response;
        }
    }

    /// <summary>
    /// Creates a new error <see cref="IOperationResult"/>
    /// </summary>
    /// <param name="result">The newly created <see cref="IOperationResult"/></param>
    /// <param name="problem">The <see cref="ProblemDetails"/> to create the new <see cref="IOperationResult"/> for</param>
    /// <returns>A new error <see cref="IOperationResult"/></returns>
    protected virtual bool TryCreateErrorResponse(ProblemDetails problem, out TResult result)
    {
        Type responseType;
        if (typeof(IOperationResult).IsAssignableFrom(typeof(TResult)))
        {
            if (typeof(TResult).IsGenericType) responseType = typeof(OperationResult<>).MakeGenericType(typeof(TResult).GetGenericArguments().First());
            else responseType = typeof(OperationResult);
        }
        else
        {
            result = default!;
            return false;
        }
        result = (TResult)Activator.CreateInstance(responseType, problem.Status, problem.Detail, problem.Errors)!;
        return true;
    }

}