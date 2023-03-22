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

namespace CloudStreams.Core.Application;

/// <summary>
/// Defines extensions for <see cref="IRequestHandler{TRequest}"/> implementations
/// </summary>
public static class IRequestHandlerExtensions
{

    /// <summary>
    /// Creates a new <see cref="Response"/> that describes a successfull operation
    /// </summary>
    /// <typeparam name="TRequest">The type of request to handle</typeparam>
    /// <param name="handler">The extended request handler</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response Accepted<TRequest>(this IRequestHandler<TRequest> handler)
        where TRequest : IRequest
    {
        return Response.Accepted();
    }

    /// <summary>
    /// Creates a new <see cref="Response"/> that describes failure due to a forbidden operation
    /// </summary>
    /// <typeparam name="TRequest">The type of request to handle</typeparam>
    /// <param name="handler">The extended request handler</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response Forbidden<TRequest>(this IRequestHandler<TRequest> handler)
        where TRequest : IRequest
    {
        return Response.Forbidden();
    }

    /// <summary>
    /// Creates a new <see cref="Response"/> that describes a successfull operation
    /// </summary>
    /// <typeparam name="TRequest">The type of request to handle</typeparam>
    /// <param name="handler">The extended request handler</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response Ok<TRequest>(this IRequestHandler<TRequest> handler)
        where TRequest : IRequest
    {
        return Response.Ok();
    }

    /// <summary>
    /// Creates a new <see cref="Response"/> that describes a successfull operation
    /// </summary>
    /// <typeparam name="TRequest">The type of request to handle</typeparam>
    /// <typeparam name="TResult">The expected type of result</typeparam>
    /// <param name="handler">The extended request handler</param>
    /// <param name="result">The result to return</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response<TResult> Ok<TRequest, TResult>(this IRequestHandler<TRequest, TResult> handler, TResult result)
        where TRequest : IRequest<TResult>
    {
        return Response.Ok(result);
    }

    /// <summary>
    /// Creates a new <see cref="Response"/> that describes a successfull operation
    /// </summary>
    /// <typeparam name="TRequest">The type of request to handle</typeparam>
    /// <typeparam name="TResult">The expected type of result</typeparam>
    /// <param name="handler">The extended request handler</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response<TResult> Ok<TRequest, TResult>(this IRequestHandler<TRequest, TResult> handler)
        where TRequest : IRequest<TResult>
    {
        return handler.Ok(default!);
    }

    /// <summary>
    /// Creates a new <see cref="Response"/> that describes failure due to validation problems
    /// </summary>
    /// <typeparam name="TRequest">The type of request to handle</typeparam>
    /// <param name="handler">The extended request handler</param>
    /// <param name="evaluationResults">The <see cref="EvaluationResults"/> used to describe the failed validation</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response ValidationFailed<TRequest>(this IRequestHandler<TRequest> handler, EvaluationResults evaluationResults)
        where TRequest : IRequest
    {
        return Response.ValidationFailed(evaluationResults);
    }

    /// <summary>
    /// Creates a new <see cref="Response"/> that describes failure due to validation problems
    /// </summary>
    /// <typeparam name="TRequest">The type of request to handle</typeparam>
    /// <typeparam name="TResult">The expected type of result</typeparam>
    /// <param name="handler">The extended request handler</param>
    /// <param name="evaluationResults">The <see cref="EvaluationResults"/> used to describe the failed validation</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response ValidationFailed<TRequest, TResult>(this IRequestHandler<TRequest, TResult> handler, EvaluationResults evaluationResults)
        where TRequest : IRequest<TResult>
    {
        return Response.ValidationFailed(evaluationResults);
    }

    /// <summary>
    /// Creates a new <see cref="Response"/> that describes failure due to validation problems
    /// </summary>
    /// <typeparam name="TRequest">The type of request to handle</typeparam>
    /// <typeparam name="TResult">The expected type of result</typeparam>
    /// <param name="handler">The extended request handler</param>
    /// <param name="errors">The errors that have occured during validation</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response<TResult> ValidationFailed<TRequest, TResult>(this IRequestHandler<TRequest, TResult> handler, params KeyValuePair<string, string[]>[] errors)
        where TRequest : IRequest<TResult>
    {
        return Response.ValidationFailed<TResult>(errors);
    }

}
