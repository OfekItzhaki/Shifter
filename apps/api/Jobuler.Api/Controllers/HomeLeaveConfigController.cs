using Jobuler.Application.Common;
using Jobuler.Application.HomeLeave;
using Jobuler.Application.HomeLeave.Commands;
using Jobuler.Application.HomeLeave.Queries;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/groups/{groupId:guid}/home-leave-config")]
[Authorize]
public class HomeLeaveConfigController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;
    private readonly IOptimalRatioCalculator _optimalRatioCalculator;
    private readonly IFeasibilityEngine _feasibilityEngine;

    public HomeLeaveConfigController(
        IMediator mediator,
        IPermissionService permissions,
        IOptimalRatioCalculator optimalRatioCalculator,
        IFeasibilityEngine feasibilityEngine)
    {
        _mediator = mediator;
        _permissions = permissions;
        _optimalRatioCalculator = optimalRatioCalculator;
        _feasibilityEngine = feasibilityEngine;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Get home-leave configuration for a group (returns defaults if none saved).
    /// Includes computed optimal ratio in the response.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);

        var result = await _mediator.Send(new GetHomeLeaveConfigQuery(spaceId, groupId), ct);

        // Compute optimal ratio for the response
        var memberCount = await _mediator.Send(new GetGroupMemberCountQuery(spaceId, groupId), ct);
        OptimalRatioResult? optimalRatio = null;

        if (memberCount >= 2)
        {
            var coverageRequirement = Math.Max(1, memberCount - result.LeaveCapacity);
            optimalRatio = _optimalRatioCalculator.Calculate(
                memberCount,
                result.LeaveCapacity,
                result.LeaveDurationHours,
                coverageRequirement);
        }

        return Ok(new HomeLeaveConfigResponse(
            Id: result.Id,
            GroupId: result.GroupId,
            SpaceId: result.SpaceId,
            Mode: result.Mode,
            BaseDays: result.BaseDays,
            HomeDays: result.HomeDays,
            LeaveDurationHours: result.LeaveDurationHours,
            LeaveCapacity: result.LeaveCapacity,
            BalanceValue: result.BalanceValue,
            EmergencyFreezeActive: result.EmergencyFreezeActive,
            EmergencyUseForScheduling: result.EmergencyUseForScheduling,
            FreezeStartedAt: result.FreezeStartedAt,
            OptimalBaseDays: optimalRatio?.BaseDays ?? result.BaseDays,
            OptimalHomeDays: optimalRatio?.HomeDays ?? result.HomeDays,
            OptimalIsReduced: optimalRatio?.IsReduced ?? false));
    }

    /// <summary>Create or update home-leave configuration for a group.</summary>
    [HttpPut]
    public async Task<IActionResult> Upsert(
        Guid spaceId, Guid groupId,
        [FromBody] UpsertHomeLeaveConfigRequest req,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);

        var result = await _mediator.Send(new UpsertHomeLeaveConfigCommand(
            spaceId,
            groupId,
            MinRestHours: 0, // New mode system always uses 0
            EligibilityThresholdHours: (req.BaseDays ?? 7) * 24,
            req.LeaveCapacity,
            req.LeaveDurationHours,
            CurrentUserId,
            BalanceValue: null,
            req.Mode,
            req.BaseDays,
            req.HomeDays,
            req.SliderValue,
            req.EmergencyFreezeActive,
            req.EmergencyUseForScheduling), ct);

        return Ok(result);
    }

    /// <summary>Get the computed optimal ratio for the group based on current member count and coverage requirements.</summary>
    [HttpGet("optimal-ratio")]
    public async Task<IActionResult> GetOptimalRatio(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);

        var config = await _mediator.Send(new GetHomeLeaveConfigQuery(spaceId, groupId), ct);
        var memberCount = await _mediator.Send(new GetGroupMemberCountQuery(spaceId, groupId), ct);

        if (memberCount < 2)
            return BadRequest(new { message = "Group must have at least 2 members for home-leave" });

        var coverageRequirement = Math.Max(1, memberCount - config.LeaveCapacity);

        var optimalRatio = _optimalRatioCalculator.Calculate(
            memberCount,
            config.LeaveCapacity,
            config.LeaveDurationHours,
            coverageRequirement);

        return Ok(new OptimalRatioResponse(
            BaseDays: optimalRatio.BaseDays,
            HomeDays: optimalRatio.HomeDays,
            IsReduced: optimalRatio.IsReduced,
            MemberCount: memberCount,
            CoverageRequirement: coverageRequirement));
    }

    /// <summary>Preview the impact of a ratio/mode change without persisting.
    /// Returns solver preview alongside feasibility result.</summary>
    [HttpPost("~/spaces/{spaceId:guid}/groups/{groupId:guid}/home-leave-preview")]
    public async Task<IActionResult> Preview(
        Guid spaceId, Guid groupId,
        [FromBody] HomeLeavePreviewRequest req,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);

        // Determine effective balance value based on mode
        int effectiveBalanceValue;
        if (req.Mode == HomeLeaveMode.Automatic && req.SliderValue.HasValue)
        {
            effectiveBalanceValue = req.SliderValue.Value;
        }
        else if (req.Mode == HomeLeaveMode.Manual)
        {
            effectiveBalanceValue = 50; // Neutral for manual mode
        }
        else
        {
            effectiveBalanceValue = req.SliderValue ?? 50;
        }

        if (effectiveBalanceValue < 0 || effectiveBalanceValue > 100)
            return BadRequest(new { message = "balance_value must be between 0 and 100" });

        // Compute feasibility
        var memberCount = await _mediator.Send(new GetGroupMemberCountQuery(spaceId, groupId), ct);
        FeasibilityResult? feasibility = null;

        if (memberCount >= 2)
        {
            var config = await _mediator.Send(new GetHomeLeaveConfigQuery(spaceId, groupId), ct);
            var effectiveBaseDays = req.BaseDays ?? config.BaseDays;
            var effectiveHomeDays = req.HomeDays ?? config.HomeDays;
            var coverageRequirement = Math.Max(1, memberCount - config.LeaveCapacity);

            feasibility = _feasibilityEngine.Evaluate(
                memberCount,
                config.LeaveCapacity,
                effectiveBaseDays,
                effectiveHomeDays,
                coverageRequirement);
        }

        // Call solver preview
        var solverResult = await _mediator.Send(new PreviewHomeLeaveCommand(
            spaceId, groupId, effectiveBalanceValue, CurrentUserId), ct);

        return Ok(new HomeLeavePreviewResponse(
            Preview: solverResult,
            Feasibility: feasibility));
    }

    /// <summary>
    /// Cancel a home-leave presence window for a person.
    /// If starts_at is in the future: deletes the window entirely.
    /// If starts_at is in the past and ends_at is in the future: truncates to current timestamp.
    /// Requires schedule.publish permission.
    /// </summary>
    [HttpDelete("~/spaces/{spaceId:guid}/home-leave-presence/{presenceWindowId:guid}")]
    public async Task<IActionResult> CancelHomeLeave(
        Guid spaceId,
        Guid presenceWindowId,
        [FromQuery] Guid personId,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new CancelHomeLeaveCommand(
            spaceId,
            personId,
            presenceWindowId,
            CurrentUserId), ct);

        return Ok(result);
    }
}

