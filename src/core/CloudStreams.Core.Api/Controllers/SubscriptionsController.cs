namespace CloudStreams.Core.Api.Controllers;

/// <summary>
/// Represents the <see cref="ResourceApiController{TResource}"/> used to manage <see cref="Subscription"/>s
/// </summary>
/// <inheritdoc/>
[Route("api/resources/v1/subscriptions")]
public class SubscriptionsController(IMediator mediator)
    : ClusterResourceApiController<Subscription>(mediator)
{
}