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

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        AddRangeError(errors, nameof(MinShiftsPerCycle), MinShiftsPerCycle, 0, 100);
        AddRangeError(errors, nameof(MaxShiftsPerCycle), MaxShiftsPerCycle, 1, 100);

        if (MinShiftsPerCycle > MaxShiftsPerCycle)
        {
            errors.Add($"{nameof(MinShiftsPerCycle)} must be less than or equal to {nameof(MaxShiftsPerCycle)}.");
        }

        AddRangeError(errors, nameof(RequestWindowOpenOffsetHours), RequestWindowOpenOffsetHours, 1, 720);
        AddRangeError(errors, nameof(RequestWindowCloseOffsetHours), RequestWindowCloseOffsetHours, 1, 720);

        if (RequestWindowOpenOffsetHours <= RequestWindowCloseOffsetHours)
        {
            errors.Add($"{nameof(RequestWindowOpenOffsetHours)} must be greater than {nameof(RequestWindowCloseOffsetHours)}.");
        }

        AddRangeError(errors, nameof(CancellationCutoffHours), CancellationCutoffHours, 1, 720);
        AddRangeError(errors, nameof(MaxAbsencesPerCycle), MaxAbsencesPerCycle, 0, 100);
        AddRangeError(errors, nameof(MaxLateCancellationsPerCycle), MaxLateCancellationsPerCycle, 0, 100);
        AddRangeError(errors, nameof(LateCancellationWindowHours), LateCancellationWindowHours, 1, 720);
        AddRangeError(errors, nameof(WaitlistOfferMinutes), WaitlistOfferMinutes, 15, 1440);
        AddRangeError(errors, nameof(CycleDurationDays), CycleDurationDays, 1, 30);

        return errors;
    }

    public SelfServiceConfig ToConfig(Guid spaceId, Guid groupId)
    {
        var errors = Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"SelfServiceDefaults contains invalid values: {string.Join(" ", errors)}");
        }

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

    private static void AddRangeError(List<string> errors, string name, int value, int min, int max)
    {
        if (value < min || value > max)
        {
            errors.Add($"{name} must be between {min} and {max}.");
        }
    }
}
