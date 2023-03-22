namespace CloudStreams.Core.Application;

/// <summary>
/// Defines the fundamentals of an application command
/// </summary>
public interface ICommand
    : IRequest
{



}

/// <summary>
/// Defines the fundamentals of an application command
/// </summary>
/// <typeparam name="TResult">The expected type of result</typeparam>
public interface ICommand<TResult>
    : ICommand, IRequest<TResult>
{



}