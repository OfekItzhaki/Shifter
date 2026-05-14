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
    public int BalanceValue { get; private set; } = 50;

    private HomeLeaveConfig() { }

    public static HomeLeaveConfig Create(
        Guid spaceId,
        Guid groupId,
        decimal minRestHours,
        decimal eligibilityThresholdHours,
        int leaveCapacity,
        decimal leaveDurationHours,
        int balanceValue = 50)
    {
        ValidateMinRestHours(minRestHours);
        ValidateEligibilityThresholdHours(eligibilityThresholdHours, minRestHours);
        ValidateLeaveCapacity(leaveCapacity);
        ValidateLeaveDurationHours(leaveDurationHours);
        ValidateBalanceValue(balanceValue);

        return new HomeLeaveConfig
        {
            SpaceId = spaceId,
            GroupId = groupId,
            MinRestHours = minRestHours,
            EligibilityThresholdHours = eligibilityThresholdHours,
            LeaveCapacity = leaveCapacity,
            LeaveDurationHours = leaveDurationHours,
            BalanceValue = balanceValue
        };
    }

    public void Update(
        decimal minRestHours,
        decimal eligibilityThresholdHours,
        int leaveCapacity,
        decimal leaveDurationHours,
        int? balanceValue = null)
    {
        ValidateMinRestHours(minRestHours);
        ValidateEligibilityThresholdHours(eligibilityThresholdHours, minRestHours);
        ValidateLeaveCapacity(leaveCapacity);
        ValidateLeaveDurationHours(leaveDurationHours);

        if (balanceValue.HasValue)
        {
            ValidateBalanceValue(balanceValue.Value);
            BalanceValue = balanceValue.Value;
        }

        MinRestHours = minRestHours;
        EligibilityThresholdHours = eligibilityThresholdHours;
        LeaveCapacity = leaveCapacity;
        LeaveDurationHours = leaveDurationHours;
        Touch();
    }

    private static void ValidateMinRestHours(decimal value)
    {
        if (value < 0 || value > 16)
            throw new InvalidOperationException("שעות מנוחה חייבות להיות בין 0 ל-16.");
    }

    private static void ValidateEligibilityThresholdHours(decimal value, decimal minRestHours)
    {
        if (value < 0 || value > 336)
            throw new InvalidOperationException("זמן בבסיס לפני יציאה חייב להיות בין 0 ל-336 שעות (14 ימים).");
    }

    private static void ValidateLeaveCapacity(int value)
    {
        if (value < 1)
            throw new InvalidOperationException("מכסת היוצאים חייבת להיות לפחות 1.");
    }

    private static void ValidateLeaveDurationHours(decimal value)
    {
        if (value < 12 || value > 168)
            throw new InvalidOperationException("משך החופשה חייב להיות בין 12 ל-168 שעות.");
    }

    private static void ValidateBalanceValue(int value)
    {
        if (value < 0 || value > 100)
            throw new InvalidOperationException("ערך האיזון חייב להיות בין 0 ל-100");
    }
}
