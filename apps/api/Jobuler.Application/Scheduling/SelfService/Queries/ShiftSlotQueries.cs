using Jobuler.Application.Scheduling.SelfService.Models;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.SelfService.Queries;

/// <summary>
/// Query to get available shift slots for a member in a scheduling cycle.
/// Delegates to ISlotAvailabilityEngine.
/// </summary>
public record GetAvailableSlotsQuery(
    Guid SpaceId,
    Guid GroupId,
    Guid SchedulingCycleId,
    Guid UserId) : IRequest<SlotAvailabilityResult>;

public class GetAvailableSlotsQueryHandler : IRequestHandler<GetAvailableSlotsQuery, SlotAvailabilityResult>
{
    private readonly AppDbContext _db;
    private readonly ISlotAvailabilityEngine _availabilityEngine;

    public GetAvailableSlotsQueryHandler(AppDbContext db, ISlotAvailabilityEngine availabilityEngine)
    {
        _db = db;
        _availabilityEngine = availabilityEngine;
    }

    public async Task<SlotAvailabilityResult> Handle(GetAvailableSlotsQuery req, CancellationToken ct)
    {
        // Resolve person from user in this space
        var person = await _db.People.AsNoTracking()
            .Where(p => p.SpaceId == req.SpaceId && p.LinkedUserId == req.UserId)
            .Join(
                _db.GroupMemberships.AsNoTracking().Where(gm => gm.SpaceId == req.SpaceId && gm.GroupId == req.GroupId),
                p => p.Id,
                gm => gm.PersonId,
                (p, _) => p)
            .FirstOrDefaultAsync(ct);

        if (person is null)
            return new SlotAvailabilityResult([], false, null);

        return await _availabilityEngine.GetAvailableSlotsAsync(person.Id, req.GroupId, req.SchedulingCycleId, ct);
    }
}

/// <summary>
/// Query to get a single shift slot's details by ID.
/// </summary>
public record GetShiftSlotDetailQuery(
    Guid SpaceId,
    Guid GroupId,
    Guid ShiftSlotId,
    Guid UserId) : IRequest<ShiftSlotDetailDto?>;

public class GetShiftSlotDetailQueryHandler : IRequestHandler<GetShiftSlotDetailQuery, ShiftSlotDetailDto?>
{
    private readonly AppDbContext _db;

    public GetShiftSlotDetailQueryHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ShiftSlotDetailDto?> Handle(GetShiftSlotDetailQuery req, CancellationToken ct)
    {
        var slot = await _db.ShiftSlots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == req.ShiftSlotId
                                      && s.SpaceId == req.SpaceId
                                      && s.GroupId == req.GroupId, ct);

        if (slot is null)
            return null;

        var isGroupMember = await _db.People
            .AsNoTracking()
            .Where(p => p.SpaceId == req.SpaceId && p.LinkedUserId == req.UserId)
            .Join(
                _db.GroupMemberships.AsNoTracking().Where(gm => gm.SpaceId == req.SpaceId && gm.GroupId == req.GroupId),
                p => p.Id,
                gm => gm.PersonId,
                (p, _) => p.Id)
            .AnyAsync(ct);

        if (!isGroupMember)
            return null;

        // Get the task name
        var taskName = await _db.GroupTasks
            .AsNoTracking()
            .Where(t => t.Id == slot.GroupTaskId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct) ?? "Unknown";

        // Get the scheduling cycle to determine read-only state
        var cycle = await _db.SchedulingCycles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == slot.SchedulingCycleId && c.GroupId == req.GroupId, ct);

        var isReadOnly = cycle is null || !cycle.IsRequestWindowOpen(DateTime.UtcNow);

        return new ShiftSlotDetailDto(
            Id: slot.Id,
            GroupId: slot.GroupId,
            GroupTaskId: slot.GroupTaskId,
            TaskName: taskName,
            ShiftTemplateId: slot.ShiftTemplateId,
            SchedulingCycleId: slot.SchedulingCycleId,
            Date: slot.Date,
            StartTime: slot.StartTime,
            EndTime: slot.EndTime,
            Capacity: slot.Capacity,
            CurrentFillCount: slot.CurrentFillCount,
            Status: slot.Status.ToString(),
            IsReadOnly: isReadOnly);
    }
}
