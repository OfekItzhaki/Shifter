using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Jobuler.Application.AI.Commands;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record SmartImportPersonDto(
    string FullName,
    string? DisplayName,
    bool IsNew);

public record SmartImportTaskDto(
    string Name,
    int ShiftDurationMinutes,
    int RequiredHeadcount,
    string BurdenLevel,
    bool IsNew);

public record SmartImportAssignmentDto(
    string PersonName,
    string TaskName,
    string StartsAt,
    string EndsAt);

public record SmartImportPreviewDto(
    List<SmartImportPersonDto> People,
    List<SmartImportTaskDto> Tasks,
    List<SmartImportAssignmentDto> Assignments,
    string Summary);

// ── AI raw response shape ─────────────────────────────────────────────────────

public record AiImportPersonRaw(string FullName, string? DisplayName);
public record AiImportTaskRaw(string Name, int ShiftDurationMinutes, int RequiredHeadcount, string BurdenLevel);
public record AiImportAssignmentRaw(string PersonName, string TaskName, string StartsAt, string EndsAt);

public record AiImportRawResponse(
    List<AiImportPersonRaw>? People,
    List<AiImportTaskRaw>? Tasks,
    List<AiImportAssignmentRaw>? Assignments);

// ── Preview command ───────────────────────────────────────────────────────────

public record SmartImportCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid RequestingUserId,
    string FileName,
    string FileContentBase64,
    string ContentType) : IRequest<SmartImportPreviewDto>;

public class SmartImportCommandHandler : IRequestHandler<SmartImportCommand, SmartImportPreviewDto>
{
    private readonly IAiAssistant _ai;
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly bool _aiAvailable;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public SmartImportCommandHandler(IAiAssistant ai, AppDbContext db, IPermissionService permissions)
    {
        _ai = ai;
        _db = db;
        _permissions = permissions;
        _aiAvailable = ai is not Infrastructure.AI.NoOpAiAssistant;
    }

    public async Task<SmartImportPreviewDto> Handle(SmartImportCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            req.RequestingUserId, req.SpaceId, Permissions.TasksManage, ct);

        // Verify group belongs to space
        var groupExists = await _db.Groups
            .AnyAsync(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId && g.DeletedAt == null, ct);
        if (!groupExists)
            throw new KeyNotFoundException("Group not found in this space.");

        AiImportRawResponse? parsed;

        if (_aiAvailable)
        {
            parsed = await ParseWithAiAsync(req, ct);
        }
        else
        {
            throw new InvalidOperationException(
                "Smart import requires AI to be configured. Please set up the AI:ApiKey in configuration.");
        }

        if (parsed == null)
            throw new InvalidOperationException("Failed to parse the uploaded file.");

        // Determine which people/tasks are new vs existing
        var existingPeople = await _db.People
            .Where(p => p.SpaceId == req.SpaceId && p.IsActive)
            .Select(p => p.FullName.ToLower())
            .ToListAsync(ct);

        var existingTasks = await _db.GroupTasks
            .Where(t => t.SpaceId == req.SpaceId && t.GroupId == req.GroupId)
            .Select(t => t.Name.ToLower())
            .ToListAsync(ct);

        var existingPeopleSet = new HashSet<string>(existingPeople);
        var existingTasksSet = new HashSet<string>(existingTasks);

        var people = (parsed.People ?? []).Select(p => new SmartImportPersonDto(
            p.FullName,
            p.DisplayName,
            !existingPeopleSet.Contains(p.FullName.Trim().ToLowerInvariant())
        )).ToList();

        var tasks = (parsed.Tasks ?? []).Select(t => new SmartImportTaskDto(
            t.Name,
            t.ShiftDurationMinutes > 0 ? t.ShiftDurationMinutes : 240,
            t.RequiredHeadcount > 0 ? t.RequiredHeadcount : 1,
            string.IsNullOrWhiteSpace(t.BurdenLevel) ? "neutral" : t.BurdenLevel.ToLowerInvariant(),
            !existingTasksSet.Contains(t.Name.Trim().ToLowerInvariant())
        )).ToList();

        var assignments = (parsed.Assignments ?? []).Select(a => new SmartImportAssignmentDto(
            a.PersonName,
            a.TaskName,
            a.StartsAt,
            a.EndsAt
        )).ToList();

