using Jobuler.Application.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator) => _mediator = mediator;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List(
        Guid spaceId, [FromQuery] bool unreadOnly = false, CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetNotificationsQuery(spaceId, CurrentUserId, unreadOnly), ct);
        return Ok(result);
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> Dismiss(
        Guid spaceId, Guid notificationId, CancellationToken ct = default)
    {
        await _mediator.Send(
            new DismissNotificationCommand(spaceId, CurrentUserId, notificationId), ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> DismissAll(
        Guid spaceId, CancellationToken ct = default)
    {
        await _mediator.Send(
            new DismissAllNotificationsCommand(spaceId, CurrentUserId), ct);
        return NoContent();
    }
}
