using Jobuler.Domain.Common;

namespace Jobuler.Domain.Groups;

/// <summary>
/// Home-leave configuration for a closed-base group.
/// Defines the parameters the solver uses to schedule leave rotations:
/// minimum rest between missions, eligibility threshold for leave,
/// concurrent leave capacity, and leave duration.
/// Supports three operating modes: Automatic (slider-based), Manual (explicit days), and Emergency Freeze.
/// One-to-one relationship with Group (unique on GroupId).
/// </summary>
public class HomeLeaveConfig : AuditableEntity, ITenantScoped
{
    // Existing fields
    public Guid SpaceId { get; private set; }
    public Guid GroupId { get; private set; }
    public decimal MinRestHours { get; private set; }
    public decimal EligibilityThresholdHours { get; private set; }
    public int LeaveCapacity { get; private set; }
    public decimal LeaveDurationHours { get; private set; }
    public int BalanceValue { get; private set; } = 50;
    public int MinPeopleAtBase { get; private set; } = 8;

    // New fields — mode system
    public HomeLeaveMode Mode { get; private set; } = HomeLeaveMode.Automatic;
    public int BaseDays { get; private set; } = 7;
    public int HomeDays { get; private set; } = 2;

    // Emergency freeze fields
    public bool EmergencyFreezeActive { get; private set; }
    public bool EmergencyUseForScheduling { get; private set; }
    public DateTime? FreezeStartedAt { get; private set; }
    public HomeLeaveMode PreFreezeMode { get; private set; } = HomeLeaveMode.Automatic;

    private HomeLeaveConfig() { }

    public static HomeLeaveConfig Create(
        Guid spaceId,
        Guid groupId,
        decimal minRestHours,
        decimal eligibilityThresholdHours,
        int leaveCapacity,
        decimal leaveDurationHours,
        int balanceValue = 50,
        HomeLeaveMode mode = HomeLeaveMode.Automatic,
        int baseDays = 7,
        int homeDays = 2,
        int minPeopleAtBase = 8)
    {
        ValidateMinRestHours(minRestHours);
        ValidateEligibilityThresholdHours(eligibilityThresholdHours);
        ValidateLeaveCapacity(leaveCapacity);
        ValidateLeaveDurationHours(leaveDurationHours);
        ValidateBalanceValue(balanceValue);
        ValidateBaseDays(baseDays);
        ValidateHomeDays(homeDays);
        ValidateMinPeopleAtBase(minPeopleAtBase);

        return new HomeLeaveConfig
        {
            SpaceId = spaceId,
            GroupId = groupId,
            MinRestHours = minRestHours,
            EligibilityThresholdHours = eligibilityThresholdHours,
            LeaveCapacity = leaveCapacity,
            LeaveDurationHours = leaveDurationHours,
            BalanceValue = balanceValue,
            Mode = mode,
            BaseDays = baseDays,
            HomeDays = homeDays,
            MinPeopleAtBase = minPeopleAtBase
        };
    }

    public void Update(
        decimal minRestHours,
        decimal eligibilityThresholdHours,
        int leaveCapacity,
        decimal leaveDurationHours,
        int? balanceValue = null,
        HomeLeaveMode? mode = null,
        int? baseDays = null,
        int? homeDays = null,
        int? minPeopleAtBase = null)
    {
        ValidateMinRestHours(minRestHours);
        ValidateEligibilityThresholdHours(eligibilityThresholdHours);
        ValidateLeaveCapacity(leaveCapacity);
        ValidateLeaveDurationHours(leaveDurationHours);

        if (balanceValue.HasValue)
        {
            ValidateBalanceValue(balanceValue.Value);
            BalanceValue = balanceValue.Value;
        }

        if (baseDays.HasValue)
        {
            ValidateBaseDays(baseDays.Value);
            BaseDays = baseDays.Value;
        }

        if (homeDays.HasValue)
        {
            ValidateHomeDays(homeDays.Value);
            HomeDays = homeDays.Value;
        }

        if (minPeopleAtBase.HasValue)
        {
            ValidateMinPeopleAtBase(minPeopleAtBase.Value);
            MinPeopleAtBase = minPeopleAtBase.Value;
        }

        if (mode.HasValue)
        {
            Mode = mode.Value;
        }

        MinRestHours = minRestHours;
        EligibilityThresholdHours = eligibilityThresholdHours;
        LeaveCapacity = leaveCapacity;
        LeaveDurationHours = leaveDurationHours;
        Touch();
    }

    /// <summary>
    /// Switches the operating mode and recalculates solver parameters accordingly.
    /// </summary>
    public void SetMode(HomeLeaveMode mode)
    {
        Mode = mode;

        // Recalculate solver params based on current stored ratio
        EligibilityThresholdHours = BaseDays * 24;
        MinRestHours = 0;

        Touch();
    }

