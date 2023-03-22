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
