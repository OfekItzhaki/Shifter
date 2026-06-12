using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.SelfService;

public static class SpecialDaySelfServicePolicy
{
    public const string NoCoverageMessage =
        "This slot is on a special day that is not configured for coverage.";

    public static Task<bool> IsMemberActionBlockedAsync(
        AppDbContext db,
        ShiftSlot slot,
        CancellationToken ct) =>
        db.SpaceSpecialDays
            .AsNoTracking()
            .AnyAsync(d => d.SpaceId == slot.SpaceId
                           && d.Date == slot.Date
                           && !d.RequiresCoverage, ct);
}