    /// <summary>
    /// Sets an explicit base:home day ratio (Manual mode).
    /// Validates inputs and converts to solver-compatible parameters.
    /// </summary>
    public void SetRatio(int baseDays, int homeDays)
    {
        ValidateBaseDays(baseDays);
        ValidateHomeDays(homeDays);

        BaseDays = baseDays;
        HomeDays = homeDays;
        EligibilityThresholdHours = baseDays * 24;
        MinRestHours = 0;

        Touch();
    }

    /// <summary>
    /// Converts a slider position (0–100) into a base:home ratio by interpolating
    /// between the optimal ratio and the extremes.
    /// Slider at 50 = optimal. Slider toward 0 = more conservative (more base days).
    /// Slider toward 100 = more generous (more home days).
    /// </summary>
    public void SetSliderPosition(int sliderValue, int optimalBaseDays, int optimalHomeDays)
    {
        if (sliderValue < 0 || sliderValue > 100)
            throw new InvalidOperationException("ערך המחוון חייב להיות בין 0 ל-100.");

        if (optimalBaseDays < 1)
            throw new InvalidOperationException("ימי בסיס אופטימליים חייבים להיות לפחות 1.");

        if (optimalHomeDays < 1)
            throw new InvalidOperationException("ימי בית אופטימליים חייבים להיות לפחות 1.");

        int baseDays;
        int homeDays;

        if (sliderValue == 50)
        {
            baseDays = optimalBaseDays;
            homeDays = optimalHomeDays;
        }
        else if (sliderValue < 50)
        {
            // More conservative: increase base days, keep home days at optimal or reduce
            // At 0: maximum base days (14), minimum home days (1)
            double t = sliderValue / 50.0;
            baseDays = (int)Math.Ceiling(14 + t * (optimalBaseDays - 14));
            homeDays = (int)Math.Max(1, Math.Round(1 + t * (optimalHomeDays - 1)));
        }
        else
        {
            // More generous: decrease base days, increase home days
            // At 100: minimum base days (1), maximum home days (7)
            double t = (sliderValue - 50) / 50.0;
            baseDays = (int)Math.Max(1, Math.Ceiling(optimalBaseDays + t * (1 - optimalBaseDays)));
            homeDays = (int)Math.Min(7, Math.Round(optimalHomeDays + t * (7 - optimalHomeDays)));
        }

        BaseDays = Math.Max(1, baseDays);
        HomeDays = Math.Max(1, homeDays);
        BalanceValue = sliderValue;
        EligibilityThresholdHours = BaseDays * 24;
        MinRestHours = 0;

        Touch();
    }

    /// <summary>
    /// Activates emergency freeze. Records the current mode as pre-freeze mode
    /// and sets the freeze state with a timestamp.
    /// </summary>
    public void ActivateEmergencyFreeze(bool useForScheduling)
    {
        if (EmergencyFreezeActive)
            throw new InvalidOperationException("Emergency freeze is already active");

        PreFreezeMode = Mode;
        EmergencyFreezeActive = true;
        EmergencyUseForScheduling = useForScheduling;
        FreezeStartedAt = DateTime.UtcNow;

        Touch();
    }

    /// <summary>
    /// Deactivates emergency freeze. Restores the mode that was active before the freeze
    /// and clears freeze state.
    /// </summary>
    public void DeactivateEmergencyFreeze()
    {
        if (!EmergencyFreezeActive)
            throw new InvalidOperationException("Emergency freeze is not active");

        Mode = PreFreezeMode;
        EmergencyFreezeActive = false;
        EmergencyUseForScheduling = false;
        FreezeStartedAt = null;

        Touch();
    }

    // --- Validation methods ---

    private static void ValidateMinRestHours(decimal value)
    {
        if (value < 0 || value > 16)
            throw new InvalidOperationException("שעות מנוחה חייבות להיות בין 0 ל-16.");
    }

    private static void ValidateEligibilityThresholdHours(decimal value)
    {
        if (value < 0 || value > 9999)
            throw new InvalidOperationException("זמן בבסיס לפני יציאה חייב להיות בין 0 ל-9999 שעות.");
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

    private static void ValidateBaseDays(int value)
    {
        if (value < 1)
            throw new InvalidOperationException("ימים בבסיס חייבים להיות לפחות 1");
    }

    private static void ValidateHomeDays(int value)
    {
        if (value < 1)
            throw new InvalidOperationException("ימים בבית חייבים להיות לפחות 1");
    }

    private static void ValidateMinPeopleAtBase(int value)
    {
        if (value < 1)
            throw new InvalidOperationException("מינימום אנשים בבסיס חייב להיות לפחות 1.");
    }
}
