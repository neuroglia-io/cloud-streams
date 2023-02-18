namespace CloudStreams.Core.Application;

/// <summary>
/// Defines the fundamentals of a service used to handle <see cref="IQuery{TResult}"/> instances
/// </summary>
/// <typeparam name="TQuery">The type of <see cref="IQuery{TResult}"/>s to handle</typeparam>
/// <typeparam name="TResult">The expected type of result</typeparam>
public interface IQueryHandler<TQuery, TResult>
    : IRequestHandler<TQuery, TResult>
     where TQuery : IQuery<TResult>
{



}
