namespace Jobuler.Application.HomeLeave;

/// <summary>
/// Evaluates whether a given base:home day configuration can satisfy
/// the group's coverage requirements. Returns feasibility status,
/// maximum feasible home days when applicable, and a localized reason
/// when the configuration is not feasible.
/// </summary>
public interface IFeasibilityEngine
{
    FeasibilityResult Evaluate(int memberCount, int leaveCapacity, int baseDays, int homeDays, int coverageRequirement);
}

/// <summary>
/// Result of a feasibility evaluation.
/// </summary>
/// <param name="IsFeasible">True if the configuration satisfies coverage requirements.</param>
/// <param name="MaxFeasibleHomeDays">When feasible, the maximum home days that would still be feasible. Null when not feasible.</param>
/// <param name="Reason">Localized explanation when not feasible. Null when feasible.</param>
public record FeasibilityResult(
    bool IsFeasible,
    int? MaxFeasibleHomeDays,
    string? Reason
);
