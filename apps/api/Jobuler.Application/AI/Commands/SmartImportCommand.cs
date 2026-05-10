using Jobuler.Application.AI.Import;
using Jobuler.Application.Common;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Jobuler.Application.AI.Commands;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record ImportPreviewDto(
    List<string> People,
    List<ImportTaskDto> Tasks,
    List<ImportAssignmentDto> Assignments,
    string? AiConfidence,
    string? ParseMethod,
    List<string>? Warnings);

public record ImportTaskDto(string Name, int ShiftDurationHours, int RequiredHeadcount);

public record ImportAssignmentDto(
    string PersonName,
    string TaskName,
    string DayOfWeek,
    int StartHour,
    int EndHour);

public record ImportConfirmResultDto(Guid DraftVersionId);

// ── AI raw response shape ─────────────────────────────────────────────────────

internal record AiImportRawResponse(
    List<string>? People,
    List<AiImportTaskRaw>? Tasks,
    List<AiImportAssignmentRaw>? Assignments);

internal record AiImportTaskRaw(string Name, int ShiftDurationHours, int RequiredHeadcount);

internal record AiImportAssignmentRaw(
    string PersonName,
    string TaskName,
    string DayOfWeek,
    int StartHour,
    int EndHour);

// ── Parse command ─────────────────────────────────────────────────────────────

public record ParseScheduleImportCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid RequestingUserId,
    string FileName,
    string FileContentBase64,
    string ContentType) : IRequest<ImportPreviewDto>;

public class ParseScheduleImportCommandHandler : IRequestHandler<ParseScheduleImportCommand, ImportPreviewDto>
{
    private readonly IAiAssistant _ai;
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly IStructuredImportParser _parser;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ParseScheduleImportCommandHandler(
        IAiAssistant ai,
        AppDbContext db,
        IPermissionService permissions,
        IStructuredImportParser parser)
    {
        _ai = ai;
        _db = db;
        _permissions = permissions;
        _parser = parser;
    }

    public async Task<ImportPreviewDto> Handle(ParseScheduleImportCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            req.RequestingUserId, req.SpaceId, Permissions.TasksManage, ct);

        // Verify group belongs to space
        var groupExists = await _db.Groups
            .AnyAsync(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId && g.DeletedAt == null, ct);
        if (!groupExists)
            throw new KeyNotFoundException("Group not found in this space.");

        var extension = Path.GetExtension(req.FileName)?.ToLowerInvariant();
        var isStructuredFormat = extension is ".csv" or ".xlsx" or ".xls";
        var isImageOrPdf = extension is ".png" or ".jpg" or ".jpeg" or ".pdf";

        // ── Structured parsing (CSV/Excel) ────────────────────────────────────
        if (isStructuredFormat)
        {
            var fileBytes = Convert.FromBase64String(req.FileContentBase64);
            var structuredResult = _parser.TryParse(fileBytes, req.FileName);

            if (structuredResult != null)
            {
                if (structuredResult.Assignments.Count == 0)
                    throw new InvalidOperationException("File contains no valid data rows.");

                return new ImportPreviewDto(
                    structuredResult.People,
                    structuredResult.Tasks,
                    structuredResult.Assignments,
                    null,
                    "structured",
                    structuredResult.Warnings.Count > 0 ? structuredResult.Warnings : null);
            }

            // Structured parsing failed — fall through to AI parsing below
        }

        // ── AI parsing (images/PDFs, or structured fallback) ──────────────────
        var rawJson = await _ai.ParseScheduleFileAsync(
            req.FileContentBase64, req.ContentType, req.FileName, ct);

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            if (isImageOrPdf)
                throw new InvalidOperationException(
                    "AI is not configured. Image/PDF import requires AI. Please set up the AI:ApiKey or upload a structured CSV/Excel file.");

            throw new InvalidOperationException(
                "Could not detect columns. Expected: שם (person_name), משימה (task_name), יום (day_of_week), שעת_התחלה (start_hour), שעת_סיום (end_hour)");
        }

        // Strip markdown code fences if present
        var json = rawJson.Trim();
        if (json.StartsWith("```"))
        {
            var firstNewline = json.IndexOf('\n');
            if (firstNewline > 0) json = json[(firstNewline + 1)..];
            if (json.EndsWith("```")) json = json[..^3];
            json = json.Trim();
        }

        var parsed = JsonSerializer.Deserialize<AiImportRawResponse>(json, JsonOpts);
        if (parsed == null)
            throw new InvalidOperationException("Failed to parse the uploaded file.");

        var people = parsed.People ?? [];
        var tasks = (parsed.Tasks ?? []).Select(t => new ImportTaskDto(
            t.Name,
            t.ShiftDurationHours > 0 ? t.ShiftDurationHours : 4,
            t.RequiredHeadcount > 0 ? t.RequiredHeadcount : 1
        )).ToList();

        var assignments = (parsed.Assignments ?? []).Select(a => new ImportAssignmentDto(
            a.PersonName,
            a.TaskName,
            a.DayOfWeek.ToLowerInvariant(),
            a.StartHour,
            a.EndHour
        )).ToList();

        // Determine confidence based on data completeness
        string? confidence = "high";
        if (assignments.Count == 0) confidence = "low";
        else if (people.Count == 0 || tasks.Count == 0) confidence = "medium";

        return new ImportPreviewDto(people, tasks, assignments, confidence, "ai", null);
    }
}

