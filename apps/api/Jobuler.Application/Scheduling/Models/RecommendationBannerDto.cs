namespace Jobuler.Application.Scheduling.Models;

/// <summary>
/// Aggregated recommendation data for the solver results banner.
/// Shows a summary of uncovered slots and up to 5 recommended tasks.
/// </summary>
public record RecommendationBannerDto(
    int TotalUncoveredSlots,
    List<RecommendationDto> Recommendations,
    int RemainingCount,
    string AffectedDateRange);
