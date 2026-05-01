using Jobuler.Application.Common;
using Jobuler.Application.Groups.Commands;
using Jobuler.Application.Groups.Queries;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/groups/{groupId:guid}/roles")]
[Authorize]
public class GroupRolesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public GroupRolesController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>List all roles for a group.</summary>
    [HttpGet]
    public async Task<IActionResult> List(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        return Ok(await _mediator.Send(new GetGroupRolesQuery(spaceId, groupId), ct));
    }

    /// <summary>Create a new role scoped to this group.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        Guid spaceId, Guid groupId,
        [FromBody] GroupRoleRequest req,
        CancellationToken ct)
    {
        var id = await _mediator.Send(
            new CreateGroupRoleCommand(spaceId, groupId, req.Name, req.Description,
                req.PermissionLevel ?? "view", CurrentUserId), ct);
        return Created($"/spaces/{spaceId}/groups/{groupId}/roles/{id}", new { id });
    }

    /// <summary>Update a group role's name, description, and permission level.</summary>
    [HttpPut("{roleId:guid}")]
    public async Task<IActionResult> Update(
        Guid spaceId, Guid groupId, Guid roleId,
        [FromBody] GroupRoleRequest req,
        CancellationToken ct)
    {
        await _mediator.Send(
            new UpdateGroupRoleCommand(spaceId, groupId, roleId, req.Name, req.Description,
                req.PermissionLevel ?? "view", CurrentUserId), ct);
        return NoContent();
    }

    /// <summary>Deactivate a group role (soft delete).</summary>
    [HttpDelete("{roleId:guid}")]
    public async Task<IActionResult> Deactivate(
        Guid spaceId, Guid groupId, Guid roleId,
        CancellationToken ct)
    {
        await _mediator.Send(
            new DeactivateGroupRoleCommand(spaceId, groupId, roleId, CurrentUserId), ct);
        return NoContent();
    }
}

public record GroupRoleRequest(string Name, string? Description, string? PermissionLevel);
