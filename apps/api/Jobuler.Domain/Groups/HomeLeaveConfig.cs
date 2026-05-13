using Jobuler.Domain.Common;

namespace Jobuler.Domain.Groups;

/// <summary>
/// Home-leave configuration for a closed-base group.
/// Defines the parameters the solver uses to schedule leave rotations:
/// minimum rest between missions, eligibility threshold for leave,
/// concurrent leave capacity, and leave duration.
/// One-to-one relationship with Group (unique on GroupId).
/// </summary>
public class HomeLeaveConfig : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid GroupId { get; private set; }
    public decimal MinRestHours { get; private set; }
    public decimal EligibilityThresholdHours { get; private set; }
    public int LeaveCapacity { get; private set; }
    public decimal LeaveDurationHours { get; private set; }

    private HomeLeaveConfig() { }

    public static HomeLeaveConfig Create(
        Guid spaceId,
        Guid groupId,
        decimal minRestHours,
        decimal eligibilityThresholdHours,
        int leaveCapacity,
        decimal leaveDurationHours)
    {
        ValidateMinRestHours(minRestHours);
        ValidateEligibilityThresholdHours(eligibilityThresholdHours, minRestHours);
        ValidateLeaveCapacity(leaveCapacity);
        ValidateLeaveDurationHours(leaveDurationHours);

        return new HomeLeaveConfig
        {
            SpaceId = spaceId,
            GroupId = groupId,
            MinRestHours = minRestHours,
            EligibilityThresholdHours = eligibilityThresholdHours,
            LeaveCapacity = leaveCapacity,
            LeaveDurationHours = leaveDurationHours
        };
    }

    public void Update(
        decimal minRestHours,
        decimal eligibilityThresholdHours,
        int leaveCapacity,
        decimal leaveDurationHours)
    {
        ValidateMinRestHours(minRestHours);
        ValidateEligibilityThresholdHours(eligibilityThresholdHours, minRestHours);
        ValidateLeaveCapacity(leaveCapacity);
        ValidateLeaveDurationHours(leaveDurationHours);

        MinRestHours = minRestHours;
        EligibilityThresholdHours = eligibilityThresholdHours;
        LeaveCapacity = leaveCapacity;
        LeaveDurationHours = leaveDurationHours;
        Touch();
    }

    private static void ValidateMinRestHours(decimal value)
    {
        if (value < 4 || value > 16)
            throw new InvalidOperationException("min_rest_hours must be between 4 and 16 inclusive.");
    }

    private static void ValidateEligibilityThresholdHours(decimal value, decimal minRestHours)
    {
        if (value < minRestHours || value > 48)
            throw new InvalidOperationException($"eligibility_threshold_hours must be between {minRestHours} and 48 inclusive.");
    }

    private static void ValidateLeaveCapacity(int value)
    {
        if (value < 1)
            throw new InvalidOperationException("leave_capacity must be at least 1.");
    }

    private static void ValidateLeaveDurationHours(decimal value)
    {
        if (value < 12 || value > 168)
            throw new InvalidOperationException("leave_duration_hours must be between 12 and 168 inclusive.");
    }
}
