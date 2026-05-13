using Jobuler.Application.Scheduling.Models;

namespace Jobuler.Application.Scheduling;

/// <summary>
/// Reads all operational data for a space (optionally scoped to a single group)
/// and builds the normalized SolverInputDto that gets sent to the Python solver service.
/// When groupId is provided, only that group's members and tasks are included.
/// When startTime is provided, it overrides the default "now" as the horizon start.
/// </summary>
public interface ISolverPayloadNormalizer
{
    Task<SolverInputDto> BuildAsync(
        Guid spaceId,
        Guid runId,
        string triggerMode,
        Guid? baselineVersionId,
        Guid? groupId = null,
        DateTime? startTime = null,
        CancellationToken ct = default);

    /// <summary>
    /// Builds a solver payload identical to a normal run but overrides the
    /// <see cref="HomeLeaveConfigDto.BalanceValue"/> with the provided value
    /// and sets <see cref="SolverInputDto.PreviewMode"/> to true.
    /// Used for lightweight preview solver runs.
    /// </summary>
    Task<SolverInputDto> BuildPreviewAsync(
        Guid spaceId,
        Guid groupId,
        int balanceValue,
        CancellationToken ct);
}
