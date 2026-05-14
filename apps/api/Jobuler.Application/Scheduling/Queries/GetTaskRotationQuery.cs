using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Queries;

/// <summary>
/// Returns task rotation progress per person for a given group.
/// </summary>
public record GetTaskRotationQuery(
    Guid SpaceId,
    Guid GroupId) : IRequest<TaskRotationResultDto>;

public record TaskRotationResultDto(
    List<PersonRotationDto> People);

public record PersonRotationDto(
    Guid PersonId,
    string DisplayName,
    int CycleNumber,
    double CompletionPercentage,
    int CompletedCount,
    int TotalCount);

public class GetTaskRotationQueryHandler
    : IRequestHandler<GetTaskRotationQuery, TaskRotationResultDto>
{
    private readonly AppDbContext _db;

    public GetTaskRotationQueryHandler(AppDbContext db) => _db = db;

    public async Task<TaskRotationResultDto> Handle(GetTaskRotationQuery req, CancellationToken ct)
    {
        // Validate group exists
        var group = await _db.Groups.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId && g.DeletedAt == null, ct);

        if (group is null)
            throw new KeyNotFoundException("Group not found");

        // Load rotation progress for this group
        var rotationRecords = await _db.TaskRotationProgress.AsNoTracking()
            .Where(r => r.SpaceId == req.SpaceId && r.GroupId == req.GroupId)
            .ToListAsync(ct);

        // Load person display names
        var personIds = rotationRecords.Select(r => r.PersonId).ToList();
        var people = await _db.People.AsNoTracking()
            .Where(p => p.SpaceId == req.SpaceId && personIds.Contains(p.Id))
            .Select(p => new { p.Id, p.DisplayName })
            .ToDictionaryAsync(p => p.Id, p => p.DisplayName, ct);

        var result = rotationRecords.Select(r => new PersonRotationDto(
            r.PersonId,
            people.GetValueOrDefault(r.PersonId, "Unknown"),
            r.CycleNumber,
            r.CompletionPercentage,
            r.CompletedTaskTypeIds.Count,
            r.TotalQualifiedTaskTypes
        )).ToList();

        return new TaskRotationResultDto(result);
    }
}
