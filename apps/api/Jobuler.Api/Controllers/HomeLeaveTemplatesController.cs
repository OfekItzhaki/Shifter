using Jobuler.Application.Common;
using Jobuler.Application.HomeLeave.Commands;
using Jobuler.Application.HomeLeave.Queries;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/home-leave-templates")]
[Authorize]
public class HomeLeaveTemplatesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public HomeLeaveTemplatesController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<IActionResult> Create(Guid spaceId,
        [FromBody] CreateHomeLeaveTemplateRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);
        var id = await _mediator.Send(new CreateHomeLeaveTemplateCommand(
            spaceId,
            req.Name,
            req.MinRestHours,
            req.EligibilityThresholdHours,
            req.LeaveCapacity,
            req.LeaveDurationHours,
            CurrentUserId), ct);
        return Created($"/spaces/{spaceId}/home-leave-templates/{id}", new { id });
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid spaceId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);
        return Ok(await _mediator.Send(new ListHomeLeaveTemplatesQuery(spaceId), ct));
    }

    [HttpGet("{templateId:guid}")]
    public async Task<IActionResult> Load(Guid spaceId, Guid templateId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);
        return Ok(await _mediator.Send(new LoadHomeLeaveTemplateQuery(spaceId, templateId), ct));
    }

    [HttpDelete("{templateId:guid}")]
    public async Task<IActionResult> Delete(Guid spaceId, Guid templateId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);
        await _mediator.Send(new DeleteHomeLeaveTemplateCommand(spaceId, templateId, CurrentUserId), ct);
        return NoContent();
    }
}

public record CreateHomeLeaveTemplateRequest(
    string Name,
    decimal MinRestHours,
    decimal EligibilityThresholdHours,
    int LeaveCapacity,
    decimal LeaveDurationHours);
