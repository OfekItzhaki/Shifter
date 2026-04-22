using Jobuler.Application.Common;
using Jobuler.Application.People.Commands;
using Jobuler.Application.People.Queries;
using Jobuler.Application.Spaces.Queries;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/people")]
[Authorize]
public class PeopleController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public PeopleController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List(Guid spaceId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        var result = await _mediator.Send(new GetPeopleQuery(spaceId), ct);
        return Ok(result);
    }

    [HttpGet("{personId:guid}")]
    public async Task<IActionResult> Get(Guid spaceId, Guid personId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        var includeSensitive = await _permissions.HasPermissionAsync(
            CurrentUserId, spaceId, Permissions.RestrictionsManageSensitive, ct);
        var result = await _mediator.Send(new GetPersonDetailQuery(spaceId, personId, includeSensitive), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid spaceId, [FromBody] CreatePersonRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        var id = await _mediator.Send(
            new CreatePersonCommand(spaceId, req.FullName, req.DisplayName, req.LinkedUserId, CurrentUserId), ct);
        return CreatedAtAction(nameof(Get), new { spaceId, personId = id }, new { id });
    }

    [HttpPut("{personId:guid}")]
    public async Task<IActionResult> Update(Guid spaceId, Guid personId,
        [FromBody] UpdatePersonRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        await _mediator.Send(new UpdatePersonCommand(
            spaceId, personId, req.FullName, req.DisplayName, req.ProfileImageUrl, CurrentUserId), ct);
        return NoContent();
    }

    [HttpPost("{personId:guid}/roles")]
    public async Task<IActionResult> AssignRole(Guid spaceId, Guid personId,
        [FromBody] AssignRoleRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        await _mediator.Send(new AssignRoleToPersonCommand(spaceId, personId, req.RoleId), ct);
        return NoContent();
    }

    [HttpDelete("{personId:guid}/roles/{roleId:guid}")]
    public async Task<IActionResult> RemoveRole(Guid spaceId, Guid personId, Guid roleId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        await _mediator.Send(new RemoveRoleFromPersonCommand(spaceId, personId, roleId), ct);
        return NoContent();
    }

    [HttpPost("{personId:guid}/restrictions")]
    public async Task<IActionResult> AddRestriction(Guid spaceId, Guid personId,
        [FromBody] AddRestrictionRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);

        // Sensitive reason requires elevated permission
        string? sensitiveReason = null;
        if (!string.IsNullOrWhiteSpace(req.SensitiveReason))
        {
            await _permissions.RequirePermissionAsync(
                CurrentUserId, spaceId, Permissions.RestrictionsManageSensitive, ct);
            sensitiveReason = req.SensitiveReason;
        }

        var id = await _mediator.Send(new AddRestrictionCommand(
            spaceId, personId, req.RestrictionType, req.TaskTypeId,
            req.EffectiveFrom, req.EffectiveUntil, req.OperationalNote,
            sensitiveReason, CurrentUserId), ct);

        return CreatedAtAction(nameof(Get), new { spaceId, personId }, new { id });
    }
}

public record CreatePersonRequest(string FullName, string? DisplayName, Guid? LinkedUserId);
public record UpdatePersonRequest(string FullName, string? DisplayName, string? ProfileImageUrl);
public record AssignRoleRequest(Guid RoleId);
public record AddRestrictionRequest(
    string RestrictionType, Guid? TaskTypeId,
    DateOnly EffectiveFrom, DateOnly? EffectiveUntil,
    string? OperationalNote, string? SensitiveReason);
