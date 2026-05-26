using Jobuler.Domain.Common;

namespace Jobuler.Domain.Scheduling;

/// <summary>
/// Represents a scheduling period (typically one week) for which shift slots
/// are generated and shift requests are collected in self-service mode.
/// </summary>
public class SchedulingCycle : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid GroupId { get; private set; }
    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    public DateTime RequestWindowOpensAt { get; private set; }
    public DateTime RequestWindowClosesAt { get; private set; }
    public bool IsGenerated { get; private set; }

    private SchedulingCycle() { }

    public static SchedulingCycle Create(
        Guid spaceId,
        Guid groupId,
        DateTime startsAt,
        DateTime endsAt,
        DateTime requestWindowOpensAt,
        DateTime requestWindowClosesAt)
    {
        if (endsAt <= startsAt)
            throw new InvalidOperationException("Cycle end must be after cycle start.");

        if (requestWindowOpensAt >= requestWindowClosesAt)
            throw new InvalidOperationException("Request window open must be before request window close.");

        if (requestWindowClosesAt > startsAt)
            throw new InvalidOperationException("Request window must close no later than the cycle start.");

        return new()
        {
            SpaceId = spaceId,
            GroupId = groupId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            RequestWindowOpensAt = requestWindowOpensAt,
            RequestWindowClosesAt = requestWindowClosesAt,
            IsGenerated = false
        };
    }

    /// <summary>
    /// Marks that shift slots have been generated for this cycle.
    /// </summary>
    public void MarkGenerated()
    {
        IsGenerated = true;
        Touch();
    }

    /// <summary>
    /// Updates the request window close time. Used when an admin adjusts
    /// the window while it is currently open.
    /// </summary>
    public void UpdateRequestWindowClose(DateTime newCloseAt)
    {
        if (newCloseAt <= RequestWindowOpensAt)
            throw new InvalidOperationException("Request window close must be after request window open.");

        RequestWindowClosesAt = newCloseAt;
        Touch();
    }

    /// <summary>
    /// Returns true if the request window is currently open at the given time.
    /// </summary>
    public bool IsRequestWindowOpen(DateTime utcNow) =>
        utcNow >= RequestWindowOpensAt && utcNow <= RequestWindowClosesAt;
}