        var newPeopleCount = people.Count(p => p.IsNew);
        var newTasksCount = tasks.Count(t => t.IsNew);
        var summary = $"Found {people.Count} people ({newPeopleCount} new), " +
                      $"{tasks.Count} tasks ({newTasksCount} new), " +
                      $"{assignments.Count} assignments";

        return new SmartImportPreviewDto(people, tasks, assignments, summary);
    }

    private async Task<AiImportRawResponse?> ParseWithAiAsync(SmartImportCommand req, CancellationToken ct)
    {
        var result = await _ai.ParseScheduleFileAsync(req.FileContentBase64, req.ContentType, req.FileName, ct);
        if (string.IsNullOrWhiteSpace(result))
            return null;

        // Strip markdown code fences if present
        var json = result.Trim();
        if (json.StartsWith("```"))
        {
            var firstNewline = json.IndexOf('\n');
            if (firstNewline > 0) json = json[(firstNewline + 1)..];
            if (json.EndsWith("```")) json = json[..^3];
            json = json.Trim();
        }

        return JsonSerializer.Deserialize<AiImportRawResponse>(json, JsonOpts);
    }
}

// ── Confirm command ───────────────────────────────────────────────────────────

public record SmartImportConfirmCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid RequestingUserId,
    List<SmartImportPersonDto> People,
    List<SmartImportTaskDto> Tasks,
    List<SmartImportAssignmentDto> Assignments) : IRequest<SmartImportConfirmResultDto>;

public record SmartImportConfirmResultDto(
    int PeopleCreated,
    int TasksCreated,
    int AssignmentsCreated);

public class SmartImportConfirmCommandHandler : IRequestHandler<SmartImportConfirmCommand, SmartImportConfirmResultDto>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly IMediator _mediator;

    public SmartImportConfirmCommandHandler(AppDbContext db, IPermissionService permissions, IMediator mediator)
    {
        _db = db;
        _permissions = permissions;
        _mediator = mediator;
    }

    public async Task<SmartImportConfirmResultDto> Handle(SmartImportConfirmCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            req.RequestingUserId, req.SpaceId, Permissions.TasksManage, ct);

        var groupExists = await _db.Groups
            .AnyAsync(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId && g.DeletedAt == null, ct);
        if (!groupExists)
            throw new KeyNotFoundException("Group not found in this space.");

        int peopleCreated = 0;
        int tasksCreated = 0;

        // Create new people
        foreach (var person in req.People.Where(p => p.IsNew))
        {
            var nameLower = person.FullName.Trim().ToLowerInvariant();
            var exists = await _db.People
                .AnyAsync(p => p.SpaceId == req.SpaceId && p.IsActive &&
                               p.FullName.ToLower() == nameLower, ct);
            if (exists) continue;

            try
            {
                await _mediator.Send(new Application.People.Commands.CreatePersonCommand(
                    req.SpaceId, person.FullName, person.DisplayName, null, req.RequestingUserId), ct);

                // Add to group
                var newPerson = await _db.People
                    .FirstAsync(p => p.SpaceId == req.SpaceId && p.IsActive &&
                                     p.FullName.ToLower() == nameLower, ct);
                var alreadyMember = await _db.GroupMembers
                    .AnyAsync(gm => gm.GroupId == req.GroupId && gm.PersonId == newPerson.Id, ct);
                if (!alreadyMember)
                {
                    _db.GroupMembers.Add(new Domain.Groups.GroupMember
                    {
                        GroupId = req.GroupId,
                        PersonId = newPerson.Id
                    });
                    await _db.SaveChangesAsync(ct);
                }

                peopleCreated++;
            }
            catch (ConflictException)
            {
                // Already exists — skip
            }
        }

        // Create new tasks
        var now = DateTime.UtcNow;
        var endsAt = now.AddDays(90);

        foreach (var task in req.Tasks.Where(t => t.IsNew))
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
                    task.Name, now, endsAt,
                    task.ShiftDurationMinutes,
                    task.RequiredHeadcount,
                    task.BurdenLevel,
                    false, false), ct);
                tasksCreated++;
            }
            catch
            {
                // Skip on error
            }
        }

        return new SmartImportConfirmResultDto(peopleCreated, tasksCreated, req.Assignments.Count);
    }
}
