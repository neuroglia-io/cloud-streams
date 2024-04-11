namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="Exception"/>s
/// </summary>
public static class ExceptionExtensions
{

    /// <summary>
    /// Converts the <see cref="Exception"/> into new <see cref="ProblemDetails"/>
    /// </summary>
    /// <param name="exception">The <see cref="Exception"/> to convert</param>
    /// <returns>New <see cref="ProblemDetails"/> that describe the <see cref="Exception"/></returns>
    public static ProblemDetails ToProblemDetails(this Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is ProblemDetailsException problemDetailsExceptions && problemDetailsExceptions.Problem != null) return problemDetailsExceptions.Problem;
        var statusCode = exception switch
        {
            HttpRequestException httpEx => httpEx.StatusCode.HasValue ? (int)httpEx.StatusCode : 500,
            _ => 500
        };
        return new ProblemDetails(new Uri(exception.GetType().FullName!, UriKind.Relative), exception.GetType().Name, statusCode, exception.Message);
    }

}