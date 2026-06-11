using Jobuler.Domain.Common;

namespace Jobuler.Domain.Groups;

/// <summary>
/// Group-level configuration for self-service scheduling.
/// Defines min/max shift constraints, request window offsets,
/// cancellation cutoff, waitlist offer duration, and cycle length.
/// One-to-one relationship with Group (unique on GroupId).
/// </summary>
public class SelfServiceConfig : AuditableEntity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid GroupId { get; private set; }
    public int MinShiftsPerCycle { get; private set; } = 0;
    public int MaxShiftsPerCycle { get; private set; } = 7;
    public int RequestWindowOpenOffsetHours { get; private set; } = 168; // 7 days before cycle start
    public int RequestWindowCloseOffsetHours { get; private set; } = 24; // 1 day before cycle start
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

    private SelfServiceConfig() { }

    public static SelfServiceConfig Create(Guid spaceId, Guid groupId)
    {
        return new SelfServiceConfig
        {
            SpaceId = spaceId,
            GroupId = groupId
        };
    }

    public static SelfServiceConfig Create(
        Guid spaceId,
        Guid groupId,
        int minShiftsPerCycle,
        int maxShiftsPerCycle,
        int requestWindowOpenOffsetHours,
        int requestWindowCloseOffsetHours,
        int cancellationCutoffHours,
        int waitlistOfferMinutes,
        int cycleDurationDays) =>
        Create(
            spaceId,
            groupId,
            minShiftsPerCycle,
            maxShiftsPerCycle,
            requestWindowOpenOffsetHours,
            requestWindowCloseOffsetHours,
            cancellationCutoffHours,
            maxLateCancellationsPerCycle: 2,
            lateCancellationWindowHours: 24,
            waitlistOfferMinutes,
            cycleDurationDays);

    public static SelfServiceConfig Create(
        Guid spaceId,
        Guid groupId,
        int minShiftsPerCycle,
        int maxShiftsPerCycle,
        int requestWindowOpenOffsetHours,
        int requestWindowCloseOffsetHours,
        int cancellationCutoffHours,
        int maxLateCancellationsPerCycle,
        int lateCancellationWindowHours,
        int waitlistOfferMinutes,
        int cycleDurationDays)
    {
        var config = new SelfServiceConfig
        {
            SpaceId = spaceId,
            GroupId = groupId
        };

        config.SetMinMaxShifts(minShiftsPerCycle, maxShiftsPerCycle);
        config.SetRequestWindowOffsets(requestWindowOpenOffsetHours, requestWindowCloseOffsetHours);
        config.SetCancellationCutoffHours(cancellationCutoffHours);
        config.SetLateCancellationLimits(maxLateCancellationsPerCycle, lateCancellationWindowHours);
        config.SetWaitlistOfferMinutes(waitlistOfferMinutes);
        config.SetCycleDurationDays(cycleDurationDays);

        return config;
    }

    public void Update(
        int minShiftsPerCycle,
        int maxShiftsPerCycle,
        int requestWindowOpenOffsetHours,
        int requestWindowCloseOffsetHours,
        int cancellationCutoffHours,
        int waitlistOfferMinutes,
        int cycleDurationDays) =>
        Update(
            minShiftsPerCycle,
            maxShiftsPerCycle,
            requestWindowOpenOffsetHours,
            requestWindowCloseOffsetHours,
            cancellationCutoffHours,
            MaxLateCancellationsPerCycle,
            LateCancellationWindowHours,
            waitlistOfferMinutes,
            cycleDurationDays);

    public void Update(
        int minShiftsPerCycle,
        int maxShiftsPerCycle,
        int requestWindowOpenOffsetHours,
        int requestWindowCloseOffsetHours,
        int cancellationCutoffHours,
        int maxLateCancellationsPerCycle,
        int lateCancellationWindowHours,
        int waitlistOfferMinutes,
        int cycleDurationDays) =>
        Update(
            minShiftsPerCycle,
            maxShiftsPerCycle,
            requestWindowOpenOffsetHours,
            requestWindowCloseOffsetHours,
            cancellationCutoffHours,
            maxLateCancellationsPerCycle,
            lateCancellationWindowHours,
            waitlistOfferMinutes,
            cycleDurationDays,
            AllowMemberShiftClaims,
            AllowWaitlist,
            AllowShiftChangeRequests,
            AllowAbsenceReports,
            AllowShiftSwaps,
            MaxAbsencesPerCycle);

    public void Update(
        int minShiftsPerCycle,
        int maxShiftsPerCycle,
        int requestWindowOpenOffsetHours,
        int requestWindowCloseOffsetHours,
        int cancellationCutoffHours,
        int maxLateCancellationsPerCycle,
        int lateCancellationWindowHours,
        int waitlistOfferMinutes,
        int cycleDurationDays,
        bool allowMemberShiftClaims,
        bool allowWaitlist,
        bool allowShiftChangeRequests,
        bool allowAbsenceReports,
        bool allowShiftSwaps) =>
        Update(
            minShiftsPerCycle,
            maxShiftsPerCycle,
            requestWindowOpenOffsetHours,
            requestWindowCloseOffsetHours,
            cancellationCutoffHours,
            maxLateCancellationsPerCycle,
            lateCancellationWindowHours,
            waitlistOfferMinutes,
            cycleDurationDays,
            allowMemberShiftClaims,
            allowWaitlist,
            allowShiftChangeRequests,
            allowAbsenceReports,
            allowShiftSwaps,
            MaxAbsencesPerCycle);

    public void Update(
        int minShiftsPerCycle,
        int maxShiftsPerCycle,
        int requestWindowOpenOffsetHours,
        int requestWindowCloseOffsetHours,
        int cancellationCutoffHours,
        int maxLateCancellationsPerCycle,
        int lateCancellationWindowHours,
        int waitlistOfferMinutes,
        int cycleDurationDays,
        bool allowMemberShiftClaims,
        bool allowWaitlist,
        bool allowShiftChangeRequests,
        bool allowAbsenceReports,
        bool allowShiftSwaps,
        int maxAbsencesPerCycle)
    {
        SetMinMaxShifts(minShiftsPerCycle, maxShiftsPerCycle);
        SetRequestWindowOffsets(requestWindowOpenOffsetHours, requestWindowCloseOffsetHours);
        SetCancellationCutoffHours(cancellationCutoffHours);
        SetAbsenceReportLimit(maxAbsencesPerCycle);
        SetLateCancellationLimits(maxLateCancellationsPerCycle, lateCancellationWindowHours);
        SetWaitlistOfferMinutes(waitlistOfferMinutes);
        SetCycleDurationDays(cycleDurationDays);
        SetWorkflowPermissions(
            allowMemberShiftClaims,
            allowWaitlist,
            allowShiftChangeRequests,
            allowAbsenceReports,
            allowShiftSwaps);
        Touch();
    }

    public void SetMinMaxShifts(int min, int max)
    {
        if (min < 0 || min > 100)
            throw new InvalidOperationException("Min shifts per cycle must be between 0 and 100.");

        if (max < 1 || max > 100)
            throw new InvalidOperationException("Max shifts per cycle must be between 1 and 100.");

        if (min > max)
            throw new InvalidOperationException("Min shifts per cycle must be less than or equal to max shifts per cycle.");

        MinShiftsPerCycle = min;
        MaxShiftsPerCycle = max;
        Touch();
    }

    public void SetRequestWindowOffsets(int openOffsetHours, int closeOffsetHours)
    {
        if (openOffsetHours < 1 || openOffsetHours > 720)
            throw new InvalidOperationException("Request window open offset must be between 1 and 720 hours.");

        if (closeOffsetHours < 1 || closeOffsetHours > 720)
            throw new InvalidOperationException("Request window close offset must be between 1 and 720 hours.");

        if (openOffsetHours <= closeOffsetHours)
            throw new InvalidOperationException("Request window open offset must be greater than close offset (open time must be before close time).");

        RequestWindowOpenOffsetHours = openOffsetHours;
        RequestWindowCloseOffsetHours = closeOffsetHours;
        Touch();
    }

    public void SetCancellationCutoffHours(int hours)
    {
        if (hours < 1 || hours > 720)
            throw new InvalidOperationException("Cancellation cutoff must be between 1 and 720 hours.");

        CancellationCutoffHours = hours;
        Touch();
    }

    public void SetLateCancellationLimits(int maxPerCycle, int windowHours)
    {
        if (maxPerCycle < 0 || maxPerCycle > 100)
            throw new InvalidOperationException("Max late cancellations per cycle must be between 0 and 100.");

        if (windowHours < 1 || windowHours > 720)
            throw new InvalidOperationException("Late cancellation window must be between 1 and 720 hours.");

        MaxLateCancellationsPerCycle = maxPerCycle;
        LateCancellationWindowHours = windowHours;
        Touch();
    }

    public void SetAbsenceReportLimit(int maxPerCycle)
    {
        if (maxPerCycle < 0 || maxPerCycle > 100)
            throw new InvalidOperationException("Max absence reports per cycle must be between 0 and 100.");

        MaxAbsencesPerCycle = maxPerCycle;
        Touch();
    }

    public void SetWaitlistOfferMinutes(int minutes)
    {
        if (minutes < 15 || minutes > 1440)
            throw new InvalidOperationException("Waitlist offer duration must be between 15 and 1440 minutes.");

        WaitlistOfferMinutes = minutes;
        Touch();
    }

    public void SetCycleDurationDays(int days)
    {
        if (days < 1 || days > 30)
            throw new InvalidOperationException("Cycle duration must be between 1 and 30 days.");

        CycleDurationDays = days;
        Touch();
    }

    public void SetWorkflowPermissions(
        bool allowMemberShiftClaims,
        bool allowWaitlist,
        bool allowShiftChangeRequests,
        bool allowAbsenceReports,
        bool allowShiftSwaps)
    {
        AllowMemberShiftClaims = allowMemberShiftClaims;
        AllowWaitlist = allowWaitlist;
        AllowShiftChangeRequests = allowShiftChangeRequests;
        AllowAbsenceReports = allowAbsenceReports;
        AllowShiftSwaps = allowShiftSwaps;
        Touch();
    }
}
