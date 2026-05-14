using Jobuler.Application.Common;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.HomeLeave.Commands;

public class PreviewHomeLeaveHandler : IRequestHandler<PreviewHomeLeaveCommand, HomeLeavePreviewResponse>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly ISolverPayloadNormalizer _normalizer;
    private readonly ISolverClient _solverClient;
    private readonly ILogger<PreviewHomeLeaveHandler> _logger;

    /// <summary>
    /// HTTP timeout for the preview solver call.
    /// The solver itself has a 3-second CP-SAT limit; we allow 5 seconds total
    /// to account for network + serialization overhead.
    /// </summary>
    private static readonly TimeSpan PreviewTimeout = TimeSpan.FromSeconds(5);

    public PreviewHomeLeaveHandler(
        AppDbContext db,
        IPermissionService permissions,
        ISolverPayloadNormalizer normalizer,
        ISolverClient solverClient,
        ILogger<PreviewHomeLeaveHandler> logger)
    {
        _db = db;
        _permissions = permissions;
        _normalizer = normalizer;
        _solverClient = solverClient;
        _logger = logger;
    }

    public async Task<HomeLeavePreviewResponse> Handle(PreviewHomeLeaveCommand req, CancellationToken ct)
    {
        // Set PostgreSQL session variables for RLS policies.
        if (_db.Database.IsRelational())
        {
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_space_id', {0}, TRUE), set_config('app.current_user_id', {1}, TRUE)",
                req.SpaceId.ToString(),
                req.RequestingUserId.ToString());
        }

        // 1. Permission check
        await _permissions.RequirePermissionAsync(
            req.RequestingUserId, req.SpaceId, Permissions.ConstraintsManage, ct);

        // 2. Verify group exists and is closed-base
        var group = await _db.Groups.AsNoTracking()
            .Where(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId && g.DeletedAt == null)
            .Select(g => new { g.Id, g.IsClosedBase })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("הקבוצה לא נמצאה.");

        if (!group.IsClosedBase)
            return new HomeLeavePreviewResponse(
                Status: "no_solution", PeopleHomeCount: 0, PeopleAtBaseCount: 0,
                TotalHomeLeaveSlots: 0, CoverageGaps: [], FairnessSpread: 0, SolverTimeMs: 0);

        // 3. Verify home-leave config exists — if not, use defaults
        var hlConfig = await _db.HomeLeaveConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.GroupId == req.GroupId && c.SpaceId == req.SpaceId, ct);

        // 4. Build solver payload with overridden balance_value and preview_mode
        SolverInputDto payload;
        try
        {
            payload = await _normalizer.BuildPreviewAsync(req.SpaceId, req.GroupId, req.BalanceValue, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Preview payload build failed for group {GroupId}", req.GroupId);
            return new HomeLeavePreviewResponse(
                Status: "no_solution", PeopleHomeCount: 0, PeopleAtBaseCount: 0,
                TotalHomeLeaveSlots: 0, CoverageGaps: [], FairnessSpread: 0, SolverTimeMs: 0);
        }

        // 5. Call solver with timeout
        SolverOutputDto solverOutput;
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(PreviewTimeout);

            solverOutput = await _solverClient.SolveAsync(payload, timeoutCts.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException or HttpRequestException)
        {
            _logger.LogWarning(ex, "Preview solver call failed for group {GroupId} with balance_value {BalanceValue}",
                req.GroupId, req.BalanceValue);

            return new HomeLeavePreviewResponse(
                Status: "no_solution",
                PeopleHomeCount: 0,
                PeopleAtBaseCount: 0,
                TotalHomeLeaveSlots: 0,
                CoverageGaps: [],
                FairnessSpread: 0,
                SolverTimeMs: 0);
        }

        // 6. Transform solver output into preview response
        var totalMembers = payload.People.Count;

        // Determine status
        string status;
        if (!solverOutput.Feasible && solverOutput.TimedOut)
            status = "no_solution";
        else if (!solverOutput.Feasible)
            status = "no_solution";
        else if (solverOutput.TimedOut)
            status = "feasible"; // found a solution but couldn't prove optimality
        else
            status = "optimal";

        // Count distinct people with home-leave assignments
        var peopleHomeCount = solverOutput.HomeLeaveAssignments
            .Select(a => a.PersonId)
            .Distinct()
            .Count();

        var peopleAtBaseCount = totalMembers - peopleHomeCount;

        // Total home-leave assignment slots
        var totalHomeLeaveSlots = solverOutput.HomeLeaveAssignments.Count;

        // Calculate coverage gaps: time windows where people on leave > leave_capacity
        var coverageGaps = CalculateCoverageGaps(
            solverOutput.HomeLeaveAssignments,
            totalMembers,
            hlConfig?.LeaveCapacity ?? 1);

        // Calculate fairness spread from home_leave_metrics
        var fairnessSpread = CalculateFairnessSpread(solverOutput.HomeLeaveMetrics);

        // Extract solver_time_ms from the solver output
        var solverTimeMs = solverOutput.SolverTimeMs;

        return new HomeLeavePreviewResponse(
            Status: status,
            PeopleHomeCount: peopleHomeCount,
            PeopleAtBaseCount: peopleAtBaseCount,
            TotalHomeLeaveSlots: totalHomeLeaveSlots,
            CoverageGaps: coverageGaps,
            FairnessSpread: fairnessSpread,
            SolverTimeMs: solverTimeMs);
    }

    /// <summary>
    /// Calculates coverage gaps — time windows where the number of people on leave
    /// exceeds the configured leave_capacity.
    /// A gap occurs when: people_on_leave > leave_capacity
    /// The available_count for a gap = total_members - max_concurrent_on_leave during that window.
    /// </summary>
    private static List<CoverageGapDto> CalculateCoverageGaps(
        List<HomeLeaveAssignmentDto> assignments,
        int totalMembers,
        int leaveCapacity)
    {
        if (assignments.Count == 0)
            return [];

        // Build timeline events: each assignment creates a +1 at start and -1 at end
        var events = new List<(DateTime Time, int Delta)>();
        foreach (var a in assignments)
        {
            if (DateTime.TryParse(a.StartsAt, out var start) && DateTime.TryParse(a.EndsAt, out var end))
            {
                events.Add((start, +1));
                events.Add((end, -1));
            }
        }

        if (events.Count == 0)
            return [];

        // Sort by time; for ties, process departures (-1) before arrivals (+1)
        events.Sort((a, b) =>
        {
            var cmp = a.Time.CompareTo(b.Time);
            return cmp != 0 ? cmp : a.Delta.CompareTo(b.Delta);
        });

        // Sweep through events to find windows where concurrent leaves > capacity
        var gaps = new List<CoverageGapDto>();
        int currentOnLeave = 0;
        DateTime? gapStart = null;
        int maxOnLeaveDuringGap = 0;

        for (int i = 0; i < events.Count; i++)
        {
            currentOnLeave += events[i].Delta;

            // Entered a gap
            if (currentOnLeave > leaveCapacity && gapStart == null)
            {
                gapStart = events[i].Time;
                maxOnLeaveDuringGap = currentOnLeave;
            }
            else if (gapStart != null && currentOnLeave > maxOnLeaveDuringGap)
            {
                maxOnLeaveDuringGap = currentOnLeave;
            }

            // Exited a gap
            if (currentOnLeave <= leaveCapacity && gapStart != null)
            {
                gaps.Add(new CoverageGapDto(
                    StartsAt: gapStart.Value.ToString("o"),
                    EndsAt: events[i].Time.ToString("o"),
                    AvailableCount: totalMembers - maxOnLeaveDuringGap));
                gapStart = null;
                maxOnLeaveDuringGap = 0;
            }
        }

        // If still in a gap at the end of all events
        if (gapStart != null)
        {
            gaps.Add(new CoverageGapDto(
                StartsAt: gapStart.Value.ToString("o"),
                EndsAt: events[^1].Time.ToString("o"),
                AvailableCount: totalMembers - maxOnLeaveDuringGap));
        }

        return gaps;
    }

    /// <summary>
    /// Calculates fairness spread as the difference between the highest and lowest
    /// base_time_ratio across all people in the home-leave metrics.
    /// </summary>
    private static decimal CalculateFairnessSpread(List<HomeLeaveMetricDto> metrics)
    {
        if (metrics.Count < 2)
            return 0;

        var ratios = metrics.Select(m => m.BaseTimeRatio).ToList();
        var spread = ratios.Max() - ratios.Min();
        return (decimal)spread;
    }
}
