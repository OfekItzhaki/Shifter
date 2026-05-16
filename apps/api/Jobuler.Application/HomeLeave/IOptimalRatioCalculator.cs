namespace Jobuler.Application.HomeLeave;

/// <summary>
/// Computes the optimal base:home day ratio for a group based on member count,
/// minimum people at base, and leave duration.
/// Uses an iterative formula that converges in 2-3 iterations.
/// The solver derives leave_capacity = memberCount - minPeopleAtBase.
/// </summary>
public interface IOptimalRatioCalculator
{
    OptimalRatioResult Calculate(int memberCount, int minPeopleAtBase, decimal leaveDurationHours);
}

/// <summary>
/// Result of the optimal ratio calculation.
/// </summary>
/// <param name="BaseDays">Minimum days at base before becoming eligible for leave.</param>
/// <param name="HomeDays">Days at home per leave cycle (derived from leave duration).</param>
/// <param name="IsReduced">True if the optimal results in less than 1 full day at home (reduced availability).</param>
public record OptimalRatioResult(
    int BaseDays,
    int HomeDays,
    bool IsReduced
);
