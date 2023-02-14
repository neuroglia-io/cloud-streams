using CloudStreams.Data.Models;

namespace CloudStreams.Api;

/// <summary>
/// Defines the fundamentals of an application request
/// </summary>
public interface IRequest
    : MediatR.IRequest<Response>
{



}

/// <summary>
/// Defines the fundamentals of an application request
/// </summary>
/// <typeparam name="TResult">The expected type of result</typeparam>
public interface IRequest<TResult>
    : MediatR.IRequest<Response<TResult>>
{



}