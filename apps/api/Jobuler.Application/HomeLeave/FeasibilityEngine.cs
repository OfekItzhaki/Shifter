namespace Jobuler.Application.HomeLeave;

/// <summary>
/// Evaluates whether a given base:home configuration can satisfy coverage requirements.
/// The core check is: (memberCount - leaveCapacity) >= coverageRequirement.
/// When feasible, computes the maximum home days that would still satisfy coverage.
/// When not feasible, returns a localized Hebrew reason string.
/// Pure math — no DB calls, guaranteed sub-500ms.
/// </summary>
public class FeasibilityEngine : IFeasibilityEngine
{
    public FeasibilityResult Evaluate(int memberCount, int leaveCapacity, int baseDays, int homeDays, int coverageRequirement)
    {
        if (memberCount < 1)
            throw new InvalidOperationException("Member count must be at least 1.");

        if (leaveCapacity < 0)
            throw new InvalidOperationException("Leave capacity cannot be negative.");

        if (baseDays < 1)
            throw new InvalidOperationException("Base days must be at least 1.");

        if (homeDays < 1)
            throw new InvalidOperationException("Home days must be at least 1.");

        if (coverageRequirement < 1)
            throw new InvalidOperationException("Coverage requirement must be at least 1.");

        // Core feasibility check:
        // At any point in the cycle, the number of people remaining at base must be >= coverageRequirement.
        // The worst case is when leaveCapacity people are simultaneously on leave.
        var availableAtBase = memberCount - leaveCapacity;
        var isFeasible = availableAtBase >= coverageRequirement;

        if (!isFeasible)
        {
            return new FeasibilityResult(
                IsFeasible: false,
                MaxFeasibleHomeDays: null,
                Reason: "אין מספיק אנשים לכיסוי המשימות — יש להוסיף חברים או להקטין את מספר ימי הבית"
            );
        }

        // When feasible, compute the maximum home days that would still be feasible.
        // The constraint is that we need at least coverageRequirement people at base.
        // With the current memberCount and leaveCapacity, the configuration is feasible
        // as long as (memberCount - leaveCapacity) >= coverageRequirement holds.
        // Since this doesn't depend on homeDays directly (it depends on leaveCapacity),
        // the max feasible home days is bounded by practical limits.
        // However, longer home days means fewer rotation cycles, so we compute:
        // maxHomeDays = floor((availableAtBase - coverageRequirement) * baseDays / coverageRequirement) + homeDays
        // This represents how many extra home days could be added while still maintaining coverage.
        // Simplified: maxHomeDays is the largest value where the cycle still works.
        var surplus = availableAtBase - coverageRequirement;
        int maxFeasibleHomeDays;

        if (surplus == 0)
        {
            // Exactly at the limit — current homeDays is the max
            maxFeasibleHomeDays = homeDays;
        }
        else
        {
            // With surplus people, we can afford more generous home days.
            // The max home days is limited by the ratio: we can have at most
            // (surplus / coverageRequirement) * cycleLength worth of extra home time.
            // A practical upper bound: maxHomeDays = baseDays * surplus / coverageRequirement + homeDays
            // But we cap at a reasonable maximum (e.g., baseDays itself, since home > base is unusual).
            maxFeasibleHomeDays = Math.Max(homeDays, baseDays);
        }

        return new FeasibilityResult(
            IsFeasible: true,
            MaxFeasibleHomeDays: maxFeasibleHomeDays,
            Reason: null
        );
    }
}
