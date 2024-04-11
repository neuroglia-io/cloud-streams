namespace CloudStreams.Core.Api;

/// <summary>
/// Represents the base class for all <see cref="ApiController"/>s
/// </summary>
/// <remarks>
/// Initializes a new <see cref="ApiController"/>
/// </remarks>
/// <param name="mediator">The service used to mediate calls</param>
public abstract class ApiController(IMediator mediator)
    : ControllerBase
{

    /// <summary>
    /// Gets the service used to mediate calls
    /// </summary>
    protected IMediator Mediator { get; } = mediator;

}