using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record ScheduleVersionDto(
    Guid Id,
    int VersionNumber,
    string Status,
    Guid? BaselineVersionId,
    Guid? SourceRunId,
    Guid? RollbackSourceVersionId,
    Guid? CreatedByUserId,
    Guid? PublishedByUserId,
    DateTime CreatedAt,
    DateTime? PublishedAt,
    string? SummaryJson);

public record ScheduleVersionDetailDto(
    ScheduleVersionDto Version,
    DiffSummaryDto? Diff,
    List<AssignmentDto> Assignments);

public record DiffSummaryDto(
    int AddedCount,
    int RemovedCount,
    int ChangedCount,
    decimal? StabilityScore,
    string? DiffJson);

public record AssignmentDto(
    Guid Id,
    Guid TaskSlotId,
    Guid PersonId,
    string PersonName,
    string TaskTypeName,
    DateTime SlotStartsAt,
    DateTime SlotEndsAt,
    string Source);

// ── List versions ─────────────────────────────────────────────────────────────

public record GetScheduleVersionsQuery(
    Guid SpaceId,
    string? StatusFilter = null) : IRequest<List<ScheduleVersionDto>>;

public class GetScheduleVersionsQueryHandler
    : IRequestHandler<GetScheduleVersionsQuery, List<ScheduleVersionDto>>
{
    private readonly AppDbContext _db;
    public GetScheduleVersionsQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<ScheduleVersionDto>> Handle(
        GetScheduleVersionsQuery req, CancellationToken ct)
    {
        var query = _db.ScheduleVersions.AsNoTracking()
            .Where(v => v.SpaceId == req.SpaceId);

        if (!string.IsNullOrEmpty(req.StatusFilter) &&
            Enum.TryParse<ScheduleVersionStatus>(req.StatusFilter, true, out var status))
            query = query.Where(v => v.Status == status);

        return await query
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new ScheduleVersionDto(
                v.Id, v.VersionNumber, v.Status.ToString(),
                v.BaselineVersionId, v.SourceRunId, v.RollbackSourceVersionId,
                v.CreatedByUserId, v.PublishedByUserId,
                v.CreatedAt, v.PublishedAt, v.SummaryJson))
            .ToListAsync(ct);
    }
}

// ── Get version detail with assignments ──────────────────────────────────────

public record GetScheduleVersionDetailQuery(
    Guid SpaceId, Guid VersionId) : IRequest<ScheduleVersionDetailDto?>;

public class GetScheduleVersionDetailQueryHandler
    : IRequestHandler<GetScheduleVersionDetailQuery, ScheduleVersionDetailDto?>
{
    private readonly AppDbContext _db;
    public GetScheduleVersionDetailQueryHandler(AppDbContext db) => _db = db;

    public async Task<ScheduleVersionDetailDto?> Handle(
        GetScheduleVersionDetailQuery req, CancellationToken ct)
    {
        var version = await _db.ScheduleVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == req.VersionId && v.SpaceId == req.SpaceId, ct);
        if (version is null) return null;

        var diff = await _db.AssignmentChangeSummaries.AsNoTracking()
            .FirstOrDefaultAsync(d => d.VersionId == req.VersionId, ct);

        // Load raw assignments
        var rawAssignments = await _db.Assignments.AsNoTracking()
            .Where(a => a.ScheduleVersionId == req.VersionId && a.SpaceId == req.SpaceId)
            .ToListAsync(ct);

        var slotIds = rawAssignments.Select(a => a.TaskSlotId).ToHashSet();
        var personIds = rawAssignments.Select(a => a.PersonId).ToHashSet();

        var people = await _db.People.AsNoTracking()
            .Where(p => personIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.DisplayName ?? p.FullName, ct);

        // Legacy task slots
        var taskSlots = await _db.TaskSlots.AsNoTracking()
            .Where(s => slotIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        var taskTypeIds = taskSlots.Values.Select(s => s.TaskTypeId).ToHashSet();
        var taskTypes = await _db.TaskTypes.AsNoTracking()
            .Where(t => taskTypeIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        // GroupTasks for any IDs not in task_slots
        var missingIds = slotIds.Where(id => !taskSlots.ContainsKey(id)).ToHashSet();
        var groupTasks = missingIds.Count > 0
            ? await _db.GroupTasks.AsNoTracking()
                .Where(t => missingIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, ct)
            : new();

        var assignments = new List<AssignmentDto>();
        foreach (var a in rawAssignments)
        {
            string taskName;
            DateTime startsAt;
            DateTime endsAt;

            if (taskSlots.TryGetValue(a.TaskSlotId, out var slot))
            {
                taskName = taskTypes.TryGetValue(slot.TaskTypeId, out var tn) ? tn : "Unknown";
                startsAt = slot.StartsAt;
                endsAt = slot.EndsAt;
            }
            else if (groupTasks.TryGetValue(a.TaskSlotId, out var gt))
            {
                taskName = gt.Name;
                startsAt = gt.StartsAt;
                endsAt = gt.EndsAt;
            }
            else continue;

            var personName = people.TryGetValue(a.PersonId, out var pn) ? pn : "Unknown";
            assignments.Add(new AssignmentDto(
                a.Id, a.TaskSlotId, a.PersonId, personName, taskName,
                startsAt, endsAt, a.Source.ToString()));
        }

        assignments = assignments.OrderBy(a => a.SlotStartsAt).ToList();

        var versionDto = new ScheduleVersionDto(
            version.Id, version.VersionNumber, version.Status.ToString(),
            version.BaselineVersionId, version.SourceRunId, version.RollbackSourceVersionId,
            version.CreatedByUserId, version.PublishedByUserId,
            version.CreatedAt, version.PublishedAt, version.SummaryJson);

        var diffDto = diff is null ? null : new DiffSummaryDto(
            diff.AddedCount, diff.RemovedCount, diff.ChangedCount,
            diff.StabilityScore, diff.DiffJson);

        return new ScheduleVersionDetailDto(versionDto, diffDto, assignments);
    }
}

// ── Get current published version ────────────────────────────────────────────

public record GetCurrentPublishedVersionQuery(Guid SpaceId) : IRequest<ScheduleVersionDetailDto?>;

public class GetCurrentPublishedVersionQueryHandler
    : IRequestHandler<GetCurrentPublishedVersionQuery, ScheduleVersionDetailDto?>
{
    private readonly IMediator _mediator;
    private readonly AppDbContext _db;

    public GetCurrentPublishedVersionQueryHandler(IMediator mediator, AppDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    public async Task<ScheduleVersionDetailDto?> Handle(
        GetCurrentPublishedVersionQuery req, CancellationToken ct)
    {
        var latest = await _db.ScheduleVersions.AsNoTracking()
            .Where(v => v.SpaceId == req.SpaceId && v.Status == ScheduleVersionStatus.Published)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        if (latest is null) return null;

        return await _mediator.Send(
            new GetScheduleVersionDetailQuery(req.SpaceId, latest.Id), ct);
    }
}
