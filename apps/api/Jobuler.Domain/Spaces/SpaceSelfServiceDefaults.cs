using Jobuler.Domain.Common;
using Jobuler.Domain.Groups;

namespace Jobuler.Domain.Spaces;

/// <summary>
/// Space-level template used when a group is first switched to self-service scheduling.
/// Existing group configs remain independent after creation.
/// </summary>
public class SpaceSelfServiceDefaults : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public int MinShiftsPerCycle { get; private set; } = 0;
    public int MaxShiftsPerCycle { get; private set; } = 7;
    public int RequestWindowOpenOffsetHours { get; private set; } = 168;
    public int RequestWindowCloseOffsetHours { get; private set; } = 24;
    public int CancellationCutoffHours { get; private set; } = 24;
    public int MaxAbsencesPerCycle { get; private set; } = 3;
    public int MaxLateCancellationsPerCycle { get; private set; } = 2;
    public int LateCancellationWindowHours { get; private set; } = 24;
    public int WaitlistOfferMinutes { get; private set; } = 60;
    public int CycleDurationDays { get; private set; } = 7;
    public bool AllowMemberShiftClaims { get; private set; } = true;
    public bool AllowWaitlist { get; private set; } = true;
    public bool AllowShiftChangeRequests { get; private set; } = true;
    public bool AllowAbsenceReports { get; private set; } = true;
    public bool AllowShiftSwaps { get; private set; } = true;

    private SpaceSelfServiceDefaults() { }

    public static SpaceSelfServiceDefaults Create(
        Guid spaceId,
        int minShiftsPerCycle,
        int maxShiftsPerCycle,
        int requestWindowOpenOffsetHours,
        int requestWindowCloseOffsetHours,
        int cancellationCutoffHours,
        int maxAbsencesPerCycle,
        int maxLateCancellationsPerCycle,
        int lateCancellationWindowHours,
        int waitlistOfferMinutes,
        int cycleDurationDays,
        bool allowMemberShiftClaims,
        bool allowWaitlist,
        bool allowShiftChangeRequests,
        bool allowAbsenceReports,
        bool allowShiftSwaps)
    {
        var defaults = new SpaceSelfServiceDefaults { SpaceId = spaceId };
        defaults.Update(
            minShiftsPerCycle,
            maxShiftsPerCycle,
            requestWindowOpenOffsetHours,
            requestWindowCloseOffsetHours,
            cancellationCutoffHours,
            maxAbsencesPerCycle,
            maxLateCancellationsPerCycle,
            lateCancellationWindowHours,
            waitlistOfferMinutes,
            cycleDurationDays,
            allowMemberShiftClaims,
            allowWaitlist,
            allowShiftChangeRequests,
            allowAbsenceReports,
            allowShiftSwaps);
        return defaults;
    }

    public void Update(
        int minShiftsPerCycle,
        int maxShiftsPerCycle,
        int requestWindowOpenOffsetHours,
        int requestWindowCloseOffsetHours,
        int cancellationCutoffHours,
        int maxAbsencesPerCycle,
        int maxLateCancellationsPerCycle,
        int lateCancellationWindowHours,
        int waitlistOfferMinutes,
        int cycleDurationDays,
        bool allowMemberShiftClaims,
        bool allowWaitlist,
        bool allowShiftChangeRequests,
        bool allowAbsenceReports,
        bool allowShiftSwaps)
    {
        var config = SelfServiceConfig.Create(
            SpaceId,
            Guid.NewGuid(),
            minShiftsPerCycle,
            maxShiftsPerCycle,
            requestWindowOpenOffsetHours,
            requestWindowCloseOffsetHours,
            cancellationCutoffHours,
            maxLateCancellationsPerCycle,
            lateCancellationWindowHours,
            waitlistOfferMinutes,
            cycleDurationDays);

        config.SetAbsenceReportLimit(maxAbsencesPerCycle);

        MinShiftsPerCycle = config.MinShiftsPerCycle;
        MaxShiftsPerCycle = config.MaxShiftsPerCycle;
        RequestWindowOpenOffsetHours = config.RequestWindowOpenOffsetHours;
        RequestWindowCloseOffsetHours = config.RequestWindowCloseOffsetHours;
        CancellationCutoffHours = config.CancellationCutoffHours;
        MaxAbsencesPerCycle = config.MaxAbsencesPerCycle;
        MaxLateCancellationsPerCycle = config.MaxLateCancellationsPerCycle;
        LateCancellationWindowHours = config.LateCancellationWindowHours;
        WaitlistOfferMinutes = config.WaitlistOfferMinutes;
        CycleDurationDays = config.CycleDurationDays;
        AllowMemberShiftClaims = allowMemberShiftClaims;
        AllowWaitlist = allowWaitlist;
        AllowShiftChangeRequests = allowShiftChangeRequests;
        AllowAbsenceReports = allowAbsenceReports;
        AllowShiftSwaps = allowShiftSwaps;
        Touch();
    }

    public SelfServiceConfig ToConfig(Guid groupId)
    {
        var config = SelfServiceConfig.Create(
            SpaceId,
            groupId,
            MinShiftsPerCycle,
            MaxShiftsPerCycle,
            RequestWindowOpenOffsetHours,
            RequestWindowCloseOffsetHours,
            CancellationCutoffHours,
            MaxLateCancellationsPerCycle,
            LateCancellationWindowHours,
            WaitlistOfferMinutes,
            CycleDurationDays);

        config.SetAbsenceReportLimit(MaxAbsencesPerCycle);
        config.SetWorkflowPermissions(
            AllowMemberShiftClaims,
            AllowWaitlist,
            AllowShiftChangeRequests,
            AllowAbsenceReports,
            AllowShiftSwaps);

        return config;
    }
}
