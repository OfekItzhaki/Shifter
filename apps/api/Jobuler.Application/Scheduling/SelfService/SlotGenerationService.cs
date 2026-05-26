using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Scheduling.SelfService;

/// <summary>
/// Generates shift slots from shift templates for a scheduling cycle.
/// Produces one slot per non-deleted template with an active GroupTask
/// for each date in the cycle that matches the template's day of week.
/// Generation is idempotent — running multiple times produces the same result.
/// </summary>
public class SlotGenerationService : ISlotGenerationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<SlotGenerationService> _logger;

    public SlotGenerationService(AppDbContext db, ILogger<SlotGenerationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task GenerateSlotsForCycleAsync(Guid groupId, Guid schedulingCycleId, CancellationToken ct = default)
    {
        // Load the scheduling cycle
        var cycle = await _db.SchedulingCycles
            .FirstOrDefaultAsync(c => c.Id == schedulingCycleId && c.GroupId == groupId, ct)
            ?? throw new KeyNotFoundException($"Scheduling cycle '{schedulingCycleId}' not found for group '{groupId}'.");

        // Load all non-deleted templates for this group
        var templates = await _db.ShiftTemplates
            .Where(t => t.GroupId == groupId && !t.IsDeleted)
            .ToListAsync(ct);

        if (templates.Count == 0)
        {
            _logger.LogWarning(
                "No active shift templates found for group {GroupId} when generating slots for cycle {CycleId}.",
                groupId, schedulingCycleId);
            return;
        }

        // Load active GroupTask IDs for this group to validate template references
        var activeGroupTaskIds = await _db.GroupTasks
            .Where(t => t.GroupId == groupId && t.IsActive)
            .Select(t => t.Id)
            .ToListAsync(ct);

        var activeGroupTaskIdSet = activeGroupTaskIds.ToHashSet();

        // Load existing slots for this cycle to support idempotent generation
        var existingSlotKeys = await _db.ShiftSlots
            .Where(s => s.SchedulingCycleId == schedulingCycleId && s.GroupId == groupId)
            .Select(s => new { s.ShiftTemplateId, s.Date })
            .ToListAsync(ct);

        var existingSlotKeySet = existingSlotKeys
            .Select(k => (k.ShiftTemplateId, k.Date))
            .ToHashSet();

        // Determine the date range from the cycle
        var cycleStartDate = DateOnly.FromDateTime(cycle.StartsAt);
        var cycleEndDate = DateOnly.FromDateTime(cycle.EndsAt);

        // Generate slots for each template
        foreach (var template in templates)
        {
            // Requirement 3.6: Skip templates referencing inactive GroupTasks
            if (!activeGroupTaskIdSet.Contains(template.GroupTaskId))
            {
                _logger.LogWarning(
                    "Shift template {TemplateId} references inactive or missing GroupTask {GroupTaskId}. Skipping during slot generation for cycle {CycleId}.",
                    template.Id, template.GroupTaskId, schedulingCycleId);
                continue;
            }

            // Generate a slot for each date in the cycle that matches the template's DayOfWeek
            for (var date = cycleStartDate; date < cycleEndDate; date = date.AddDays(1))
            {
                if (date.DayOfWeek != template.DayOfWeek)
                    continue;

                // Requirement 3.3: Idempotent — skip if slot already exists for this template+date
                if (existingSlotKeySet.Contains((template.Id, date)))
                    continue;

                // Requirement 3.2: Generate slot with Open status, zero fill, capacity from template
                var slot = ShiftSlot.Create(
                    spaceId: template.SpaceId,
                    groupId: template.GroupId,
                    groupTaskId: template.GroupTaskId,
                    shiftTemplateId: template.Id,
                    schedulingCycleId: schedulingCycleId,
                    date: date,
                    startTime: template.StartTime,
                    endTime: template.EndTime,
                    capacity: template.RequiredHeadcount);

                _db.ShiftSlots.Add(slot);
            }
        }

        // Mark the cycle as generated
        cycle.MarkGenerated();

        await _db.SaveChangesAsync(ct);
    }
}
