using Jobuler.Application.Common;
using Jobuler.Application.Exports.Commands;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/exports")]
[Authorize]
public class ExportsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public ExportsController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Export a schedule version as CSV.
    /// Requires schedule.publish permission (same level as publishing).
    /// </summary>
    [HttpGet("{versionId:guid}/csv")]
    public async Task<IActionResult> ExportCsv(
        Guid spaceId, Guid versionId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            CurrentUserId, spaceId, Permissions.SchedulePublish, ct);

        var result = await _mediator.Send(
            new ExportScheduleCsvCommand(spaceId, versionId, CurrentUserId), ct);

        return File(result.Content, "text/csv; charset=utf-8", result.FileName);
    }

    [HttpGet("{versionId:guid}/pdf")]
    public async Task<IActionResult> ExportPdf(
        Guid spaceId, Guid versionId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            CurrentUserId, spaceId, Permissions.SchedulePublish, ct);

        var result = await _mediator.Send(
            new ExportSchedulePdfCommand(spaceId, versionId, CurrentUserId), ct);

        return File(result.Content, "application/pdf", result.FileName);
    }
}
