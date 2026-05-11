using Jobuler.Application.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/push-subscriptions")]
[Authorize]
public class PushSubscriptionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PushSubscriptionsController(IMediator mediator) => _mediator = mediator;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<IActionResult> Subscribe(
        Guid spaceId, [FromBody] CreatePushSubscriptionRequest request, CancellationToken ct)
    {
        await _mediator.Send(new CreatePushSubscriptionCommand(
            spaceId, CurrentUserId, request.Endpoint, request.P256dh, request.Auth), ct);
        return StatusCode(201);
    }

    [HttpDelete]
    public async Task<IActionResult> Unsubscribe(
        Guid spaceId, [FromBody] DeletePushSubscriptionRequest request, CancellationToken ct)
    {
        await _mediator.Send(new DeletePushSubscriptionCommand(
            spaceId, CurrentUserId, request.Endpoint), ct);
        return NoContent();
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(Guid spaceId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPushSubscriptionStatusQuery(
            spaceId, CurrentUserId), ct);
        return Ok(result);
    }
}

// ── Request records ───────────────────────────────────────────────────────────

public record CreatePushSubscriptionRequest(string Endpoint, string P256dh, string Auth);
public record DeletePushSubscriptionRequest(string Endpoint);
