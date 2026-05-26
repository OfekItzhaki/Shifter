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
[Route("spaces/{spaceId:guid}/groups/{groupId:guid}/shift-templates")]
[Authorize]
public class ShiftTemplatesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public ShiftTemplatesController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>List all shift templates for a group.</summary>
    [HttpGet]
    public async Task<IActionResult> List(Guid spaceId, Guid groupId,
        [FromQuery] bool includeDeleted = false, CancellationToken ct = default)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.TasksManage, ct);
        return Ok(await _mediator.Send(new ListShiftTemplatesQuery(spaceId, groupId, includeDeleted), ct));
    }

    /// <summary>Get a shift template by ID.</summary>
    [HttpGet("{templateId:guid}")]
    public async Task<IActionResult> Get(Guid spaceId, Guid groupId, Guid templateId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.TasksManage, ct);
        var result = await _mediator.Send(new GetShiftTemplateQuery(spaceId, groupId, templateId), ct);
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    /// <summary>Create a new shift template.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(Guid spaceId, Guid groupId,
        [FromBody] CreateShiftTemplateRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.TasksManage, ct);
        var result = await _mediator.Send(new CreateShiftTemplateCommand(
            spaceId,
            groupId,
            req.GroupTaskId,
            CurrentUserId,
            req.DayOfWeek,
            req.StartTime,
            req.EndTime,
            req.RequiredHeadcount), ct);
        return Created($"/spaces/{spaceId}/groups/{groupId}/shift-templates/{result.Id}", result);
    }

    /// <summary>Update an existing shift template.</summary>
    [HttpPut("{templateId:guid}")]
    public async Task<IActionResult> Update(Guid spaceId, Guid groupId, Guid templateId,
        [FromBody] UpdateShiftTemplateRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.TasksManage, ct);
        var result = await _mediator.Send(new UpdateShiftTemplateCommand(
            spaceId,
            groupId,
            templateId,
            CurrentUserId,
            req.DayOfWeek,
            req.StartTime,
            req.EndTime,
            req.RequiredHeadcount,
            req.GroupTaskId), ct);
        return Ok(result);
    }

    /// <summary>Soft-delete a shift template.</summary>
    [HttpDelete("{templateId:guid}")]
    public async Task<IActionResult> Delete(Guid spaceId, Guid groupId, Guid templateId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.TasksManage, ct);
        await _mediator.Send(new DeleteShiftTemplateCommand(spaceId, groupId, templateId, CurrentUserId), ct);
        return NoContent();
    }
}

public record CreateShiftTemplateRequest(
    Guid GroupTaskId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int RequiredHeadcount);

public record UpdateShiftTemplateRequest(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int RequiredHeadcount,
    Guid? GroupTaskId = null);
