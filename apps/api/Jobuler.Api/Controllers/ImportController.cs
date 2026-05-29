using Jobuler.Application.AI.Commands;
using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

/// <summary>
/// AI-powered schedule import — parse files (Excel, image, PDF) and create draft schedules.
/// All parsed data is returned for admin review before any changes are persisted.
/// </summary>
[ApiController]
[Route("spaces/{spaceId:guid}/groups/{groupId:guid}/import")]
[Authorize]
public class ImportController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;

    public ImportController(IMediator mediator, IPermissionService permissions)
    {
        _mediator = mediator;
        _permissions = permissions;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx", ".xls", ".csv", ".png", ".jpg", ".jpeg", ".pdf"
    };

    /// <summary>
    /// Parse an uploaded file (Excel, CSV, image, or PDF) using AI and return structured preview.
    /// The admin reviews the preview before confirming.
    /// </summary>
    [HttpPost("parse")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
    public async Task<IActionResult> Parse(
        Guid spaceId, Guid groupId, [FromForm] IFormFile file, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            CurrentUserId, spaceId, Permissions.TasksManage, ct);

        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { error = "File too large. Maximum 10MB." });

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
            return BadRequest(new { error = $"Unsupported file type: {extension}. Supported: .xlsx, .xls, .csv, .png, .jpg, .jpeg, .pdf" });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var base64 = Convert.ToBase64String(ms.ToArray());

        var result = await _mediator.Send(new ParseScheduleImportCommand(
            spaceId, groupId, CurrentUserId,
            file.FileName, base64, file.ContentType), ct);

        return Ok(result);
    }

    /// <summary>
    /// Download a CSV template with the expected column format for structured import.
    /// </summary>
    [HttpGet("template")]
    public async Task<IActionResult> DownloadTemplate(
        Guid spaceId, Guid groupId, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            CurrentUserId, spaceId, Permissions.TasksManage, ct);

        // UTF-8 BOM for proper Hebrew display in Excel
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var csvContent = "name,task,day,start_hour,end_hour,shift_duration,headcount\n" +
                         "John Doe,Guard,Sun,8,16,8,2\n";

        var bytes = bom.Concat(System.Text.Encoding.UTF8.GetBytes(csvContent)).ToArray();

        return File(bytes, "text/csv; charset=utf-8", "import-template.csv");
    }

    /// <summary>
    /// Confirm the import: create people, tasks, and a draft schedule from the reviewed preview data.
    /// </summary>
    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(
        Guid spaceId, Guid groupId, [FromBody] ImportConfirmRequest req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            CurrentUserId, spaceId, Permissions.TasksManage, ct);

        var result = await _mediator.Send(new ConfirmScheduleImportCommand(
            spaceId, groupId, CurrentUserId,
            req.People, req.Tasks, req.Assignments), ct);

        return Ok(result);
    }
}

public record ImportConfirmRequest(
    List<string> People,
    List<ImportTaskDto> Tasks,
    List<ImportAssignmentDto> Assignments);
