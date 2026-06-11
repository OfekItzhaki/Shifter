using Jobuler.Application.Spaces.Commands;
using Jobuler.Application.Spaces.Queries;
using Jobuler.Application.Common;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces")]
[Authorize]
public class SpacesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public SpacesController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>List all spaces the current user belongs to.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMySpaces(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMySpacesQuery(CurrentUserId), ct);
        return Ok(result);
    }

    /// <summary>Get a single space by ID (extended detail).</summary>
    [HttpGet("{spaceId:guid}")]
    public async Task<IActionResult> GetSpace(Guid spaceId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSpaceDetailQuery(spaceId, CurrentUserId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Create a new space. The requesting user becomes the owner.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateSpace([FromBody] CreateSpaceRequest req, CancellationToken ct)
    {
        var spaceId = await _mediator.Send(
            new CreateSpaceCommand(req.Name, req.Description, req.Locale ?? "he", CurrentUserId), ct);
        return CreatedAtAction(nameof(GetSpace), new { spaceId }, new { spaceId });
    }

    /// <summary>Update space settings (owner only).</summary>
    [HttpPut("{spaceId:guid}")]
    public async Task<IActionResult> UpdateSpace(Guid spaceId, [FromBody] UpdateSpaceRequest req, CancellationToken ct)
    {
        await _mediator.Send(
            new UpdateSpaceCommand(spaceId, req.Name, req.Description, req.Locale, CurrentUserId), ct);
        return NoContent();
    }

    /// <summary>Join a space via invite code.</summary>
    [HttpPost("join")]
    public async Task<IActionResult> JoinSpace([FromBody] JoinSpaceRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new JoinSpaceByInviteCodeCommand(req.InviteCode, CurrentUserId), ct);
        return Ok(result);
    }

    /// <summary>Regenerate the space invite code (owner only).</summary>
    [HttpPost("{spaceId:guid}/invite-code/regenerate")]
    public async Task<IActionResult> RegenerateInviteCode(Guid spaceId, CancellationToken ct)
    {
        var newCode = await _mediator.Send(
            new RegenerateSpaceInviteCodeCommand(spaceId, CurrentUserId), ct);
        return Ok(new { inviteCode = newCode });
    }

    /// <summary>Get space members.</summary>
    [HttpGet("{spaceId:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid spaceId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSpaceMembersQuery(spaceId), ct);
        return Ok(result);
    }

    /// <summary>Trigger migration for existing users without spaces.</summary>
    [HttpPost("migrate")]
    public async Task<IActionResult> MigrateUser(CancellationToken ct)
    {
        var result = await _mediator.Send(new MigrateUserSpaceCommand(CurrentUserId), ct);
        return Ok(result);
    }

    /// <summary>Transfer space ownership to another user.</summary>
    [HttpPost("{spaceId:guid}/transfer-ownership")]
    public async Task<IActionResult> TransferOwnership(
        Guid spaceId, [FromBody] TransferOwnershipRequest req, CancellationToken ct)
    {
        await _mediator.Send(
            new TransferOwnershipCommand(spaceId, req.TargetUserId, CurrentUserId, req.Reason), ct);
        return NoContent();
    }

    /// <summary>Soft-delete a space (owner only). Cascades to all groups.</summary>
    [HttpDelete("{spaceId:guid}")]
    public async Task<IActionResult> SoftDeleteSpace(Guid spaceId, CancellationToken ct)
    {
        await _mediator.Send(new SoftDeleteSpaceCommand(spaceId, CurrentUserId), ct);
        return NoContent();
    }

    /// <summary>Restore a previously soft-deleted space (owner only).</summary>
    [HttpPost("{spaceId:guid}/restore")]
    public async Task<IActionResult> RestoreSpace(Guid spaceId, CancellationToken ct)
    {
        await _mediator.Send(new RestoreSpaceCommand(spaceId, CurrentUserId), ct);
        return NoContent();
    }

    /// <summary>Assign a permission level to a space member.</summary>
    [HttpPut("{spaceId:guid}/members/{userId:guid}/role")]
    public async Task<IActionResult> AssignRole(
        Guid spaceId, Guid userId, [FromBody] AssignSpaceRoleRequest req, CancellationToken ct)
    {
        await _mediator.Send(
            new AssignSpaceRoleCommand(spaceId, userId, req.Level, CurrentUserId), ct);
        return NoContent();
    }

    /// <summary>Get all space members with their assigned permission levels.</summary>
    [HttpGet("{spaceId:guid}/members/roles")]
    public async Task<IActionResult> GetMemberRoles(Guid spaceId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSpacePermissionLevelsQuery(spaceId), ct);
        return Ok(result);
    }

    /// <summary>Check whether the current user has a permission in this space.</summary>
    [HttpGet("{spaceId:guid}/permissions/{permissionKey}")]
    public async Task<IActionResult> HasPermission(Guid spaceId, string permissionKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
            return BadRequest(new { error = "Permission key is required." });

        var hasPermission = await _permissions.HasPermissionAsync(CurrentUserId, spaceId, permissionKey, ct);
        return Ok(new CurrentUserPermissionResponse(permissionKey, hasPermission));
    }

    /// <summary>Update the space-level management timeout (owner only).</summary>
    [HttpPut("{spaceId:guid}/management-timeout")]
    public async Task<IActionResult> UpdateManagementTimeout(
        Guid spaceId, [FromBody] UpdateManagementTimeoutRequest req, CancellationToken ct)
    {
        await _mediator.Send(
            new UpdateManagementTimeoutCommand(spaceId, req.Minutes, CurrentUserId), ct);
        return NoContent();
    }

    /// <summary>Update the space-level home-leave configuration (owner only).</summary>
    [HttpPut("{spaceId:guid}/home-leave-config")]
    public async Task<IActionResult> UpdateHomeLeaveConfig(
        Guid spaceId, [FromBody] UpdateHomeLeaveConfigRequest req, CancellationToken ct)
    {
        await _mediator.Send(new UpdateSpaceHomeLeaveConfigCommand(
            spaceId,
            CurrentUserId,
            req.Mode,
            req.BalanceValue,
            req.BaseDays,
            req.HomeDays,
            req.MinPeopleAtBase,
            req.MinRestHours,
            req.EligibilityThresholdHours,
            req.LeaveCapacity,
            req.LeaveDurationHours,
            req.EmergencyFreezeActive,
            req.EmergencyUseForScheduling), ct);
        return NoContent();
    }

    /// <summary>Get the space-level home-leave configuration.</summary>
    [HttpGet("{spaceId:guid}/home-leave-config")]
    public async Task<IActionResult> GetHomeLeaveConfig(Guid spaceId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSpaceHomeLeaveConfigQuery(spaceId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Get the space-level self-service default policy template.</summary>
    [HttpGet("{spaceId:guid}/self-service-defaults")]
    public async Task<IActionResult> GetSelfServiceDefaults(Guid spaceId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSpaceSelfServiceDefaultsQuery(spaceId), ct);
        return Ok(result);
    }

    /// <summary>Update the space-level self-service default policy template (owner only).</summary>
    [HttpPut("{spaceId:guid}/self-service-defaults")]
    public async Task<IActionResult> UpdateSelfServiceDefaults(
        Guid spaceId, [FromBody] UpdateSpaceSelfServiceDefaultsRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateSpaceSelfServiceDefaultsCommand(
            spaceId,
            CurrentUserId,
            req.MinShiftsPerCycle,
            req.MaxShiftsPerCycle,
            req.RequestWindowOpenOffsetHours,
            req.RequestWindowCloseOffsetHours,
            req.CancellationCutoffHours,
            req.MaxAbsencesPerCycle,
            req.MaxLateCancellationsPerCycle,
            req.LateCancellationWindowHours,
            req.WaitlistOfferMinutes,
            req.CycleDurationDays,
            req.AllowMemberShiftClaims,
            req.AllowWaitlist,
            req.AllowShiftChangeRequests,
            req.AllowAbsenceReports,
            req.AllowShiftSwaps), ct);

        return Ok(result);
    }

    /// <summary>Regenerate the space invite code (owner only). Alternative route.</summary>
    [HttpPost("{spaceId:guid}/regenerate-invite-code")]
    public async Task<IActionResult> RegenerateInviteCodeAlt(Guid spaceId, CancellationToken ct)
    {
        var newCode = await _mediator.Send(
            new RegenerateSpaceInviteCodeCommand(spaceId, CurrentUserId), ct);
        return Ok(new { inviteCode = newCode });
    }
}

