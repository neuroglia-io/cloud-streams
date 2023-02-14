namespace CloudStreams.Api;

/// <summary>
/// Defines the fundamentals of an application query
/// </summary>
/// <typeparam name="TResult">The expected type of result</typeparam>
public interface IQuery<TResult>
    : IRequest<TResult>
{



}
