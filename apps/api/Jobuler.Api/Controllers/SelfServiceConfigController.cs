using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.SelfService.Commands;
using Jobuler.Application.Scheduling.SelfService.Queries;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/groups/{groupId:guid}/self-service-config")]
[Authorize]
public class SelfServiceConfigController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public SelfServiceConfigController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Get self-service scheduling configuration for a group (returns defaults if none saved).</summary>
    [HttpGet]
    public async Task<IActionResult> Get(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);

        var result = await _mediator.Send(new GetSelfServiceConfigQuery(spaceId, groupId), ct);

        if (result is null)
        {
            // Return defaults when no config has been saved yet
            return Ok(new SelfServiceConfigResponse(
                Id: null,
                GroupId: groupId,
                MinShiftsPerCycle: 0,
                MaxShiftsPerCycle: 7,
                RequestWindowOpenOffsetHours: 168,
                RequestWindowCloseOffsetHours: 24,
                CancellationCutoffHours: 24,
                MaxLateCancellationsPerCycle: 2,
                LateCancellationWindowHours: 24,
                WaitlistOfferMinutes: 60,
                CycleDurationDays: 7,
                AllowMemberShiftClaims: true,
                AllowWaitlist: true,
                AllowShiftChangeRequests: true,
                AllowAbsenceReports: true,
                AllowShiftSwaps: true));
        }

        return Ok(new SelfServiceConfigResponse(
            Id: result.Id,
            GroupId: result.GroupId,
            MinShiftsPerCycle: result.MinShiftsPerCycle,
            MaxShiftsPerCycle: result.MaxShiftsPerCycle,
            RequestWindowOpenOffsetHours: result.RequestWindowOpenOffsetHours,
            RequestWindowCloseOffsetHours: result.RequestWindowCloseOffsetHours,
            CancellationCutoffHours: result.CancellationCutoffHours,
            MaxLateCancellationsPerCycle: result.MaxLateCancellationsPerCycle,
            LateCancellationWindowHours: result.LateCancellationWindowHours,
            WaitlistOfferMinutes: result.WaitlistOfferMinutes,
            CycleDurationDays: result.CycleDurationDays,
            AllowMemberShiftClaims: result.AllowMemberShiftClaims,
            AllowWaitlist: result.AllowWaitlist,
            AllowShiftChangeRequests: result.AllowShiftChangeRequests,
            AllowAbsenceReports: result.AllowAbsenceReports,
            AllowShiftSwaps: result.AllowShiftSwaps));
    }

    /// <summary>Create or update self-service scheduling configuration for a group.</summary>
    [HttpPut]
    public async Task<IActionResult> Update(
        Guid spaceId, Guid groupId,
        [FromBody] UpdateSelfServiceConfigRequest req,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);

        var result = await _mediator.Send(new UpdateSelfServiceConfigCommand(
            spaceId,
            groupId,
            req.MinShiftsPerCycle,
            req.MaxShiftsPerCycle,
            req.RequestWindowOpenOffsetHours,
            req.RequestWindowCloseOffsetHours,
            req.CancellationCutoffHours,
            req.MaxLateCancellationsPerCycle,
            req.LateCancellationWindowHours,
            req.WaitlistOfferMinutes,
            req.CycleDurationDays,
            req.AllowMemberShiftClaims,
            req.AllowWaitlist,
            req.AllowShiftChangeRequests,
            req.AllowAbsenceReports,
            req.AllowShiftSwaps), ct);

        return Ok(new SelfServiceConfigResponse(
            Id: result.Id,
            GroupId: result.GroupId,
            MinShiftsPerCycle: result.MinShiftsPerCycle,
            MaxShiftsPerCycle: result.MaxShiftsPerCycle,
            RequestWindowOpenOffsetHours: result.RequestWindowOpenOffsetHours,
            RequestWindowCloseOffsetHours: result.RequestWindowCloseOffsetHours,
            CancellationCutoffHours: result.CancellationCutoffHours,
            MaxLateCancellationsPerCycle: result.MaxLateCancellationsPerCycle,
            LateCancellationWindowHours: result.LateCancellationWindowHours,
            WaitlistOfferMinutes: result.WaitlistOfferMinutes,
            CycleDurationDays: result.CycleDurationDays,
            AllowMemberShiftClaims: result.AllowMemberShiftClaims,
            AllowWaitlist: result.AllowWaitlist,
            AllowShiftChangeRequests: result.AllowShiftChangeRequests,
            AllowAbsenceReports: result.AllowAbsenceReports,
            AllowShiftSwaps: result.AllowShiftSwaps));
    }
}

// --- Request DTOs ---

public record UpdateSelfServiceConfigRequest(
    int MinShiftsPerCycle,
    int MaxShiftsPerCycle,
    int RequestWindowOpenOffsetHours,
    int RequestWindowCloseOffsetHours,
    int CancellationCutoffHours,
    int MaxLateCancellationsPerCycle,
    int LateCancellationWindowHours,
    int WaitlistOfferMinutes,
    int CycleDurationDays,
    bool AllowMemberShiftClaims = true,
    bool AllowWaitlist = true,
    bool AllowShiftChangeRequests = true,
    bool AllowAbsenceReports = true,
    bool AllowShiftSwaps = true);

// --- Response DTOs ---

public record SelfServiceConfigResponse(
    Guid? Id,
    Guid GroupId,
    int MinShiftsPerCycle,
    int MaxShiftsPerCycle,
    int RequestWindowOpenOffsetHours,
    int RequestWindowCloseOffsetHours,
    int CancellationCutoffHours,
    int MaxLateCancellationsPerCycle,
    int LateCancellationWindowHours,
    int WaitlistOfferMinutes,
    int CycleDurationDays,
    bool AllowMemberShiftClaims,
    bool AllowWaitlist,
    bool AllowShiftChangeRequests,
    bool AllowAbsenceReports,
    bool AllowShiftSwaps);
