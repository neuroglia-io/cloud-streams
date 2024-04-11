using CloudStreams.Core.Application.Queries.Gateways;

namespace CloudStreams.Core.Api.Controllers;

/// <summary>
/// Represents the <see cref="ResourceApiController{TResource}"/> used to manage <see cref="Gateway"/>s
/// </summary>
/// <inheritdoc/>
[Route("api/resources/v1/gateways")]
public class GatewaysController(IMediator mediator)
    : ClusterResourceApiController<Gateway>(mediator)
{

    /// <summary>
    /// Checks the health of the specified gateway service, if any
    /// </summary>
    /// <param name="name">Gets the name of the gateway to check the health of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("{name}/health")]
    public virtual async Task<IActionResult> CheckGatewayHealth(string name, CancellationToken cancellationToken = default)
    {
        return this.Process(await this.Mediator.ExecuteAsync(new CheckGatewayHealthQuery(name), cancellationToken).ConfigureAwait(false));
    }

}