public record CreateSpaceRequest(string Name, string? Description, string? Locale);
public record UpdateSpaceRequest(string Name, string? Description, string Locale);
public record JoinSpaceRequest(string InviteCode);
public record TransferOwnershipRequest(Guid TargetUserId, string? Reason);
public record AssignSpaceRoleRequest(SpacePermissionLevel Level);
public record CurrentUserPermissionResponse(string PermissionKey, bool HasPermission);
public record UpdateManagementTimeoutRequest(int Minutes);
public record UpdateHomeLeaveConfigRequest(
    HomeLeaveMode Mode,
    int BalanceValue,
    int BaseDays,
    int HomeDays,
    int MinPeopleAtBase,
    decimal MinRestHours,
    decimal EligibilityThresholdHours,
    int LeaveCapacity,
    decimal LeaveDurationHours,
    bool EmergencyFreezeActive,
    bool EmergencyUseForScheduling);
public record UpdateSpaceSelfServiceDefaultsRequest(
    int MinShiftsPerCycle,
    int MaxShiftsPerCycle,
    int RequestWindowOpenOffsetHours,
    int RequestWindowCloseOffsetHours,
    int CancellationCutoffHours,
    int MaxAbsencesPerCycle,
    int MaxLateCancellationsPerCycle,
    int LateCancellationWindowHours,
    int WaitlistOfferMinutes,
    int CycleDurationDays,
    bool AllowMemberShiftClaims,
    bool AllowWaitlist,
    bool AllowShiftChangeRequests,
    bool AllowAbsenceReports,
    bool AllowShiftSwaps);
