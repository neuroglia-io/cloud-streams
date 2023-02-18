using CloudStreams.Core.Data.Models;
using MediatR;

namespace CloudStreams.Core.Application;

/// <summary>
/// Defines the fundamentals of a service used to handle <see cref="ICommand"/>s
/// </summary>
/// <typeparam name="TCommand">The type of <see cref="ICommand"/>s to handle</typeparam>
public interface ICommandHandler<TCommand>
    : IRequestHandler<TCommand>
    where TCommand : ICommand
{



}

/// <summary>
/// Defines the fundamentals of a service used to handle <see cref="ICommand"/>s
/// </summary>
/// <typeparam name="TCommand">The type of <see cref="ICommand"/>s to handle</typeparam>
/// <typeparam name="TResult">The expected type of result</typeparam>
public interface ICommandHandler<TCommand, TResult>
    : IRequestHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{



}