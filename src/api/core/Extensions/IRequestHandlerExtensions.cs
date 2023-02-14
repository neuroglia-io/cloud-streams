using CloudStreams.Data.Models;

namespace CloudStreams.Api;

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
    /// <param name="request">The extended request handler</param>
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
    /// <returns>A new <see cref="Response"/></returns>
    public static Response ValidationFailed<TRequest>(this IRequestHandler<TRequest> handler, ValidationResults? validationResults = null)
        where TRequest : IRequest
    {
        return Response.ValidationFailed(validationResults);
    }

    /// <summary>
    /// Creates a new <see cref="Response"/> that describes failure due to validation problems
    /// </summary>
    /// <typeparam name="TRequest">The type of request to handle</typeparam>
    /// <typeparam name="TResult">The expected type of result</typeparam>
    /// <param name="handler">The extended request handler</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response ValidationFailed<TRequest, TResult>(this IRequestHandler<TRequest, TResult> handler, ValidationResults? validationResults = null)
        where TRequest : IRequest<TResult>
    {
        return Response.ValidationFailed(validationResults);
    }

}
