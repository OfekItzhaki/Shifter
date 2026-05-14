using Jobuler.Application.Common;
using Jobuler.Application.People.Commands;
using Jobuler.Application.People.Queries;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/people/{personId:guid}")]
[Authorize]
public class AvailabilityController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public AvailabilityController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailability(
        Guid spaceId, Guid personId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        return Ok(await _mediator.Send(new GetAvailabilityQuery(spaceId, personId), ct));
    }

    [HttpPost("availability")]
    public async Task<IActionResult> AddAvailability(
        Guid spaceId, Guid personId,
        [FromBody] AddAvailabilityRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        var id = await _mediator.Send(new AddAvailabilityWindowCommand(
            spaceId, personId, req.StartsAt, req.EndsAt, req.Note, CurrentUserId), ct);
        return Created("", new { id });
    }

    [HttpGet("presence")]
    public async Task<IActionResult> GetPresence(
        Guid spaceId, Guid personId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        return Ok(await _mediator.Send(new GetPresenceQuery(spaceId, personId), ct));
    }

    [HttpPost("presence")]
    public async Task<IActionResult> AddPresence(
        Guid spaceId, Guid personId,
        [FromBody] AddPresenceRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        var id = await _mediator.Send(new AddPresenceWindowCommand(
            spaceId, personId, req.State, req.StartsAt, req.EndsAt, req.Note, CurrentUserId,
            req.ReasonId), ct);
        return Created("", new { id });
    }
}

public record AddAvailabilityRequest(DateTime StartsAt, DateTime EndsAt, string? Note);
public record AddPresenceRequest(string State, DateTime StartsAt, DateTime EndsAt, string? Note, Guid? ReasonId = null);
