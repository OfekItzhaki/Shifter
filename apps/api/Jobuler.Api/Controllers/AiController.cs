using Jobuler.Application.AI;
using Jobuler.Application.AI.Commands;
using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

/// <summary>
/// AI assistant endpoints — optional helper layer.
/// All responses are candidate data for admin review, never auto-applied.
/// Requires admin_mode permission.
/// </summary>
[ApiController]
[Route("spaces/{spaceId:guid}/ai")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public AiController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Parse a natural language instruction into a candidate constraint.
    /// The admin must review and confirm before saving.
    /// </summary>
    [HttpPost("parse-constraint")]
    public async Task<IActionResult> ParseConstraint(
        Guid spaceId, [FromBody] ParseConstraintRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);

        var locale = User.FindFirstValue("locale") ?? "he";
        var result = await _mediator.Send(
            new ParseConstraintCommand(spaceId, req.Input, locale, CurrentUserId), ct);

        return Ok(result);
    }

    /// <summary>
    /// Summarize a schedule diff in plain language.
    /// </summary>
    [HttpPost("summarize-diff")]
    public async Task<IActionResult> SummarizeDiff(
        Guid spaceId, [FromBody] DiffContextDto diff, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            CurrentUserId, spaceId, Permissions.SpaceAdminMode, ct);

        var locale = User.FindFirstValue("locale") ?? "he";
        var summary = await _mediator.Send(new SummarizeDiffCommand(diff, locale), ct);
        return Ok(new { summary });
    }

    /// <summary>
    /// Explain why a schedule was infeasible.
    /// </summary>
    [HttpPost("explain-infeasibility")]
    public async Task<IActionResult> ExplainInfeasibility(
        Guid spaceId, [FromBody] InfeasibilityContextDto context, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            CurrentUserId, spaceId, Permissions.SpaceAdminMode, ct);

        var locale = User.FindFirstValue("locale") ?? "he";
        var explanation = await _mediator.Send(
            new ExplainInfeasibilityCommand(context, locale), ct);
        return Ok(new { explanation });
    }

    /// <summary>
    /// Smart import: parse a file (Excel, image, PDF) using AI and return structured preview.
    /// </summary>
    [HttpPost("/spaces/{spaceId:guid}/groups/{groupId:guid}/import/smart")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
    public async Task<IActionResult> SmartImport(
        Guid spaceId, Guid groupId, IFormFile file, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            CurrentUserId, spaceId, Permissions.TasksManage, ct);

        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { error = "File too large. Maximum 10MB." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var base64 = Convert.ToBase64String(ms.ToArray());

        var result = await _mediator.Send(new SmartImportCommand(
            spaceId, groupId, CurrentUserId,
            file.FileName, base64, file.ContentType), ct);

        return Ok(result);
    }

    /// <summary>
    /// Confirm smart import: create people, tasks, and assignments from the preview data.
    /// </summary>
    [HttpPost("/spaces/{spaceId:guid}/groups/{groupId:guid}/import/confirm")]
    public async Task<IActionResult> ConfirmImport(
        Guid spaceId, Guid groupId, [FromBody] SmartImportConfirmRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            CurrentUserId, spaceId, Permissions.TasksManage, ct);

        var result = await _mediator.Send(new SmartImportConfirmCommand(
            spaceId, groupId, CurrentUserId,
            req.People, req.Tasks, req.Assignments), ct);

        return Ok(result);
    }
}

public record ParseConstraintRequest(string Input);

public record SmartImportConfirmRequest(
    List<SmartImportPersonDto> People,
    List<SmartImportTaskDto> Tasks,
    List<SmartImportAssignmentDto> Assignments);
