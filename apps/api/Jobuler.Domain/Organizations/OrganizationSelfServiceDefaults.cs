using Jobuler.Domain.Common;
using Jobuler.Domain.Groups;

namespace Jobuler.Domain.Organizations;

/// <summary>
/// Organization-level template used when spaces do not define their own
/// self-service defaults.
/// </summary>
public class OrganizationSelfServiceDefaults : AuditableEntity
{
    public Guid OrganizationId { get; private set; }
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

    private OrganizationSelfServiceDefaults() { }

    public static OrganizationSelfServiceDefaults Create(
        Guid organizationId,
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
        var defaults = new OrganizationSelfServiceDefaults { OrganizationId = organizationId };
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
            Guid.NewGuid(),
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

    public SelfServiceConfig ToConfig(Guid spaceId, Guid groupId)
    {
        var config = SelfServiceConfig.Create(
            spaceId,
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
