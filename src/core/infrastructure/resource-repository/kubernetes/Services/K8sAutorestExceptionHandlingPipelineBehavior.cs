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