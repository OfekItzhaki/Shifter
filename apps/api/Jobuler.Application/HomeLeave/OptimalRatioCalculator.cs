namespace Jobuler.Application.HomeLeave;

/// <summary>
/// Computes the optimal base:home day ratio using an iterative formula.
/// The formula converges in 2-3 iterations, ensuring sub-500ms performance
/// for groups of up to 50 members.
/// </summary>
public class OptimalRatioCalculator : IOptimalRatioCalculator
{
    private const int MaxIterations = 20;

    public OptimalRatioResult Calculate(int memberCount, int leaveCapacity, decimal leaveDurationHours, int coverageRequirement)
    {
        if (memberCount < 2)
            throw new InvalidOperationException("Group must have at least 2 members for home-leave.");

        if (leaveCapacity < 1)
            throw new InvalidOperationException("Leave capacity must be at least 1.");

        if (coverageRequirement < 1)
            throw new InvalidOperationException("Coverage requirement must be at least 1.");

        if (leaveDurationHours < 12 || leaveDurationHours > 168)
            throw new InvalidOperationException("Leave duration must be between 12 and 168 hours.");

        var availableForRotation = memberCount - leaveCapacity;
        if (availableForRotation < coverageRequirement)
            throw new InvalidOperationException(
                "Not enough members to satisfy coverage requirement with the given leave capacity.");

        // Step 1: home_days = ceil(leave_duration_hours / 24)
        var homeDays = (int)Math.Ceiling((double)leaveDurationHours / 24.0);

        // Step 2: Iteratively compute base_days until convergence
        // Start with an initial guess: base_days = home_days (1:1 ratio)
        var baseDays = homeDays;

        for (var i = 0; i < MaxIterations; i++)
        {
            var cycleLength = baseDays + homeDays;
            var newBaseDays = (int)Math.Ceiling(
                (double)(coverageRequirement * cycleLength) / availableForRotation);

            if (newBaseDays == baseDays)
                break;

            baseDays = newBaseDays;
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
