using Jobuler.Domain.Groups;

namespace Jobuler.Application.Scheduling.SelfService;

public class SelfServiceDefaultPolicyOptions
{
    public int MinShiftsPerCycle { get; set; } = 0;
    public int MaxShiftsPerCycle { get; set; } = 7;
    public int RequestWindowOpenOffsetHours { get; set; } = 168;
    public int RequestWindowCloseOffsetHours { get; set; } = 24;
    public int CancellationCutoffHours { get; set; } = 24;
    public int MaxAbsencesPerCycle { get; set; } = 3;
    public int MaxLateCancellationsPerCycle { get; set; } = 2;
    public int LateCancellationWindowHours { get; set; } = 24;
    public int WaitlistOfferMinutes { get; set; } = 60;
    public int CycleDurationDays { get; set; } = 7;
    public bool AllowMemberShiftClaims { get; set; } = true;
    public bool AllowWaitlist { get; set; } = true;
    public bool AllowShiftChangeRequests { get; set; } = true;
    public bool AllowAbsenceReports { get; set; } = true;
    public bool AllowShiftSwaps { get; set; } = true;

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
