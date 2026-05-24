using Jobuler.Domain.Common;
using Jobuler.Domain.Groups;

namespace Jobuler.Domain.Spaces;

/// <summary>
/// Space-level home-leave configuration that applies to all closed-base groups within the space.
/// When present, overrides group-level home-leave settings for solver payload generation.
/// One-to-one relationship with Space (unique on SpaceId).
/// </summary>
public class SpaceHomeLeaveConfig : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public HomeLeaveMode Mode { get; private set; } = HomeLeaveMode.Automatic;
    public int BalanceValue { get; private set; } = 50;
    public int BaseDays { get; private set; } = 7;
    public int HomeDays { get; private set; } = 2;
    public int MinPeopleAtBase { get; private set; } = 8;
    public decimal MinRestHours { get; private set; }
    public decimal EligibilityThresholdHours { get; private set; }
    public int LeaveCapacity { get; private set; }
    public decimal LeaveDurationHours { get; private set; }
    public bool EmergencyFreezeActive { get; private set; }
    public bool EmergencyUseForScheduling { get; private set; }
    public DateTime? FreezeStartedAt { get; private set; }
    public HomeLeaveMode PreFreezeMode { get; private set; } = HomeLeaveMode.Automatic;

    private SpaceHomeLeaveConfig() { }

    public static SpaceHomeLeaveConfig Create(
        Guid spaceId,
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

        return new SpaceHomeLeaveConfig
        {
            SpaceId = spaceId,
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

    public void SetMode(HomeLeaveMode mode)
    {
        Mode = mode;
        Touch();
    }

    public void SetBalanceValue(int balanceValue)
    {
        ValidateBalanceValue(balanceValue);
        BalanceValue = balanceValue;
        Touch();
    }

    public void SetBaseDays(int baseDays)
    {
        ValidateBaseDays(baseDays);
        BaseDays = baseDays;
        Touch();
    }

    public void SetHomeDays(int homeDays)
    {
        ValidateHomeDays(homeDays);
        HomeDays = homeDays;
        Touch();
    }

    public void SetMinPeopleAtBase(int minPeopleAtBase)
    {
        ValidateMinPeopleAtBase(minPeopleAtBase);
        MinPeopleAtBase = minPeopleAtBase;
        Touch();
    }

    public void SetMinRestHours(decimal minRestHours)
    {
        ValidateMinRestHours(minRestHours);
        MinRestHours = minRestHours;
        Touch();
    }

    public void SetEligibilityThresholdHours(decimal eligibilityThresholdHours)
    {
        ValidateEligibilityThresholdHours(eligibilityThresholdHours);
        EligibilityThresholdHours = eligibilityThresholdHours;
        Touch();
    }

    public void SetLeaveCapacity(int leaveCapacity)
    {
        ValidateLeaveCapacity(leaveCapacity);
        LeaveCapacity = leaveCapacity;
        Touch();
    }

    public void SetLeaveDurationHours(decimal leaveDurationHours)
    {
        ValidateLeaveDurationHours(leaveDurationHours);
        LeaveDurationHours = leaveDurationHours;
        Touch();
    }

    public void SetEmergencyFreezeActive(bool active)
    {
        EmergencyFreezeActive = active;
        Touch();
    }

    public void SetEmergencyUseForScheduling(bool useForScheduling)
    {
        EmergencyUseForScheduling = useForScheduling;
        Touch();
    }

    public void SetFreezeStartedAt(DateTime? freezeStartedAt)
    {
        FreezeStartedAt = freezeStartedAt;
        Touch();
    }

    public void SetPreFreezeMode(HomeLeaveMode preFreezeMode)
    {
        PreFreezeMode = preFreezeMode;
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
            throw new InvalidOperationException("Min rest hours must be between 0 and 16.");
    }

    private static void ValidateEligibilityThresholdHours(decimal value)
    {
        if (value < 0 || value > 9999)
            throw new InvalidOperationException("Eligibility threshold hours must be between 0 and 9999.");
    }

    private static void ValidateLeaveCapacity(int value)
    {
        if (value < 1)
            throw new InvalidOperationException("Leave capacity must be at least 1.");
    }

    private static void ValidateLeaveDurationHours(decimal value)
    {
        if (value < 12 || value > 168)
            throw new InvalidOperationException("Leave duration must be between 12 and 168 hours.");
    }

    private static void ValidateBalanceValue(int value)
    {
        if (value < 0 || value > 100)
            throw new InvalidOperationException("Balance value must be between 0 and 100.");
    }

    private static void ValidateBaseDays(int value)
    {
        if (value < 1)
            throw new InvalidOperationException("Base days must be at least 1.");
    }

    private static void ValidateHomeDays(int value)
    {
        if (value < 1)
            throw new InvalidOperationException("Home days must be at least 1.");
    }

    private static void ValidateMinPeopleAtBase(int value)
    {
        if (value < 1)
            throw new InvalidOperationException("Min people at base must be at least 1.");
    }
}
