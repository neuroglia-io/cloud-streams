namespace CloudStreams.Gateway.Api;

/// <summary>
/// Represents the base class for all Cloud Streams <see cref="ControllerBase"/> implementations
/// </summary>
public abstract class ApiController
    : ControllerBase
{

    /// <summary>
    /// Initializes a new <see cref="ApiController"/>
    /// </summary>
    /// <param name="mediator">The service used to mediate calls</param>
    protected ApiController(IMediator mediator)
    {
        this.Mediator = mediator;
    }

    /// <summary>
    /// Gets the service used to mediate calls
    /// </summary>
    protected IMediator Mediator { get; }

    /// <summary>
    /// Processes the specified <see cref="Response"/>
    /// </summary>
    /// <param name="response">The <see cref="Response"/> to process</param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    protected virtual IActionResult Process(Response response)
    {
        if (response.IsSuccessStatusCode()) return new ObjectResult(response.Content) { StatusCode = response.Status };
        return new ObjectResult(response) { StatusCode = response.Status };
    }

}
