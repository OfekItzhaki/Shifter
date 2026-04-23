using Jobuler.Application.Common;
using Jobuler.Application.Groups.Commands;
using Jobuler.Application.Groups.Queries;
using Jobuler.Application.Scheduling.Queries;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Authorize]
public class GroupsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public GroupsController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── Groups ────────────────────────────────────────────────────────────────

    [HttpGet("spaces/{spaceId:guid}/groups")]
    public async Task<IActionResult> ListGroups(Guid spaceId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        return Ok(await _mediator.Send(new GetGroupsQuery(spaceId), ct));
    }

    [HttpPost("spaces/{spaceId:guid}/groups")]
    public async Task<IActionResult> CreateGroup(Guid spaceId,
        [FromBody] CreateGroupRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        var id = await _mediator.Send(
            new CreateGroupCommand(spaceId, req.GroupTypeId, req.Name, req.Description), ct);
        return Created("", new { id });
    }

    [HttpPatch("spaces/{spaceId:guid}/groups/{groupId:guid}/settings")]
    public async Task<IActionResult> UpdateSettings(Guid spaceId, Guid groupId,
        [FromBody] UpdateGroupSettingsRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        await _mediator.Send(new UpdateGroupSettingsCommand(spaceId, groupId, req.SolverHorizonDays), ct);
        return NoContent();
    }

    // ── Members ───────────────────────────────────────────────────────────────

    [HttpGet("spaces/{spaceId:guid}/groups/{groupId:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        return Ok(await _mediator.Send(new GetGroupMembersQuery(spaceId, groupId), ct));
    }

    [HttpPost("spaces/{spaceId:guid}/groups/{groupId:guid}/members/by-email")]
    public async Task<IActionResult> AddMemberByEmail(Guid spaceId, Guid groupId,
        [FromBody] AddMemberByEmailRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        var result = await _mediator.Send(
            new AddPersonByEmailCommand(spaceId, groupId, req.Email, CurrentUserId), ct);
        return Ok(result);
    }

    [HttpDelete("spaces/{spaceId:guid}/groups/{groupId:guid}/members/{personId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid spaceId, Guid groupId, Guid personId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        await _mediator.Send(new RemovePersonFromGroupCommand(spaceId, groupId, personId), ct);
        return NoContent();
    }

    // ── Group Schedule ────────────────────────────────────────────────────────

    [HttpGet("spaces/{spaceId:guid}/groups/{groupId:guid}/schedule")]
    public async Task<IActionResult> GetGroupSchedule(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        return Ok(await _mediator.Send(new GetGroupScheduleQuery(spaceId, groupId), ct));
    }

    // ── Group Types (kept for compatibility) ──────────────────────────────────

    [HttpGet("spaces/{spaceId:guid}/group-types")]
    public async Task<IActionResult> ListGroupTypes(Guid spaceId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        return Ok(await _mediator.Send(new GetGroupTypesQuery(spaceId), ct));
    }

    [HttpPost("spaces/{spaceId:guid}/group-types")]
    public async Task<IActionResult> CreateGroupType(Guid spaceId,
        [FromBody] CreateGroupTypeRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        var id = await _mediator.Send(new CreateGroupTypeCommand(spaceId, req.Name, req.Description), ct);
        return Created("", new { id });
    }
}

// ── Request records ───────────────────────────────────────────────────────────

public record CreateGroupTypeRequest(string Name, string? Description);
public record CreateGroupRequest(Guid? GroupTypeId, string Name, string? Description);
public record AddMemberByEmailRequest(string Email);
public record UpdateGroupSettingsRequest(int SolverHorizonDays);
