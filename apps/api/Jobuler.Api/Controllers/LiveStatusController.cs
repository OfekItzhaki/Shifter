using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.Queries;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/groups/{groupId:guid}/live-status")]
[Authorize]
public class LiveStatusController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public LiveStatusController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Returns the current live status of all members in a group.
    /// Status is derived from active presence windows (manual overrides) and
    /// the currently published schedule assignments.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        return Ok(await _mediator.Send(new GetGroupLiveStatusQuery(spaceId, groupId), ct));
    }
}
