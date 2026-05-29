using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Application.Scheduling.Queries;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/schedule-runs")]
[Authorize]
public class ScheduleRunsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;
    private readonly AppDbContext _db;

    public ScheduleRunsController(IMediator mediator, IPermissionService permissions, AppDbContext db)
    {
        _mediator = mediator;
        _permissions = permissions;
        _db = db;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Trigger a solver run. Returns the RunId immediately — solve happens asynchronously.
    /// Poll GET /schedule-runs/{runId} to check status.
    /// When groupId is provided, the solver only schedules that group's members and tasks.
    /// </summary>
    [HttpPost("trigger")]
    public async Task<IActionResult> Trigger(
        Guid spaceId, [FromBody] TriggerSolverRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            CurrentUserId, spaceId, Permissions.ScheduleRecalculate, ct);

        // Check space subscription — block if not active
        var spaceSub = await _db.SpaceSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SpaceId == spaceId, ct);

        if (spaceSub != null && !spaceSub.IsAccessGranted)
        {
            var msg = spaceSub.Status == Domain.Billing.SubscriptionStatus.Trialing
                ? "Your trial has expired. Upgrade to continue using the scheduler."
                : "Your subscription is not active. Please renew or upgrade.";
            return StatusCode(402, new { error = msg });
        }
        // If spaceSub is null, allow (no subscription record = grace period)

        // Validate trigger mode — only "standard" and "emergency" are accepted
        var mode = (req.TriggerMode ?? "standard").ToLowerInvariant();
        if (mode != "standard" && mode != "emergency")
            return BadRequest(new { error = $"Invalid trigger mode '{req.TriggerMode}'. Must be 'standard' or 'emergency'." });

        var runId = await _mediator.Send(
            new TriggerSolverCommand(spaceId, mode, CurrentUserId, req.GroupId, req.StartTime), ct);

        return Accepted(new { runId });
    }

    /// <summary>
    /// Trigger a schedule regeneration for a group. Returns the RunId immediately —
    /// the solver runs asynchronously. Poll GET /schedule-runs/{runId} to check status.
    /// </summary>
    [HttpPost("regenerate")]
    public async Task<IActionResult> Regenerate(
        Guid spaceId, [FromBody] RegenerateRequest request, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            CurrentUserId, spaceId, Permissions.ScheduleRecalculate, ct);

        var runId = await _mediator.Send(
            new TriggerRegenerationCommand(spaceId, request.GroupId, CurrentUserId), ct);

        return Accepted(new { runId });
    }

    /// <summary>Poll the status of a solver run.</summary>
    [HttpGet("{runId:guid}")]
    public async Task<IActionResult> GetRun(Guid spaceId, Guid runId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceAdminMode, ct);
        var result = await _mediator.Send(new GetScheduleRunQuery(spaceId, runId), ct);
        return result is null ? NotFound() : Ok(result);
    }
}

public record TriggerSolverRequest(string? TriggerMode, Guid? GroupId = null, DateTime? StartTime = null);
public record RegenerateRequest(Guid GroupId);
