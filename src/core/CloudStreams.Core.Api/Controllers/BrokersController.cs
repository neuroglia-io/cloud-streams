namespace CloudStreams.Core.Api.Controllers;

/// <summary>
/// Represents the <see cref="ApiController"/> used to manage <see cref="Broker"/>s
/// </summary>
/// <inheritdoc/>
[Route("api/resources/v1/brokers")]
public class BrokersController(IMediator mediator)
    : ClusterResourceApiController<Broker>(mediator)
{
}
