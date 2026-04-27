using Jobuler.Application.Common;
using Jobuler.Application.Tasks.Commands;
using Jobuler.Application.Tasks.Queries;
using Jobuler.Domain.Spaces;
using Jobuler.Domain.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public TasksController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── Legacy Task Types ─────────────────────────────────────────────────────

    [HttpGet("spaces/{spaceId:guid}/task-types")]
    public async Task<IActionResult> ListTaskTypes(Guid spaceId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        return Ok(await _mediator.Send(new GetTaskTypesQuery(spaceId), ct));
    }

    [HttpPost("spaces/{spaceId:guid}/task-types")]
    public async Task<IActionResult> CreateTaskType(Guid spaceId,
        [FromBody] CreateTaskTypeRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.TasksManage, ct);
        var id = await _mediator.Send(new CreateTaskTypeCommand(
            spaceId, req.Name, req.Description,
            Enum.Parse<TaskBurdenLevel>(req.BurdenLevel, true),
            req.DefaultPriority, req.AllowsOverlap, CurrentUserId), ct);
        return Created($"/spaces/{spaceId}/task-types/{id}", new { id });
    }

    // ── Legacy Task Slots ─────────────────────────────────────────────────────

    [HttpGet("spaces/{spaceId:guid}/task-slots")]
    public async Task<IActionResult> ListTaskSlots(
        Guid spaceId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.SpaceView, ct);
        return Ok(await _mediator.Send(new GetTaskSlotsQuery(spaceId, from, to), ct));
    }

    [HttpPost("spaces/{spaceId:guid}/task-slots")]
    public async Task<IActionResult> CreateTaskSlot(Guid spaceId,
        [FromBody] CreateTaskSlotRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.TasksManage, ct);
        var id = await _mediator.Send(new CreateTaskSlotCommand(
            spaceId, req.TaskTypeId, req.StartsAt, req.EndsAt,
            req.RequiredHeadcount, req.Priority,
            req.RequiredRoleIds, req.RequiredQualificationIds,
            req.Location, CurrentUserId), ct);
        return Created($"/spaces/{spaceId}/task-slots/{id}", new { id });
    }

    // ── Group Tasks (new flat model) ──────────────────────────────────────────

    [HttpGet("spaces/{spaceId:guid}/groups/{groupId:guid}/tasks")]
    public async Task<IActionResult> ListGroupTasks(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetGroupTasksQuery(spaceId, groupId, CurrentUserId), ct);
        return Ok(result);
    }

    [HttpPost("spaces/{spaceId:guid}/groups/{groupId:guid}/tasks")]
    public async Task<IActionResult> CreateGroupTask(Guid spaceId, Guid groupId,
        [FromBody] CreateGroupTaskRequest req, CancellationToken ct)
    {
        var id = await _mediator.Send(new CreateGroupTaskCommand(
            spaceId, groupId, CurrentUserId,
            req.Name, req.StartsAt, req.EndsAt,
            req.ShiftDurationMinutes, req.RequiredHeadcount,
            req.BurdenLevel, req.AllowsDoubleShift, req.AllowsOverlap), ct);
        return Created($"/spaces/{spaceId}/groups/{groupId}/tasks/{id}", new { id });
    }

    [HttpPut("spaces/{spaceId:guid}/groups/{groupId:guid}/tasks/{taskId:guid}")]
    public async Task<IActionResult> UpdateGroupTask(Guid spaceId, Guid groupId, Guid taskId,
        [FromBody] UpdateGroupTaskRequest req, CancellationToken ct)
    {
        await _mediator.Send(new UpdateGroupTaskCommand(
            spaceId, groupId, taskId, CurrentUserId,
            req.Name, req.StartsAt, req.EndsAt,
            req.ShiftDurationMinutes, req.RequiredHeadcount,
            req.BurdenLevel, req.AllowsDoubleShift, req.AllowsOverlap), ct);
        return NoContent();
    }

    [HttpDelete("spaces/{spaceId:guid}/groups/{groupId:guid}/tasks/{taskId:guid}")]
    public async Task<IActionResult> DeleteGroupTask(Guid spaceId, Guid groupId, Guid taskId,
        CancellationToken ct)
    {
        await _mediator.Send(new DeleteGroupTaskCommand(spaceId, groupId, taskId, CurrentUserId), ct);
        return NoContent();
    }
}

public record CreateTaskTypeRequest(
    string Name, string? Description, string BurdenLevel,
    int DefaultPriority, bool AllowsOverlap);

public record CreateTaskSlotRequest(
    Guid TaskTypeId, DateTime StartsAt, DateTime EndsAt,
    int RequiredHeadcount, int Priority,
    List<Guid>? RequiredRoleIds, List<Guid>? RequiredQualificationIds,
    string? Location);

public record CreateGroupTaskRequest(
    string Name,
    DateTime StartsAt,
    DateTime EndsAt,
    int ShiftDurationMinutes,
    int RequiredHeadcount,
    string BurdenLevel,
    bool AllowsDoubleShift,
    bool AllowsOverlap);

public record UpdateGroupTaskRequest(
    string Name,
    DateTime StartsAt,
    DateTime EndsAt,
    int ShiftDurationMinutes,
    int RequiredHeadcount,
    string BurdenLevel,
    bool AllowsDoubleShift,
    bool AllowsOverlap);