// ── Confirm command ───────────────────────────────────────────────────────────

public record ConfirmScheduleImportCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid RequestingUserId,
    List<string> People,
    List<ImportTaskDto> Tasks,
    List<ImportAssignmentDto> Assignments) : IRequest<ImportConfirmResultDto>;

public class ConfirmScheduleImportCommandHandler : IRequestHandler<ConfirmScheduleImportCommand, ImportConfirmResultDto>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly IMediator _mediator;

    public ConfirmScheduleImportCommandHandler(AppDbContext db, IPermissionService permissions, IMediator mediator)
    {
        _db = db;
        _permissions = permissions;
        _mediator = mediator;
    }

    public async Task<ImportConfirmResultDto> Handle(ConfirmScheduleImportCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            req.RequestingUserId, req.SpaceId, Permissions.TasksManage, ct);

        var groupExists = await _db.Groups
            .AnyAsync(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId && g.DeletedAt == null, ct);
        if (!groupExists)
            throw new KeyNotFoundException("Group not found in this space.");

        // 1. Create people who don't exist and add them to the group
        foreach (var personName in req.People)
        {
            var nameLower = personName.Trim().ToLowerInvariant();
            var existingPerson = await _db.People
                .FirstOrDefaultAsync(p => p.SpaceId == req.SpaceId && p.IsActive &&
                               p.FullName.ToLower() == nameLower, ct);

            if (existingPerson != null)
            {
                // Ensure they're in the group
                var isMember = await _db.GroupMemberships
                    .AnyAsync(gm => gm.GroupId == req.GroupId && gm.PersonId == existingPerson.Id, ct);
                if (!isMember)
                {
                    _db.GroupMemberships.Add(GroupMembership.Create(req.SpaceId, req.GroupId, existingPerson.Id));
                    await _db.SaveChangesAsync(ct);
                }
                continue;
            }

            try
            {
                var personId = await _mediator.Send(new Application.People.Commands.CreatePersonCommand(
                    req.SpaceId, personName.Trim(), null, null, req.RequestingUserId), ct);

                var alreadyMember = await _db.GroupMemberships
                    .AnyAsync(gm => gm.GroupId == req.GroupId && gm.PersonId == personId, ct);
                if (!alreadyMember)
                {
                    _db.GroupMemberships.Add(GroupMembership.Create(req.SpaceId, req.GroupId, personId));
                    await _db.SaveChangesAsync(ct);
                }
            }
            catch (ConflictException)
            {
                // Already exists — skip
            }
        }

        // 2. Create tasks that don't exist
        var now = DateTime.UtcNow;
        var endsAt = now.AddDays(90);

        foreach (var task in req.Tasks)
        {
            var taskNameLower = task.Name.Trim().ToLowerInvariant();
            var exists = await _db.GroupTasks
                .AnyAsync(t => t.SpaceId == req.SpaceId && t.GroupId == req.GroupId &&
                               t.Name.ToLower() == taskNameLower, ct);
            if (exists) continue;

            try
            {
                await _mediator.Send(new Application.Tasks.Commands.CreateGroupTaskCommand(
                    req.SpaceId, req.GroupId, req.RequestingUserId,
                    task.Name.Trim(), now, endsAt,
                    task.ShiftDurationHours * 60,
                    task.RequiredHeadcount,
                    "neutral",
                    false, false), ct);
            }
            catch
            {
                // Skip on error — task may already exist
            }
        }

        // 3. Create a draft schedule version with summary of the import
        var maxVersion = await _db.ScheduleVersions
            .Where(v => v.SpaceId == req.SpaceId)
            .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;

        var draft = ScheduleVersion.CreateDraft(
            req.SpaceId,
            maxVersion + 1,
            baselineVersionId: null,
            sourceRunId: null,
            createdByUserId: req.RequestingUserId,
            summaryJson: JsonSerializer.Serialize(new
            {
                source = "ai_import",
                peopleCount = req.People.Count,
                tasksCount = req.Tasks.Count,
                assignmentsCount = req.Assignments.Count,
                assignments = req.Assignments.Select(a => new
                {
                    a.PersonName,
                    a.TaskName,
                    a.DayOfWeek,
                    a.StartHour,
                    a.EndHour
                })
            }));

        _db.ScheduleVersions.Add(draft);
        await _db.SaveChangesAsync(ct);

        return new ImportConfirmResultDto(draft.Id);
    }
}
