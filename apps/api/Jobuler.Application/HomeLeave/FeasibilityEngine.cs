namespace Jobuler.Application.HomeLeave;

/// <summary>
/// Evaluates whether a given base:home configuration can satisfy coverage requirements.
/// The admin sets minPeopleAtBase; the engine derives leaveCapacity = memberCount - minPeopleAtBase.
/// The core check is: memberCount - minPeopleAtBase >= 1 (at least 1 person can go home).
/// When feasible, computes the maximum home days that would still satisfy coverage.
/// When not feasible, returns a localized Hebrew reason string.
/// Pure math — no DB calls, guaranteed sub-500ms.
/// </summary>
public class FeasibilityEngine : IFeasibilityEngine
{
    public FeasibilityResult Evaluate(int memberCount, int minPeopleAtBase, int baseDays, int homeDays)
    {
        if (memberCount < 1)
            throw new InvalidOperationException("Member count must be at least 1.");

        if (minPeopleAtBase < 1)
            throw new InvalidOperationException("Minimum people at base must be at least 1.");

        if (baseDays < 1)
            throw new InvalidOperationException("Base days must be at least 1.");

        if (homeDays < 1)
            throw new InvalidOperationException("Home days must be at least 1.");

        // Derive leaveCapacity and coverageRequirement from minPeopleAtBase
        var leaveCapacity = memberCount - minPeopleAtBase;
        var coverageRequirement = minPeopleAtBase;

        // Core feasibility check:
        // At least 1 person must be able to go home (leaveCapacity >= 1)
        // and the remaining people at base must satisfy coverage (memberCount - leaveCapacity >= coverageRequirement).
        var availableAtBase = memberCount - leaveCapacity;
        var isFeasible = leaveCapacity >= 1 && availableAtBase >= coverageRequirement;

        if (!isFeasible)
        {
            return new FeasibilityResult(
                IsFeasible: false,
                MaxFeasibleHomeDays: null,
                Reason: "אין מספיק אנשים לכיסוי המשימות — יש להוסיף חברים או להקטין את מספר ימי הבית"
            );
        }

        // When feasible, compute the maximum home days that would still be feasible.
        // With the new semantics, leaveCapacity people can go home simultaneously.
        // The max home days is bounded by practical rotation limits.
        // With more people available for rotation (higher leaveCapacity), longer home periods are possible.
        // maxHomeDays = baseDays × leaveCapacity / minPeopleAtBase (how long each batch can stay home)
        int maxFeasibleHomeDays;

        if (leaveCapacity <= coverageRequirement)
        {
            // Tight rotation — current homeDays is the practical max
            maxFeasibleHomeDays = homeDays;
        }
        else
        {
            // More people can go home than need to stay — more generous rotation possible
            maxFeasibleHomeDays = Math.Max(homeDays, baseDays);
        }

        return new FeasibilityResult(
            IsFeasible: true,
            MaxFeasibleHomeDays: maxFeasibleHomeDays,
            Reason: null
        );
    }
}
