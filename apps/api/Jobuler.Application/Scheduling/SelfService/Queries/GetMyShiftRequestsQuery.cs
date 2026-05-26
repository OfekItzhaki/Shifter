using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.SelfService.Queries;

/// <summary>
/// Query to retrieve a member's shift requests for a given group, optionally filtered by scheduling cycle.
/// </summary>
public record GetMyShiftRequestsQuery(
    Guid SpaceId,
    Guid GroupId,
    Guid PersonId,
    Guid? SchedulingCycleId = null) : IRequest<IReadOnlyList<ShiftRequestDto>>;

/// <summary>
/// DTO representing a shift request with associated slot details.
/// </summary>
public record ShiftRequestDto(
    Guid Id,
    Guid ShiftSlotId,
    Guid GroupId,
    Guid SchedulingCycleId,
    string Status,
    bool IsAdminOverride,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string TaskName,
    string? RejectionReason,
    string? CancellationReason,
    DateTime? CancelledAt,
    DateTime CreatedAt);

public class GetMyShiftRequestsQueryHandler : IRequestHandler<GetMyShiftRequestsQuery, IReadOnlyList<ShiftRequestDto>>
{
    private readonly AppDbContext _db;

    public GetMyShiftRequestsQueryHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ShiftRequestDto>> Handle(GetMyShiftRequestsQuery request, CancellationToken ct)
    {
        var query = _db.ShiftRequests
            .AsNoTracking()
            .Where(r => r.SpaceId == request.SpaceId
                        && r.GroupId == request.GroupId
                        && r.PersonId == request.PersonId);

        if (request.SchedulingCycleId.HasValue)
        {
            query = query.Where(r => r.SchedulingCycleId == request.SchedulingCycleId.Value);
        }

        var results = await query
            .Join(
                _db.ShiftSlots.AsNoTracking(),
                r => r.ShiftSlotId,
                s => s.Id,
                (r, s) => new { Request = r, Slot = s })
            .Join(
                _db.GroupTasks.AsNoTracking(),
                rs => rs.Slot.GroupTaskId,
                t => t.Id,
                (rs, t) => new { rs.Request, rs.Slot, TaskName = t.Name })
            .OrderByDescending(x => x.Slot.Date)
            .ThenByDescending(x => x.Slot.StartTime)
            .Select(x => new ShiftRequestDto(
                x.Request.Id,
                x.Request.ShiftSlotId,
                x.Request.GroupId,
                x.Request.SchedulingCycleId,
                x.Request.Status.ToString(),
                x.Request.IsAdminOverride,
                x.Slot.Date,
                x.Slot.StartTime,
                x.Slot.EndTime,
                x.TaskName,
                x.Request.RejectionReason,
                x.Request.CancellationReason,
                x.Request.CancelledAt,
                x.Request.CreatedAt))
            .ToListAsync(ct);

        return results;
    }
}
