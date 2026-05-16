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

        var templateType = Domain.Groups.GroupTemplateType.Custom;
        if (!string.IsNullOrEmpty(req.TemplateType))
        {
            if (!Enum.TryParse<Domain.Groups.GroupTemplateType>(req.TemplateType, true, out templateType))
                throw new InvalidOperationException("Invalid template type.");
        }

        var id = await _mediator.Send(
            new CreateGroupCommand(spaceId, req.GroupTypeId, req.Name, req.Description, CurrentUserId, templateType), ct);
        return Created("", new { id });
    }

    [HttpPatch("spaces/{spaceId:guid}/groups/{groupId:guid}/settings")]
    public async Task<IActionResult> UpdateSettings(Guid spaceId, Guid groupId,
        [FromBody] UpdateGroupSettingsRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        await _mediator.Send(new UpdateGroupSettingsCommand(spaceId, groupId, req.SolverHorizonDays, req.SolverStartDateTime, req.AutoPublish, req.MinRestBetweenShiftsHours, req.AllowMembersViewHistory), ct);
        return NoContent();
    }

    [HttpPut("spaces/{spaceId:guid}/groups/{groupId:guid}")]
    public async Task<IActionResult> UpdateGroup(Guid spaceId, Guid groupId,
        [FromBody] UpdateGroupRequest req, CancellationToken ct)
    {
        if (req.IsClosedBase.HasValue)
        {
            await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);
            await _mediator.Send(new SetGroupClosedBaseCommand(spaceId, groupId, CurrentUserId, req.IsClosedBase.Value), ct);
        }

        if (!string.IsNullOrEmpty(req.TemplateType))
        {
            await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
            if (!Enum.TryParse<Domain.Groups.GroupTemplateType>(req.TemplateType, true, out var templateType))
                throw new InvalidOperationException("Invalid template type.");
            await _mediator.Send(new SetGroupTemplateTypeCommand(spaceId, groupId, CurrentUserId, templateType), ct);
        }

        return NoContent();
    }

    [HttpPatch("spaces/{spaceId:guid}/groups/{groupId:guid}/name")]
    public async Task<IActionResult> RenameGroup(Guid spaceId, Guid groupId,
        [FromBody] RenameGroupRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        await _mediator.Send(new RenameGroupCommand(spaceId, groupId, CurrentUserId, req.Name), ct);
        return NoContent();
    }

    [HttpDelete("spaces/{spaceId:guid}/groups/{groupId:guid}")]
    public async Task<IActionResult> DeleteGroup(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        await _mediator.Send(new SoftDeleteGroupCommand(spaceId, groupId, CurrentUserId), ct);
        return NoContent();
    }

    [HttpPost("spaces/{spaceId:guid}/groups/{groupId:guid}/restore")]
    public async Task<IActionResult> RestoreGroup(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        await _mediator.Send(new RestoreGroupCommand(spaceId, groupId, CurrentUserId), ct);
        return NoContent();
    }

    [HttpGet("spaces/{spaceId:guid}/groups/deleted")]
    public async Task<IActionResult> GetDeletedGroups(Guid spaceId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        return Ok(await _mediator.Send(new GetDeletedGroupsQuery(spaceId, CurrentUserId), ct));
    }

    [HttpPost("spaces/{spaceId:guid}/groups/{groupId:guid}/transfer")]
    public async Task<IActionResult> InitiateTransfer(Guid spaceId, Guid groupId,
        [FromBody] InitiateGroupTransferRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        await _mediator.Send(new InitiateOwnershipTransferCommand(spaceId, groupId, CurrentUserId, req.ProposedPersonId), ct);
        return NoContent();
    }

    [HttpDelete("spaces/{spaceId:guid}/groups/{groupId:guid}/transfer")]
    public async Task<IActionResult> CancelTransfer(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        await _mediator.Send(new CancelOwnershipTransferCommand(spaceId, groupId, CurrentUserId), ct);
        return NoContent();
    }

    [HttpGet("groups/confirm-transfer")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmTransfer([FromQuery] string token, CancellationToken ct)
    {
        await _mediator.Send(new ConfirmOwnershipTransferCommand(token), ct);
        return Ok(new { message = "Ownership transferred successfully." });
    }

    // ── Members ───────────────────────────────────────────────────────────────

    [HttpGet("spaces/{spaceId:guid}/groups/{groupId:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        return Ok(await _mediator.Send(new GetGroupMembersQuery(spaceId, groupId), ct));
    }

    [HttpPost("spaces/{spaceId:guid}/groups/{groupId:guid}/members")]
    public async Task<IActionResult> AddMemberById(Guid spaceId, Guid groupId,
        [FromBody] AddMemberByIdRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        await _mediator.Send(new AddPersonToGroupByIdCommand(spaceId, groupId, req.PersonId, CurrentUserId, req.RoleId), ct);
        return Ok(new { personId = req.PersonId });
    }

    [HttpPost("spaces/{spaceId:guid}/groups/{groupId:guid}/members/by-email")]
    public async Task<IActionResult> AddMemberByEmail(Guid spaceId, Guid groupId,
        [FromBody] AddMemberByEmailRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        var result = await _mediator.Send(
            new AddPersonByEmailCommand(spaceId, groupId, req.Email, CurrentUserId, req.RoleId), ct);
        return Ok(result);
    }

    [HttpPost("spaces/{spaceId:guid}/groups/{groupId:guid}/members/by-phone")]
    public async Task<IActionResult> AddMemberByPhone(Guid spaceId, Guid groupId,
        [FromBody] AddMemberByPhoneRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        var result = await _mediator.Send(
            new AddPersonByPhoneCommand(spaceId, groupId, req.PhoneNumber, CurrentUserId, req.RoleId), ct);
        return Ok(result);
    }

    [HttpDelete("spaces/{spaceId:guid}/groups/{groupId:guid}/members/{personId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid spaceId, Guid groupId, Guid personId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        await _mediator.Send(new RemovePersonFromGroupCommand(spaceId, groupId, personId), ct);
        return NoContent();
    }

    /// <summary>
    /// Assigns or replaces a member's group role. Group owner only.
    /// Send { "roleId": null } to remove the current role assignment.
    /// </summary>
    [HttpPatch("spaces/{spaceId:guid}/groups/{groupId:guid}/members/{personId:guid}/role")]
    public async Task<IActionResult> UpdateMemberRole(Guid spaceId, Guid groupId, Guid personId,
        [FromBody] UpdateMemberRoleRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        await _mediator.Send(new UpdateMemberRoleCommand(spaceId, groupId, personId, req.RoleId, CurrentUserId), ct);
        return NoContent();
    }

    // ── Home-leave priority ───────────────────────────────────────────────────

    [HttpPatch("spaces/{spaceId:guid}/groups/{groupId:guid}/members/{personId:guid}/home-leave-priority")]
    public async Task<IActionResult> SetHomeLeavePriority(
        Guid spaceId, Guid groupId, Guid personId,
        [FromBody] SetHomeLeavePriorityRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);

        var membership = await _mediator.Send(
            new Application.Groups.Commands.SetHomeLeavePriorityCommand(spaceId, groupId, personId, req.Priority), ct);
        return Ok(new { priority = membership });
    }

    // ── Group Schedule ────────────────────────────────────────────────────────

    [HttpGet("spaces/{spaceId:guid}/groups/{groupId:guid}/schedule")]
    public async Task<IActionResult> GetGroupSchedule(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        return Ok(await _mediator.Send(new GetGroupScheduleQuery(spaceId, groupId), ct));
    }

    // ── Group Messages ────────────────────────────────────────────────────────

    [HttpGet("spaces/{spaceId:guid}/groups/{groupId:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        return Ok(await _mediator.Send(new GetGroupMessagesQuery(spaceId, groupId), ct));
    }

    [HttpPost("spaces/{spaceId:guid}/groups/{groupId:guid}/messages")]
    public async Task<IActionResult> CreateMessage(Guid spaceId, Guid groupId,
        [FromBody] CreateGroupMessageRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        var id = await _mediator.Send(new CreateGroupMessageCommand(spaceId, groupId, CurrentUserId, req.Content, req.IsPinned), ct);
        return Created("", new { id });
    }

    [HttpDelete("spaces/{spaceId:guid}/groups/{groupId:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> DeleteMessage(Guid spaceId, Guid groupId, Guid messageId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        await _mediator.Send(new DeleteGroupMessageCommand(spaceId, groupId, messageId, CurrentUserId), ct);
        return NoContent();
    }

    // ── Group Alerts ──────────────────────────────────────────────────────────

    [HttpPost("spaces/{spaceId:guid}/groups/{groupId:guid}/alerts")]
    public async Task<IActionResult> CreateAlert(
        Guid spaceId, Guid groupId,
        [FromBody] CreateAlertRequest req, CancellationToken ct)
    {
        var id = await _mediator.Send(
            new CreateGroupAlertCommand(spaceId, groupId, CurrentUserId, req.Title, req.Body, req.Severity), ct);
        return CreatedAtAction(nameof(CreateAlert), new { id });
    }

    [HttpGet("spaces/{spaceId:guid}/groups/{groupId:guid}/alerts")]
    public async Task<IActionResult> GetAlerts(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        var alerts = await _mediator.Send(new GetGroupAlertsQuery(spaceId, groupId, CurrentUserId), ct);
        return Ok(alerts);
    }

    [HttpDelete("spaces/{spaceId:guid}/groups/{groupId:guid}/alerts/{alertId:guid}")]
    public async Task<IActionResult> DeleteAlert(
        Guid spaceId, Guid groupId, Guid alertId, CancellationToken ct)
    {
        await _mediator.Send(new DeleteGroupAlertCommand(spaceId, groupId, alertId, CurrentUserId), ct);
        return NoContent();
    }

    [HttpPut("spaces/{spaceId:guid}/groups/{groupId:guid}/alerts/{alertId:guid}")]
    public async Task<IActionResult> UpdateAlert(
        Guid spaceId, Guid groupId, Guid alertId,
        [FromBody] UpdateAlertRequest req, CancellationToken ct)
    {
        await _mediator.Send(new UpdateGroupAlertCommand(
            spaceId, groupId, alertId, CurrentUserId, req.Title, req.Body, req.Severity), ct);
        return NoContent();
    }

    [HttpPut("spaces/{spaceId:guid}/groups/{groupId:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> UpdateMessage(
        Guid spaceId, Guid groupId, Guid messageId,
        [FromBody] UpdateMessageRequest req, CancellationToken ct)
    {
        await _mediator.Send(new UpdateGroupMessageCommand(
            spaceId, groupId, messageId, CurrentUserId, req.Content), ct);
        return NoContent();
    }

    [HttpPatch("spaces/{spaceId:guid}/groups/{groupId:guid}/messages/{messageId:guid}/pin")]
    public async Task<IActionResult> PinMessage(
        Guid spaceId, Guid groupId, Guid messageId,
        [FromBody] PinMessageRequest req, CancellationToken ct)
    {
        await _mediator.Send(new PinGroupMessageCommand(
            spaceId, groupId, messageId, CurrentUserId, req.IsPinned), ct);
        return NoContent();
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

    // ── Join Code ─────────────────────────────────────────────────────────────

    /// <summary>Get the join code for a group (admin only).</summary>
    [HttpGet("spaces/{spaceId:guid}/groups/{groupId:guid}/join-code")]
    public async Task<IActionResult> GetJoinCode(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        var result = await _mediator.Send(new GetJoinCodeQuery(spaceId, groupId), ct);
        return Ok(new { joinCode = result });
    }

    /// <summary>Regenerate the join code for a group (admin only).</summary>
    [HttpPost("spaces/{spaceId:guid}/groups/{groupId:guid}/join-code/regenerate")]
    public async Task<IActionResult> RegenerateJoinCode(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.PeopleManage, ct);
        var newCode = await _mediator.Send(new RegenerateJoinCodeCommand(spaceId, groupId), ct);
        return Ok(new { joinCode = newCode });
    }

    /// <summary>Join a group using a join code. Any authenticated user can call this.</summary>
    [HttpPost("groups/join")]
    public async Task<IActionResult> JoinByCode([FromBody] JoinByCodeRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new JoinGroupByCodeCommand(req.Code, CurrentUserId), ct);
        return Ok(result);
    }
}

// ── Request records ───────────────────────────────────────────────────────────

public record AddMemberByIdRequest(Guid PersonId, Guid? RoleId = null);
public record CreateGroupMessageRequest(string Content, bool IsPinned = false);
public record CreateGroupTypeRequest(string Name, string? Description);
public record CreateGroupRequest(Guid? GroupTypeId, string Name, string? Description, string? TemplateType = null);
public record AddMemberByEmailRequest(string Email, Guid? RoleId = null);
public record AddMemberByPhoneRequest(string PhoneNumber, Guid? RoleId = null);
public record UpdateGroupSettingsRequest(int SolverHorizonDays, DateTime? SolverStartDateTime = null, bool? AutoPublish = null, int? MinRestBetweenShiftsHours = null, bool? AllowMembersViewHistory = null);
public record UpdateGroupRequest(bool? IsClosedBase = null, string? TemplateType = null);
public record RenameGroupRequest(string Name);
public record InitiateGroupTransferRequest(Guid ProposedPersonId);
public record CreateAlertRequest(string Title, string Body, string Severity);
public record UpdateAlertRequest(string Title, string Body, string Severity);
public record UpdateMessageRequest(string Content);
public record PinMessageRequest(bool IsPinned);
public record UpdateMemberRoleRequest(Guid? RoleId);
public record JoinByCodeRequest(string Code);

public record SetHomeLeavePriorityRequest(decimal Priority);