// --- Request DTOs ---

public record UpsertHomeLeaveConfigRequest(
    HomeLeaveMode Mode,
    int? BaseDays,
    int? HomeDays,
    int? SliderValue,
    decimal LeaveDurationHours,
    int LeaveCapacity,
    bool? EmergencyFreezeActive,
    bool? EmergencyUseForScheduling);

public record HomeLeavePreviewRequest(
    HomeLeaveMode Mode,
    int? BaseDays,
    int? HomeDays,
    int? SliderValue,
    decimal? LeaveDurationHours);

// --- Response DTOs ---

public record HomeLeaveConfigResponse(
    Guid Id,
    Guid GroupId,
    Guid SpaceId,
    string Mode,
    int BaseDays,
    int HomeDays,
    decimal LeaveDurationHours,
    int LeaveCapacity,
    int BalanceValue,
    bool EmergencyFreezeActive,
    bool EmergencyUseForScheduling,
    DateTime? FreezeStartedAt,
    int OptimalBaseDays,
    int OptimalHomeDays,
    bool OptimalIsReduced);

public record OptimalRatioResponse(
    int BaseDays,
    int HomeDays,
    bool IsReduced,
    int MemberCount,
    int CoverageRequirement);

public record HomeLeavePreviewResponse(
    Application.HomeLeave.Commands.HomeLeavePreviewResponse Preview,
    FeasibilityResult? Feasibility);
