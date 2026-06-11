using Jobuler.Domain.Scheduling;

namespace Jobuler.Application.Scheduling.SelfService;

internal static class ShiftSlotTimeRange
{
    public static DateTime StartsAtUtc(ShiftSlot slot) =>
        slot.Date.ToDateTime(slot.StartTime, DateTimeKind.Utc);

    public static DateTime EndsAtUtc(ShiftSlot slot)
    {
        var startsAt = StartsAtUtc(slot);
        var endsAt = slot.Date.ToDateTime(slot.EndTime, DateTimeKind.Utc);
        return endsAt <= startsAt ? endsAt.AddDays(1) : endsAt;
    }

    public static bool Overlaps(ShiftSlot left, ShiftSlot right) =>
        StartsAtUtc(left) < EndsAtUtc(right) && StartsAtUtc(right) < EndsAtUtc(left);

    public static TimeSpan GapBetween(ShiftSlot left, ShiftSlot right)
    {
        var leftStartsAt = StartsAtUtc(left);
        var leftEndsAt = EndsAtUtc(left);
        var rightStartsAt = StartsAtUtc(right);
        var rightEndsAt = EndsAtUtc(right);

        if (leftEndsAt <= rightStartsAt)
            return rightStartsAt - leftEndsAt;

        if (rightEndsAt <= leftStartsAt)
            return leftStartsAt - rightEndsAt;

        return TimeSpan.Zero;
    }
}
