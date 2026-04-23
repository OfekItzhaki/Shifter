using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Application.Scheduling.Queries;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/schedule-versions")]
[Authorize]
public class ScheduleVersionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public ScheduleVersionsController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>List all schedule versions for a space.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        Guid spaceId, [FromQuery] string? status, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        return Ok(await _mediator.Send(new GetScheduleVersionsQuery(spaceId, status), ct));
    }

    /// <summary>Get the current published schedule with full assignment list.</summary>
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(Guid spaceId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        var result = await _mediator.Send(new GetCurrentPublishedVersionQuery(spaceId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Get a specific version with its assignments and diff summary.</summary>
    [HttpGet("{versionId:guid}")]
    public async Task<IActionResult> Get(Guid spaceId, Guid versionId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        var result = await _mediator.Send(new GetScheduleVersionDetailQuery(spaceId, versionId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Publish a draft version. Requires schedule.publish permission.
    /// Archives the current published version automatically.
    /// </summary>
    [HttpPost("{versionId:guid}/publish")]
    public async Task<IActionResult> Publish(Guid spaceId, Guid versionId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SchedulePublish, ct);
        await _mediator.Send(new PublishVersionCommand(spaceId, versionId, CurrentUserId), ct);
        return NoContent();
    }

    /// <summary>
    /// Roll back to a previously published version.
    /// Creates a new draft version copying the target's assignments.
    /// The target version is never mutated.
    /// Requires schedule.rollback permission.
    /// </summary>
    [HttpPost("{versionId:guid}/rollback")]
    public async Task<IActionResult> Rollback(Guid spaceId, Guid versionId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ScheduleRollback, ct);
        var newVersionId = await _mediator.Send(
            new RollbackVersionCommand(spaceId, versionId, CurrentUserId), ct);
        return Ok(new { newVersionId });
    }
}

/// <summary>Personal missions controller — returns assignments for the current user.</summary>
[ApiController]
[Route("spaces/{spaceId:guid}/my-assignments")]
[Authorize]
public class MyAssignmentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public MyAssignmentsController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Get my assignments for a date range.
    /// range: today | week | month | year
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        Guid spaceId, [FromQuery] string range = "week", CancellationToken ct = default)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);

        var now = DateTime.UtcNow.Date;
        var (from, to) = range switch
        {
            "today" => (now, now.AddDays(1)),
            "month" => (now, now.AddMonths(1)),
            "year"  => (now, now.AddYears(1)),
            _       => (now, now.AddDays(7)) // week default
        };

        var result = await _mediator.Send(new GetMyAssignmentsQuery(spaceId, CurrentUserId, from, to), ct);
        return Ok(result);
    }
}
