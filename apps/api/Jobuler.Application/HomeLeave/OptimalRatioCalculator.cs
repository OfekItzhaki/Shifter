namespace Jobuler.Application.HomeLeave;

/// <summary>
/// Computes the optimal base:home day ratio using an iterative formula.
/// The formula converges in 2-3 iterations, ensuring sub-500ms performance
/// for groups of up to 50 members.
/// </summary>
public class OptimalRatioCalculator : IOptimalRatioCalculator
{
    private const int MaxIterations = 20;

    public OptimalRatioResult Calculate(int memberCount, int minPeopleAtBase, decimal leaveDurationHours)
    {
        if (memberCount < 2)
            throw new InvalidOperationException("Group must have at least 2 members for home-leave.");

        if (minPeopleAtBase < 1)
            throw new InvalidOperationException("Minimum people at base must be at least 1.");

        if (minPeopleAtBase >= memberCount)
            throw new InvalidOperationException("Minimum people at base must be less than the member count.");

        if (leaveDurationHours < 12 || leaveDurationHours > 168)
            throw new InvalidOperationException("Leave duration must be between 12 and 168 hours.");

        // Derive leave_capacity and coverage_requirement from minPeopleAtBase
        var leaveCapacity = memberCount - minPeopleAtBase;
        var coverageRequirement = minPeopleAtBase;

        if (leaveCapacity < 1)
            throw new InvalidOperationException("Leave capacity must be at least 1.");

        var availableForRotation = memberCount - leaveCapacity;
        if (availableForRotation < coverageRequirement)
            throw new InvalidOperationException(
                "Not enough members to satisfy coverage requirement with the given leave capacity.");

        // Step 1: home_days = ceil(leave_duration_hours / 24)
        var homeDays = (int)Math.Ceiling((double)leaveDurationHours / 24.0);

        // Step 2: Compute base_days
        // When availableForRotation == coverageRequirement, the iterative formula diverges.
        // Use direct formula: base_days = ceil(homeDays × coverageRequirement / leaveCapacity)
        // This represents the minimum time each person must wait at base between leave rotations.
        int baseDays;
        if (availableForRotation == coverageRequirement)
        {
            // Edge case: exactly enough people to cover. Each person's base time is
            // proportional to how many others need to rotate through home leave.
            baseDays = Math.Max(1, (int)Math.Ceiling((double)(homeDays * coverageRequirement) / leaveCapacity));
        }
        else
        {
            // General case: iteratively compute base_days until convergence
            baseDays = homeDays;
            for (var i = 0; i < MaxIterations; i++)
            {
                var cycleLength = baseDays + homeDays;
                var newBaseDays = (int)Math.Ceiling(
                    (double)(coverageRequirement * cycleLength) / availableForRotation);

                if (newBaseDays == baseDays)
                    break;

                baseDays = newBaseDays;
            }
        }

        // Ensure minimum of 1 day for both
        baseDays = Math.Max(1, baseDays);
        homeDays = Math.Max(1, homeDays);

        // IsReduced: true if the raw calculation would result in < 1 day home
        // This happens when leaveDurationHours < 24
        var isReduced = leaveDurationHours < 24;

        return new OptimalRatioResult(baseDays, homeDays, isReduced);
    }
}
