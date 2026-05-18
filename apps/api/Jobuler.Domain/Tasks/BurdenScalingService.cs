namespace Jobuler.Domain.Tasks;

public static class BurdenScalingService
{
    private const int MinOriginalDurationMinutes = 240;

    /// <summary>
    /// Computes the effective burden level after split-based reduction.
    /// Only applies to tasks whose original duration (shiftDurationMinutes × splitCount) >= 240 minutes.
    /// Formula: max(Easy, originalBurden - (splitCount - 1))
    /// </summary>
    public static TaskBurdenLevel ComputeEffectiveBurden(
        TaskBurdenLevel originalBurden,
        int splitCount,
        int shiftDurationMinutes)
    {
        if (splitCount <= 1) return originalBurden;

        int originalDuration = shiftDurationMinutes * splitCount;
        if (originalDuration < MinOriginalDurationMinutes) return originalBurden;

        int reduced = (int)originalBurden - (splitCount - 1);
        return (TaskBurdenLevel)Math.Max(0, reduced);
    }
}
