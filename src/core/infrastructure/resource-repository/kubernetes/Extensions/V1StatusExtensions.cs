using CloudStreams.Core.Data.Models;
using k8s.Models;

namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="V1Status"/>es
/// </summary>
public static class V1StatusExtensions
{

    /// <summary>
    /// Converts the <see cref="V1Status"/> into a new <see cref="Response"/>
    /// </summary>
    /// <param name="status">The <see cref="V1Status"/> to convert</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response ToResponse(this V1Status status)
    {
        return new()
        {
            Status = status.Code!.Value,
            Title = status.Status,
            Detail = status.Message
        };
    }

    /// <summary>
    /// Converts the <see cref="V1Status"/> into a new <see cref="Response"/>
    /// </summary>
    /// <param name="status">The <see cref="V1Status"/> to convert</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static TResponse ToResponse<TResponse>(this V1Status status)
        where TResponse : Response, new()
    {
        return new()
        {
            Status = status.Code!.Value,
            Title = status.Status,
            Detail = status.Message
        };
    }

}
