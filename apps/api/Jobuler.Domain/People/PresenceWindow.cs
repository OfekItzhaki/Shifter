using Jobuler.Domain.Common;

namespace Jobuler.Domain.People;

public enum PresenceState
{
    FreeInBase,
    AtHome,
    OnMission   // auto-derived from assignments; not manually set
}

/// <summary>
/// Tracks where a person physically is over a time window.
/// FreeInBase and AtHome are manually set by admins.
/// OnMission is auto-derived from task slot assignments.
/// </summary>
public class PresenceWindow : Entity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid PersonId { get; private set; }
    public PresenceState State { get; private set; }
    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    public string? Note { get; private set; }
    public bool IsDerived { get; private set; }  // true = auto-derived from assignment
    public Guid? UnavailabilityReasonId { get; private set; }

    private PresenceWindow() { }

    public static PresenceWindow CreateManual(
        Guid spaceId, Guid personId, PresenceState state,
        DateTime startsAt, DateTime endsAt, string? note = null,
        Guid? unavailabilityReasonId = null)
    {
        if (state == PresenceState.OnMission)
            throw new InvalidOperationException("OnMission state must be derived, not manually set.");
        if (endsAt <= startsAt)
            throw new ArgumentException("EndsAt must be after StartsAt.");

        return new PresenceWindow
        {
            SpaceId = spaceId,
            PersonId = personId,
            State = state,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Note = note?.Trim(),
            IsDerived = false,
            UnavailabilityReasonId = unavailabilityReasonId
        };
    }

    public static PresenceWindow CreateDerived(
        Guid spaceId, Guid personId, DateTime startsAt, DateTime endsAt) =>
        new()
        {
            SpaceId = spaceId,
            PersonId = personId,
            State = PresenceState.OnMission,
            StartsAt = startsAt,
            EndsAt = endsAt,
            IsDerived = true
        };

    /// <summary>
    /// Creates a derived AtHome presence window from a solver home-leave assignment.
    /// These are auto-created when a schedule version with home-leave assignments is published.
    /// </summary>
    public static PresenceWindow CreateDerivedAtHome(
        Guid spaceId, Guid personId, DateTime startsAt, DateTime endsAt)
    {
        if (endsAt <= startsAt)
            throw new ArgumentException("EndsAt must be after StartsAt.");

        return new PresenceWindow
        {
            SpaceId = spaceId,
            PersonId = personId,
            State = PresenceState.AtHome,
            StartsAt = startsAt,
            EndsAt = endsAt,
            IsDerived = true
        };
    }

    /// <summary>
    /// Truncates this presence window to end at the specified timestamp.
    /// Used when cancelling an in-progress home-leave (starts_at in past, ends_at in future).
    /// </summary>
    public void Truncate(DateTime newEndsAt)
    {
        if (newEndsAt <= StartsAt)
            throw new ArgumentException("New EndsAt must be after StartsAt.");
        if (newEndsAt >= EndsAt)
            throw new ArgumentException("New EndsAt must be before current EndsAt (truncation only).");

        EndsAt = newEndsAt;
    }
}
