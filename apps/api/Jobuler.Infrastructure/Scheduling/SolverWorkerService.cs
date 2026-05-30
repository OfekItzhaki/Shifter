using Jobuler.Application.Notifications;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Logging;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Jobuler.Infrastructure.Scheduling;

/// <summary>
/// Background worker that dequeues solver jobs from Redis,
/// calls the Python solver, and stores the resulting draft schedule version.
/// Runs as a hosted service alongside the API process.
/// </summary>
public class SolverWorkerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SolverWorkerService> _logger;

    public SolverWorkerService(IServiceScopeFactory scopeFactory, ILogger<SolverWorkerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Solver worker started.");

        // On startup: clean up any runs that were left in 'running' or 'queued' state
        // from a previous API process that died mid-execution. Their associated draft
        // versions (if any) are discarded so the UI never shows stale empty drafts.
        await CleanupOrphanedRunsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextJobAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Unhandled error in solver worker loop.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task CleanupOrphanedRunsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Find all runs stuck in running/queued — these were interrupted by an API restart
            var orphanedRuns = await db.ScheduleRuns
                .Where(r => r.Status == ScheduleRunStatus.Running || r.Status == ScheduleRunStatus.Queued)
                .ToListAsync(ct);

            if (orphanedRuns.Count == 0) return;

            _logger.LogWarning("Found {Count} orphaned solver run(s) from previous process — cleaning up.", orphanedRuns.Count);

            foreach (var run in orphanedRuns)
            {
                // Discard any draft version that was created for this run
                var orphanedDrafts = await db.ScheduleVersions
                    .Where(v => v.SourceRunId == run.Id && v.Status == ScheduleVersionStatus.Draft)
                    .ToListAsync(ct);

                foreach (var draft in orphanedDrafts)
                    draft.Discard();

                run.MarkFailed("Orphaned - API restarted while run was in progress.");
            }

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Orphaned run cleanup complete. Cleaned {Count} run(s).", orphanedRuns.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during orphaned run cleanup — continuing startup.");
        }
    }

    private async Task ProcessNextJobAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var queue      = scope.ServiceProvider.GetRequiredService<ISolverJobQueue>();
        var normalizer = scope.ServiceProvider.GetRequiredService<ISolverPayloadNormalizer>();
        var client     = scope.ServiceProvider.GetRequiredService<ISolverClient>();
        var db         = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mediator   = scope.ServiceProvider.GetRequiredService<IMediator>();
        var sysLog     = scope.ServiceProvider.GetRequiredService<ISystemLogger>();
        var notifier   = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var job = await queue.DequeueAsync(ct);
        if (job is null)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
            return;
        }

        _logger.LogInformation("Processing solver job: run_id={RunId}", job.RunId);

        // Set PostgreSQL session variables BEFORE any DB query so RLS policies
        // can evaluate app.current_space_id. Without this, the first query
        // (loading the run record) is blocked by RLS returning zero rows.
        await db.Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.current_space_id', {0}, TRUE), set_config('app.current_user_id', {1}, TRUE)",
            job.SpaceId.ToString(),
            job.RequestedByUserId?.ToString() ?? "");

        // Load the run record — must exist (created by the trigger command)
        var run = await db.ScheduleRuns
            .FirstOrDefaultAsync(r => r.Id == job.RunId && r.SpaceId == job.SpaceId, ct);

        if (run is null)
        {
            _logger.LogWarning("Solver job references unknown run_id={RunId}. Skipping.", job.RunId);
            return;
        }

        // Idempotency: skip if already processed
        if (run.Status is ScheduleRunStatus.Completed or ScheduleRunStatus.TimedOut or ScheduleRunStatus.Failed)
        {
            _logger.LogInformation("Run {RunId} already processed (status={Status}). Skipping.", job.RunId, run.Status);
            return;
        }

        SolverInputDto? input = null;
        try
        {
            // ── Phase: building_payload ───────────────────────────────────────
            run.SetProgressPhase("building_payload");
            await db.SaveChangesAsync(ct);

            // Build solver input
            input = await normalizer.BuildAsync(
                job.SpaceId, job.RunId, job.TriggerMode, job.BaselineVersionId, job.GroupId, job.StartTime, ct);

            // ── Pre-flight checks ─────────────────────────────────────────────
            if (input.TaskSlots.Count == 0)
            {
                var (noTasksTitle, noTasksBody) = input.Locale switch {
                    "en" => ("No tasks to schedule", "No future tasks found in the current time window. Create tasks with future dates and try again."),
                    "ru" => ("Нет задач для планирования", "Не найдено будущих задач в текущем временном окне. Создайте задачи с будущими датами и повторите попытку."),
                    _    => ("No tasks to schedule", "No future tasks found in the current time window. Create tasks with future dates and try again.")
                };
                run.MarkFailed(noTasksBody);
                await db.SaveChangesAsync(ct);
                await notifier.NotifySpaceAdminsAsync(
                    job.SpaceId, "solver_no_tasks", noTasksTitle, noTasksBody, groupId: job.GroupId, ct: ct);
                return;
            }

            if (input.People.Count == 0)
            {
                var (noPeopleTitle, noPeopleBody) = input.Locale switch {
                    "en" => ("No active members", "No active members found in the group. Add members and try again."),
                    "ru" => ("Нет активных участников", "В группе не найдено активных участников. Добавьте участников и повторите попытку."),
                    _    => ("No active members", "No active members found in the group. Add members and try again.")
                };
                run.MarkFailed(noPeopleBody);
                await db.SaveChangesAsync(ct);
                await notifier.NotifySpaceAdminsAsync(
                    job.SpaceId, "solver_no_people", noPeopleTitle, noPeopleBody, groupId: job.GroupId, ct: ct);
                return;
            }

            // Warn if very few people relative to tasks
            var totalHeadcountNeeded = input.TaskSlots.Sum(s => s.RequiredHeadcount);
            if (input.People.Count < totalHeadcountNeeded / (double)input.TaskSlots.Count)
            {
                _logger.LogWarning(
                    "Solver: {People} people for {Slots} slots needing avg {Avg} headcount — may be infeasible.",
                    input.People.Count, input.TaskSlots.Count,
                    totalHeadcountNeeded / (double)input.TaskSlots.Count);
            }

            // ── Pre-flight: capacity check ────────────────────────────────────
            // Derive min_rest_hours from constraints (hard takes priority over soft, default 0).
            var minRestHours = 0.0;
            var hardRestRule = input.HardConstraints.FirstOrDefault(c => c.RuleType == "min_rest_hours");
            var softRestRule = input.SoftConstraints.FirstOrDefault(c => c.RuleType == "min_rest_hours");
            if (hardRestRule is not null && hardRestRule.Payload.TryGetValue("hours", out var hardHoursVal))
                minRestHours = ToDouble(hardHoursVal);
            else if (softRestRule is not null && softRestRule.Payload.TryGetValue("hours", out var softHoursVal))
                minRestHours = ToDouble(softHoursVal);

            // For each distinct task type, calculate the minimum people needed to cover it 24/7.
            //
            // Key factors per task type (all derived from actual slot/task data):
            //   shiftHours         = slot duration in hours
            //   allowsDoubleShift  = person can do 2 consecutive shifts without rest
            //   allowsOverlap      = person assigned here can also count toward other tasks
            //   requiredHeadcount  = people needed per shift
            //
            // If allowsDoubleShift:
            //   effectiveShiftBlock = shiftHours * 2  (two shifts back-to-back before rest)
            // Else:
            //   effectiveShiftBlock = shiftHours
            //
            // maxShiftsPerPerson = floor(24 / (effectiveShiftBlock + minRestHours))
            //                      × (2 if doubleShift, else 1)   [shifts, not blocks]
            //
            // minPeopleForTask = ceil(shiftsPerDay / maxShiftsPerPerson) × requiredHeadcount
            //
            // If allowsOverlap: this task's people requirement is satisfied by the shared pool,
            // so we don't add it to the total — we only track the max across overlapping tasks.

            var taskTypeGroups = input.TaskSlots
                .GroupBy(s => s.TaskTypeId)
                .Select(g =>
                {
                    var sample = g.First();
                    var shiftHours = 0.0;
                    if (DateTime.TryParse(sample.StartsAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var slotStart) &&
                        DateTime.TryParse(sample.EndsAt,   null, System.Globalization.DateTimeStyles.RoundtripKind, out var slotEnd))
                        shiftHours = (slotEnd - slotStart).TotalHours;

                    if (shiftHours <= 0) return (
                        TaskTypeName: sample.TaskTypeName,
                        MinPeople: sample.RequiredHeadcount,
                        AllowsOverlap: sample.AllowsOverlap);

                    // Double-shift: read directly from the slot (AllowsDoubleShift passed from GroupTask)
                    var allowsDoubleShift = sample.AllowsDoubleShift;

                    var shiftsPerDay = 24.0 / shiftHours;
                    // Effective block = how many hours a person is "occupied" per work cycle
                    var effectiveBlock = allowsDoubleShift
                        ? (shiftHours * 2) + minRestHours   // 2 shifts then rest
                        : shiftHours + minRestHours;         // 1 shift then rest
                    var shiftsPerCycle = allowsDoubleShift ? 2.0 : 1.0;
                    var cyclesPerDay   = effectiveBlock > 0 ? Math.Floor(24.0 / effectiveBlock) : 1.0;
                    var maxShiftsPerPerson = Math.Max(1.0, cyclesPerDay * shiftsPerCycle);
                    var minPeople = (int)Math.Ceiling(shiftsPerDay / maxShiftsPerPerson) * sample.RequiredHeadcount;

                    return (
                        TaskTypeName: sample.TaskTypeName,
                        MinPeople: minPeople,
                        AllowsOverlap: sample.AllowsOverlap);
                })
                .ToList();

            // If ALL tasks allow overlap, people are fully shared — use the max single-task requirement.
            // If SOME tasks allow overlap, those share the pool with non-overlap tasks.
            // Conservative approach: sum non-overlap tasks + max of overlap tasks.
            var nonOverlapMin = taskTypeGroups.Where(t => !t.AllowsOverlap).Sum(t => t.MinPeople);
            var overlapMax    = taskTypeGroups.Where(t => t.AllowsOverlap).Select(t => t.MinPeople).DefaultIfEmpty(0).Max();
            var totalMinPeople = nonOverlapMin + overlapMax;

            if (input.People.Count < totalMinPeople)
            {
                var preflightLocale = input.Locale ?? "en";
                var taskSummary = string.Join(", ", taskTypeGroups.Select(t =>
                    $"{t.TaskTypeName} ({t.MinPeople}{(t.AllowsOverlap ? ", shared" : "")})"));
                var reason = preflightLocale switch {
                    "he" => $"נדרשים לפחות {totalMinPeople} חברים לכיסוי כל המשימות 24/7 ({taskSummary}), אך רק {input.People.Count} חברים פעילים בקבוצות. הוסף חברים ונסה שוב.",
                    "ru" => $"Для покрытия всех задач 24/7 ({taskSummary}) требуется минимум {totalMinPeople} участников, но активных только {input.People.Count}. Добавьте участников и повторите попытку.",
                    _    => $"At least {totalMinPeople} members are needed to cover all tasks 24/7 ({taskSummary}), but only {input.People.Count} active members are in groups. Add more members and try again."
                };
                var preflightTitle = preflightLocale switch {
                    "he" => "לא ניתן לסדר - אין מספיק חברים",
                    "ru" => "Невозможно составить расписание - недостаточно участников",
                    _    => "Cannot schedule - not enough members"
                };
                run.MarkFailed(reason);
                await db.SaveChangesAsync(ct);
                await notifier.NotifySpaceAdminsAsync(
                    job.SpaceId, "solver_preflight_failed", preflightTitle, reason, groupId: job.GroupId, ct: ct);
                return;
            }

            var inputHash = ComputeHash(input);
            run.MarkRunning(inputHash);
            await db.SaveChangesAsync(ct);

            // Call solver
            var output = await client.SolveAsync(input, ct);
            _logger.LogInformation(
                "Solver output: run_id={RunId} feasible={Feasible} timedOut={TimedOut} assignments={Assignments} uncovered={Uncovered}",
                job.RunId, output.Feasible, output.TimedOut, output.Assignments.Count, output.UncoveredSlotIds.Count);

            // ── Phase: storing_results ────────────────────────────────────────
            run.SetProgressPhase("storing_results");
            await db.SaveChangesAsync(ct);

            // Build summary JSON for logging/notifications regardless of outcome
            var summaryJson = JsonSerializer.Serialize(new
            {
                feasible = output.Feasible,
                timed_out = output.TimedOut,
                stability = output.StabilityMetrics,
                explanation = output.ExplanationFragments,
                uncovered_slots = output.UncoveredSlotIds.Count,
                hard_conflicts = output.HardConflicts.Count,
                conflict_details = output.HardConflicts.Select(c => new
                {
                    rule_type = c.RuleType,
                    description = c.Description,
                    affected_slots = c.AffectedSlotIds.Count
                }).ToList(),
                home_leave_assignments = output.HomeLeaveAssignments.Select(a => new
                {
                    person_id = a.PersonId,
                    starts_at = a.StartsAt,
                    ends_at = a.EndsAt
                }).ToList(),
                home_leave_metrics = output.HomeLeaveMetrics.Select(m => new
                {
                    person_id = m.PersonId,
                    total_base_hours = m.TotalBaseHours,
                    total_home_hours = m.TotalHomeHours,
                    base_time_ratio = m.BaseTimeRatio,
                    leave_slot_count = m.LeaveSlotCount
                }).ToList(),
                fairness_variance = output.FairnessVariance
            });

            // Count parseable assignments to decide whether to create a version
            var parsedAssignmentDtos = output.Feasible
                ? output.Assignments.Where(a => Guid.TryParse(a.SlotId, out _)).ToList()
                : new List<AssignmentResultDto>();

            foreach (var a in output.Assignments.Where(a => !Guid.TryParse(a.SlotId, out _)))
                _logger.LogWarning("Cannot parse slot_id as GUID: '{SlotId}' — skipping assignment for person {PersonId}", a.SlotId, a.PersonId);

            _logger.LogInformation(
                "Solver output: feasible={Feasible} timedOut={TimedOut} rawAssignments={Raw} parsedAssignments={Parsed}",
                output.Feasible, output.TimedOut, output.Assignments.Count, parsedAssignmentDtos.Count);

            // ── Post-solve hard constraint validation ─────────────────────────
            // Even when the solver reports feasible, validate assignments against
            // input constraints. CP-SAT guarantees feasibility only for constraints
            // actually added to the model — if a constraint was missed, the result
            // may violate business rules. Catch those here before creating a draft.
            var postSolveViolations = new List<string>();
            if (output.Feasible && parsedAssignmentDtos.Count > 0)
            {
                postSolveViolations = ValidateHardConstraints(input, parsedAssignmentDtos);
                if (postSolveViolations.Count > 0)
                {
                    _logger.LogWarning(
                        "Post-solve validation found {Count} hard constraint violation(s) for run {RunId}.",
                        postSolveViolations.Count, job.RunId);
                }
            }

            // Do NOT persist a version if there's nothing useful to show the admin:
            // - solver proved infeasible, OR
            // - feasible but zero assignments (solver bug / all slots unparseable)
            // - feasible but has uncovered slots (all tasks MUST be fully assigned)
            // - post-solve validation detected hard constraint violations
            // In all these cases, mark the run failed and notify the admin with
            // actionable guidance (e.g. reduce planning horizon, add members).
            var hasUncoveredSlots = output.UncoveredSlotIds.Count > 0;
            var hasPostSolveViolations = postSolveViolations.Count > 0;
            var shouldDiscard = !output.Feasible || parsedAssignmentDtos.Count == 0 || hasUncoveredSlots || hasPostSolveViolations;

            // Only create the ScheduleVersion AFTER confirming the solver succeeded
            // with valid assignments. This prevents empty drafts from lingering in the DB.
            ScheduleVersion? version = null;
            List<Assignment> assignments;
            int nextVersion = 0;

            if (!shouldDiscard)
            {
                // Determine next version number for this space
                nextVersion = (await db.ScheduleVersions
                    .Where(v => v.SpaceId == job.SpaceId)
                    .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0) + 1;

                // Regeneration runs use a dedicated factory that sets SupersedesVersionId
                // and SourceType="regeneration" for audit trail and UI context.
                var isRegeneration = job.TriggerMode == "regeneration";
                if (isRegeneration && job.BaselineVersionId.HasValue && job.RequestedByUserId.HasValue)
                {
                    version = ScheduleVersion.CreateRegenerationDraft(
                        job.SpaceId, nextVersion, job.RunId,
                        job.BaselineVersionId.Value, job.RequestedByUserId.Value, summaryJson);
                }
                else
                {
                    version = ScheduleVersion.CreateDraft(
                        job.SpaceId, nextVersion, job.BaselineVersionId,
                        job.RunId, job.RequestedByUserId, summaryJson);
                }

                db.ScheduleVersions.Add(version);
                await db.SaveChangesAsync(ct); // get version.Id

                assignments = parsedAssignmentDtos.Select(a =>
                    Assignment.Create(
                        job.SpaceId, version.Id,
                        Guid.Parse(a.SlotId), Guid.Parse(a.PersonId),
                        a.Source == "override" ? AssignmentSource.Override : AssignmentSource.Solver)
                ).ToList();

                db.Assignments.AddRange(assignments);

                var baseline = job.BaselineVersionId.HasValue
                    ? await db.Assignments.AsNoTracking()
                        .Where(a => a.ScheduleVersionId == job.BaselineVersionId.Value)
                        .Select(a => new { a.TaskSlotId, a.PersonId })
                        .ToListAsync(ct)
                    : new();

                var newSet      = assignments.Select(a => (a.TaskSlotId, a.PersonId)).ToHashSet();
                var baselineSet = baseline.Select(a => (a.TaskSlotId, a.PersonId)).ToHashSet();

                var added   = newSet.Except(baselineSet).Count();
                var removed = baselineSet.Except(newSet).Count();
                var changed = Math.Min(added, removed); // approximation

                var diffSummary = AssignmentChangeSummary.Create(
                    job.SpaceId, version.Id, job.BaselineVersionId,
                    added, removed, changed,
                    (decimal?)output.StabilityMetrics.TotalStabilityPenalty,
                    JsonSerializer.Serialize(output.StabilityMetrics));

                db.AssignmentChangeSummaries.Add(diffSummary);

                // Link the result version to the run for status polling
                run.SetResultVersion(version.Id);

                // Mark run completed
                if (output.TimedOut)
                    run.MarkTimedOut(summaryJson);
                else
                    run.MarkCompleted(summaryJson);

                await db.SaveChangesAsync(ct);

                // Update fairness counters in a separate scope so its DbContext
                // instance doesn't conflict with the worker's ongoing context.
                using var fairnessScope = _scopeFactory.CreateScope();
                var fairnessMediator = fairnessScope.ServiceProvider.GetRequiredService<IMediator>();
                await fairnessMediator.Send(new UpdateFairnessCountersCommand(job.SpaceId, version.Id), ct);

                // Compute task rotation progress for army-template groups
                if (job.GroupId.HasValue)
                {
                    await fairnessMediator.Send(new ComputeTaskRotationCommand(job.SpaceId, job.GroupId.Value), ct);
                }
            }
            else
            {
                assignments = new List<Assignment>();

                // No version created — just update the run status
                _logger.LogWarning(
                    "Skipping version creation: feasible={Feasible} assignments={Count} uncovered={Uncovered} postSolveViolations={Violations}",
                    output.Feasible, parsedAssignmentDtos.Count, output.UncoveredSlotIds.Count, postSolveViolations.Count);

                if (output.TimedOut)
                    run.MarkTimedOut(summaryJson);
                else if (!output.Feasible)
                    run.MarkFailed($"Solver returned infeasible. Hard conflicts: {output.HardConflicts.Count}");
                else if (hasPostSolveViolations)
                {
                    // Post-solve validation detected hard constraint violations in a "feasible" result.
                    // This means the solver's model was missing constraints — treat as failed.
                    var violationLocale = input.Locale ?? "en";
                    var violationList = string.Join("\n", postSolveViolations.Select(v => $"• {v}"));
                    var violationError = violationLocale switch {
                        "he" => $"מנוע הסידור החזיר תוצאה שמפרה אילוצים קשיחים ({postSolveViolations.Count} הפרות). לא נוצר טיוטה.\n\n{violationList}\n\nבדוק את הגדרות האילוצים ונסה שוב.",
                        "ru" => $"Решатель вернул результат с нарушениями жёстких ограничений ({postSolveViolations.Count} нарушений). Черновик не создан.\n\n{violationList}\n\nПроверьте настройки ограничений и повторите попытку.",
                        _    => $"Solver returned a result that violates hard constraints ({postSolveViolations.Count} violation(s)). No draft was created.\n\n{violationList}\n\nReview constraint settings and try again."
                    };
                    run.MarkFailed(violationError);
                }
                else if (hasUncoveredSlots)
                {
                    // Identify which tasks have uncovered slots and suggest fixes
                    var uncoveredSlotSet = output.UncoveredSlotIds.ToHashSet();
                    var uncoveredTaskNames = input.TaskSlots
                        .Where(s => uncoveredSlotSet.Contains(s.SlotId))
                        .Select(s => s.TaskTypeName)
                        .Distinct()
                        .ToList();
                    var taskList = string.Join(", ", uncoveredTaskNames);

                    // Build a locale-aware error with actionable guidance
                    var uncoveredLocale = input.Locale ?? "en";
                    var uncoveredError = uncoveredLocale switch {
                        "he" => $"לא ניתן לאייש את כל המשמרות: {taskList} ({output.UncoveredSlotIds.Count} משמרות לא מאוישות). נסה לצמצם את אופק התכנון, להוסיף חברים, או להקל אילוצים.",
                        "ru" => $"Не удалось укомплектовать все смены: {taskList} ({output.UncoveredSlotIds.Count} смен не заполнены). Попробуйте уменьшить горизонт планирования, добавить участников или смягчить ограничения.",
                        _    => $"Could not staff all shifts: {taskList} ({output.UncoveredSlotIds.Count} shifts unfilled). Try reducing the planning horizon, adding members, or relaxing constraints."
                    };
                    run.MarkFailed(uncoveredError);
                }
                else
                    run.MarkFailed("Solver returned zero assignments despite reporting feasible.");

                await db.SaveChangesAsync(ct);
            }

            // ── Recommendation engine (best-effort, never disrupts solver flow) ──
            // The engine analyzes solver output for staffing shortfalls and produces
            // double-shift recommendations. It runs after the run status is persisted
            // and before the main notification dispatch. Failures are logged but never
            // disrupt the solver flow.
            if (input is not null && output.Feasible)
            {
                try
                {
                    var recommendationEngine = scope.ServiceProvider.GetRequiredService<IRecommendationEngine>();
                    var recommendationResult = await recommendationEngine.AnalyzeAsync(
                        job.SpaceId, job.GroupId ?? Guid.Empty, job.RunId, input, output, ct);

                    if (recommendationResult.Recommendations.Count > 0)
                    {
                        var totalUncoveredSlots = output.UncoveredSlotIds.Count;
                        var recommendationMetadata = JsonSerializer.Serialize(new
                        {
                            run_id = job.RunId,
                            total_uncovered_slots = totalUncoveredSlots
                        });

                        var recLocale = input.Locale ?? "en";
                        await notifier.NotifySpaceAdminsAsync(
                            job.SpaceId,
                            "double_shift_recommendation",
                            recLocale switch
                            {
                                "he" => "המלצה: הפעל משמרת כפולה",
                                "ru" => "Рекомендация: включить двойную смену",
                                _ => "Recommendation: enable double shift"
                            },
                            recLocale switch
                            {
                                "he" => $"זוהו {totalUncoveredSlots} משבצות לא מכוסות. הפעלת משמרת כפולה ב-{recommendationResult.Recommendations.Count} משימות עשויה לשפר את הכיסוי.",
                                "ru" => $"Обнаружено {totalUncoveredSlots} незаполненных смен. Включение двойной смены для {recommendationResult.Recommendations.Count} задач может улучшить покрытие.",
                                _ => $"{totalUncoveredSlots} uncovered slot(s) detected. Enabling double shift on {recommendationResult.Recommendations.Count} task(s) may improve coverage."
                            },
                            metadataJson: recommendationMetadata,
                            groupId: job.GroupId,
                            ct: ct);
                    }
                }
                catch (Exception recEx)
                {
                    _logger.LogWarning(recEx,
                        "Recommendation engine failed for run {RunId} — continuing without recommendations.", job.RunId);
                }
            }

            // System log — solver completed
            var sev = output.TimedOut ? "warning" : (output.Feasible && !shouldDiscard ? "info" : "error");
            var evt = output.Feasible && !shouldDiscard ? "solver_completed" : "solver_infeasible";
            await sysLog.LogAsync(job.SpaceId, sev, evt,
                $"Solver run {job.RunId} finished. Feasible={output.Feasible} TimedOut={output.TimedOut} Assignments={assignments.Count}",
                detailsJson: summaryJson, actorUserId: job.RequestedByUserId, ct: ct);

            // In-app notification — locale-aware
            var locale = input.Locale ?? "en";
            string notifTitle, notifBody;

            if (!shouldDiscard)
            {
                // Count unique people and unique task slots for a meaningful message
                var uniquePeople = assignments.Select(a => a.PersonId).Distinct().Count();
                var uniqueSlots  = assignments.Select(a => a.TaskSlotId).Distinct().Count();
                var coverageNote = output.UncoveredSlotIds.Count > 0
                    ? (locale == "he" ? $" {output.UncoveredSlotIds.Count} משמרות לא מאוישות במלואן."
                       : locale == "ru" ? $" {output.UncoveredSlotIds.Count} смен не укомплектованы."
                       : $" {output.UncoveredSlotIds.Count} shift(s) not fully staffed.")
                    : (locale == "he" ? " כל המשמרות מאוישות במלואן." : locale == "ru" ? " Все смены укомплектованы." : " All shifts fully staffed.");

                (notifTitle, notifBody) = locale switch {
                    "he" => (
                        output.TimedOut ? "הסידור מוכן (חלקי)" : "הסידור מוכן לבדיקה",
                        output.TimedOut
                            ? $"מנוע הסידור הגיע למגבלת הזמן. שובצו {uniquePeople} חברים ל-{uniqueSlots} משמרות.{coverageNote} בדוק ופרסם כשמוכן."
                            : $"שובצו {uniquePeople} חברים ל-{uniqueSlots} משמרות.{coverageNote} בדוק ופרסם כשמוכן."
                    ),
                    "ru" => (
                        output.TimedOut ? "Расписание составлено (частично)" : "Расписание готово к проверке",
                        output.TimedOut
                            ? $"Решатель достиг лимита. Назначено {uniquePeople} человек на {uniqueSlots} смен.{coverageNote} Проверьте и опубликуйте."
                            : $"Назначено {uniquePeople} человек на {uniqueSlots} смен.{coverageNote} Проверьте и опубликуйте."
                    ),
                    _ => (
                        output.TimedOut ? "Schedule ready (partial)" : "Schedule ready for review",
                        output.TimedOut
                            ? $"Solver reached time limit. {uniquePeople} people assigned to {uniqueSlots} shifts.{coverageNote} Review and publish when ready."
                            : $"{uniquePeople} people assigned to {uniqueSlots} shifts.{coverageNote} Review and publish when ready."
                    )
                };
            }
            else
            {
                var conflictDetails = output.HardConflicts.Count > 0
                    ? "\n\n" + string.Join("\n", output.HardConflicts.Select(c => $"• {c.Description}"))
                    : "";

                // Include post-solve violation details in the notification
                var postSolveDetails = hasPostSolveViolations
                    ? "\n\n" + string.Join("\n", postSolveViolations.Select(v => $"• {v}"))
                    : "";

                if (hasPostSolveViolations)
                {
                    (notifTitle, notifBody) = locale switch {
                        "he" => ("הסידור מפר אילוצים קשיחים",
                                 $"מנוע הסידור החזיר תוצאה עם {postSolveViolations.Count} הפרות אילוצים קשיחים. לא נוצר טיוטה.{postSolveDetails}\n\nבדוק הגדרות אילוצים, ודא שיש מספיק חברים מוסמכים, ונסה שוב."),
                        "ru" => ("Расписание нарушает жёсткие ограничения",
                                 $"Решатель вернул результат с {postSolveViolations.Count} нарушениями жёстких ограничений. Черновик не создан.{postSolveDetails}\n\nПроверьте настройки ограничений, убедитесь в наличии квалифицированных участников, и повторите попытку."),
                        _    => ("Schedule violates hard constraints",
                                 $"Solver returned a result with {postSolveViolations.Count} hard constraint violation(s). No draft was created.{postSolveDetails}\n\nReview constraint settings, ensure enough qualified members, and try again.")
                    };
                }
                else
                {
                    (notifTitle, notifBody) = locale switch {
                        "he" => ("לא ניתן ליצור סידור",
                                 $"לא ניתן ליצור סידור עם האילוצים הנוכחיים.{conflictDetails}\n\nודא שיש מספיק חברים ומשימות עתידיות, ונסה שוב."),
                        "ru" => ("Расписание невозможно составить",
                                 $"Не удалось создать расписание при текущих ограничениях.{conflictDetails}\n\nПроверьте наличие достаточного количества участников и будущих задач, затем повторите попытку."),
                        _    => ("Schedule is infeasible",
                                 $"Could not create a schedule under the current constraints.{conflictDetails}\n\nCheck that there are enough members and future tasks, then try again.")
                    };
                }
            }
            await notifier.NotifySpaceAdminsAsync(
                job.SpaceId, evt, notifTitle, notifBody,
                metadataJson: summaryJson, groupId: job.GroupId, ct: ct);

            // Auto-publish: if the group has auto_publish enabled and the schedule is feasible,
            // publish the draft immediately without admin review.
            // Regeneration drafts are NEVER auto-published — they always require admin review.
            var isRegenerationRun = job.TriggerMode == "regeneration";
            if (!shouldDiscard && output.Feasible && job.GroupId.HasValue && version is not null && !isRegenerationRun)
            {
                var autoPublishGroup = await db.Groups.AsNoTracking()
                    .FirstOrDefaultAsync(g => g.Id == job.GroupId.Value, ct);
                if (autoPublishGroup?.AutoPublish == true)
                {
                    version.Publish(Guid.Empty); // Guid.Empty = system-published
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation(
                        "AutoPublish: version {Version} auto-published for group {GroupId}.",
                        nextVersion, job.GroupId.Value);
                }
            }

            _logger.LogInformation(
                "Solver job completed: run_id={RunId} version={Version} feasible={Feasible} assignments={Count}",
                job.RunId, nextVersion, output.Feasible, assignments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Solver job failed: run_id={RunId}", job.RunId);

            // Determine the space's locale — prefer input.Locale (already resolved),
            // fall back to querying the space entity directly so notifications are
            // always in the user's language even if input wasn't built yet.
            var spaceLocale = input?.Locale;
            if (string.IsNullOrEmpty(spaceLocale))
            {
                var space = await db.Spaces.AsNoTracking()
                    .Where(s => s.Id == job.SpaceId)
                    .Select(s => s.Locale)
                    .FirstOrDefaultAsync(ct);
                spaceLocale = space ?? "en";
            }

            // Store a user-friendly error message based on the space's locale
            string userFriendlyError;
            if (ex.Message.Contains("Timeout") || ex.Message.Contains("canceled"))
            {
                userFriendlyError = spaceLocale switch {
                    "he" => "מנוע הסידור לא מצא פתרון בזמן המוקצב. נסה לצמצם את אופק התכנון או לפשט אילוצים.",
                    "ru" => "Решатель не смог найти решение за отведённое время. Попробуйте уменьшить горизонт планирования.",
                    _ => "The solver could not find a solution within the time limit. Try reducing the planning horizon or simplifying constraints."
                };
            }
            else if (ex.Message.Contains("422") || ex.Message.Contains("Unprocessable"))
            {
                userFriendlyError = spaceLocale switch {
                    "he" => "מנוע הסידור דחה את הנתונים. ייתכן שיש בעיה בפורמט המשימות או האילוצים.",
                    "ru" => "Решатель отклонил данные. Возможно, проблема в формате задач или ограничений.",
                    _ => "The solver rejected the data. There may be an issue with the task or constraint format."
                };
            }
            else if (ex.Message.Contains("connect") || ex.Message.Contains("refused"))
            {
                userFriendlyError = spaceLocale switch {
                    "he" => "שירות הסידור אינו זמין. ודא שהוא פועל ונסה שוב.",
                    "ru" => "Служба планирования недоступна. Убедитесь, что она запущена.",
                    _ => "The scheduling service is unavailable. Ensure it is running and try again."
                };
            }
            else
            {
                userFriendlyError = spaceLocale switch {
                    "he" => "אירעה שגיאה בהרצת מנוע הסידור. נסה שוב מאוחר יותר.",
                    "ru" => "Произошла ошибка при составлении расписания. Повторите попытку позже.",
                    _ => "An error occurred while running the scheduler. Please try again later."
                };
            }

            run.MarkFailed(userFriendlyError);
            await db.SaveChangesAsync(ct);

            // System log — solver failed
            var sysLog2 = scope.ServiceProvider.GetRequiredService<ISystemLogger>();
            await sysLog2.LogAsync(job.SpaceId, "error", "solver_failed",
                $"Solver run {job.RunId} failed: {ex.Message}",
                actorUserId: job.RequestedByUserId, ct: ct);

            // In-app notification — failure
            var notifier2 = scope.ServiceProvider.GetRequiredService<INotificationService>();

            // Reuse the same locale and user-friendly error already computed above
            var failTitle = spaceLocale switch {
                "he" => "הרצת הסידור נכשלה",
                "ru" => "Ошибка составления расписания",
                _    => "Scheduling run failed"
            };

            await notifier2.NotifySpaceAdminsAsync(
                job.SpaceId, "solver_failed",
                failTitle,
                userFriendlyError,
                groupId: job.GroupId, ct: ct);
        }
    }

    private static string ComputeHash(object input)
    {
        var json = JsonSerializer.Serialize(input);
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Post-solve validation: checks solver output assignments against input constraints.
    /// Returns a list of human-readable violation descriptions. Empty list = all good.
    /// Emergency-bypassed people are excluded from all checks.
    /// </summary>
    private static List<string> ValidateHardConstraints(SolverInputDto input, List<AssignmentResultDto> assignments)
    {
        var violations = new List<string>();

        // Build lookup of emergency-bypassed person IDs.
        // Emergency constraints with scope "person" bypass all checks for that person.
        var emergencyPersonIds = input.EmergencyConstraints
            .Where(c => c.ScopeType == "person" && c.ScopeId is not null)
            .Select(c => c.ScopeId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Build slot lookup by slot ID
        var slotLookup = input.TaskSlots.ToDictionary(s => s.SlotId, StringComparer.OrdinalIgnoreCase);

        // Build person lookup by person ID
        var personLookup = input.People.ToDictionary(p => p.PersonId, StringComparer.OrdinalIgnoreCase);

        // Build availability windows grouped by person
        var availabilityByPerson = input.AvailabilityWindows
            .GroupBy(a => a.PersonId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // ── 1. Min-rest violations ────────────────────────────────────────────
        // Resolve min_rest_hours from input constraints (same logic as pre-flight)
        var resolvedMinRestHours = ResolveMinRestHours(input);

        if (resolvedMinRestHours > 0)
        {
            // Group assignments by person
            var assignmentsByPerson = assignments
                .Where(a => !emergencyPersonIds.Contains(a.PersonId))
                .Where(a => slotLookup.ContainsKey(a.SlotId))
                .GroupBy(a => a.PersonId, StringComparer.OrdinalIgnoreCase);

            foreach (var personGroup in assignmentsByPerson)
            {
                var personSlots = personGroup
                    .Select(a => slotLookup[a.SlotId])
                    .Where(s => TryParseSlotTimes(s, out _, out _))
                    .OrderBy(s => { TryParseSlotTimes(s, out var start, out _); return start; })
                    .ToList();

                for (int i = 0; i < personSlots.Count - 1; i++)
                {
                    TryParseSlotTimes(personSlots[i], out _, out var end1);
                    TryParseSlotTimes(personSlots[i + 1], out var start2, out _);

                    var gapHours = (start2 - end1).TotalHours;
                    if (gapHours >= 0 && gapHours < resolvedMinRestHours)
                    {
                        var personName = personGroup.Key;
                        violations.Add(
                            $"Min-rest violation: person {personName} has only {gapHours:F1}h rest between " +
                            $"'{personSlots[i].TaskTypeName}' (ends {personSlots[i].EndsAt}) and " +
                            $"'{personSlots[i + 1].TaskTypeName}' (starts {personSlots[i + 1].StartsAt}). " +
                            $"Required: {resolvedMinRestHours}h.");
                    }
                }
            }
        }

        // ── 2. Qualification mismatches ───────────────────────────────────────
        foreach (var assignment in assignments)
        {
            if (emergencyPersonIds.Contains(assignment.PersonId)) continue;
            if (!slotLookup.TryGetValue(assignment.SlotId, out var slot)) continue;
            if (!personLookup.TryGetValue(assignment.PersonId, out var person)) continue;

            if (slot.RequiredQualificationIds.Count > 0)
            {
                var personQualIds = person.QualificationIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var missingQuals = slot.RequiredQualificationIds
                    .Where(q => !personQualIds.Contains(q))
                    .ToList();

                if (missingQuals.Count > 0)
                {
                    violations.Add(
                        $"Qualification mismatch: person {assignment.PersonId} assigned to " +
                        $"'{slot.TaskTypeName}' ({slot.StartsAt}) but missing required qualification(s). " +
                        $"Ensure the person has all required qualifications for this task.");
                }
            }
        }

        // ── 3. Role mismatches ────────────────────────────────────────────────
        foreach (var assignment in assignments)
        {
            if (emergencyPersonIds.Contains(assignment.PersonId)) continue;
            if (!slotLookup.TryGetValue(assignment.SlotId, out var slot)) continue;
            if (!personLookup.TryGetValue(assignment.PersonId, out var person)) continue;

            if (slot.RequiredRoleIds.Count > 0)
            {
                var personRoleIds = person.RoleIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var missingRoles = slot.RequiredRoleIds
                    .Where(r => !personRoleIds.Contains(r))
                    .ToList();

                if (missingRoles.Count > 0)
                {
                    violations.Add(
                        $"Role mismatch: person {assignment.PersonId} assigned to " +
                        $"'{slot.TaskTypeName}' ({slot.StartsAt}) but missing required role(s). " +
                        $"Ensure the person has the correct role for this task.");
                }
            }
        }

        // ── 4. Availability conflicts ─────────────────────────────────────────
        foreach (var assignment in assignments)
        {
            if (emergencyPersonIds.Contains(assignment.PersonId)) continue;
            if (!slotLookup.TryGetValue(assignment.SlotId, out var slot)) continue;
            if (!TryParseSlotTimes(slot, out var slotStart, out var slotEnd)) continue;

            // If no availability windows defined for this person, they're assumed always available
            if (!availabilityByPerson.TryGetValue(assignment.PersonId, out var windows)) continue;
            if (windows.Count == 0) continue;

            // Check if the slot is fully covered by at least one availability window
            var isCovered = windows.Any(w =>
            {
                if (!DateTime.TryParse(w.StartsAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var wStart)) return false;
                if (!DateTime.TryParse(w.EndsAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var wEnd)) return false;
                return wStart <= slotStart && wEnd >= slotEnd;
            });

            if (!isCovered)
            {
                violations.Add(
                    $"Availability conflict: person {assignment.PersonId} assigned to " +
                    $"'{slot.TaskTypeName}' ({slot.StartsAt} – {slot.EndsAt}) but is not available during this time. " +
                    $"Check the person's availability settings.");
            }
        }

        return violations;
    }

    /// <summary>
    /// Resolves the effective min_rest_hours from input constraints.
    /// Priority: HomeLeaveConfig value > hard constraint rule > 8.0 default.
    /// </summary>
    private static double ResolveMinRestHours(SolverInputDto input)
    {
        // If home leave config specifies a positive value, use it
        if (input.HomeLeaveConfig is not null && input.HomeLeaveConfig.Enabled && input.HomeLeaveConfig.MinRestHours > 0)
            return input.HomeLeaveConfig.MinRestHours;

        // Fall back to hard constraint rule
        var hardRestRule = input.HardConstraints.FirstOrDefault(c => c.RuleType == "min_rest_hours");
        if (hardRestRule is not null && hardRestRule.Payload.TryGetValue("hours", out var hoursVal))
        {
            var hours = ToDouble(hoursVal);
            if (hours > 0) return hours;
        }

        // Fall back to soft constraint rule
        var softRestRule = input.SoftConstraints.FirstOrDefault(c => c.RuleType == "min_rest_hours");
        if (softRestRule is not null && softRestRule.Payload.TryGetValue("hours", out var softHoursVal))
        {
            var hours = ToDouble(softHoursVal);
            if (hours > 0) return hours;
        }

        // Default: 8 hours for closed-base groups with home leave, 0 otherwise
        if (input.HomeLeaveConfig is not null && input.HomeLeaveConfig.Enabled)
            return 8.0;

        return 0.0;
    }

    /// <summary>
    /// Attempts to parse slot start/end times from ISO 8601 strings.
    /// </summary>
    private static bool TryParseSlotTimes(TaskSlotDto slot, out DateTime start, out DateTime end)
    {
        start = default;
        end = default;
        return DateTime.TryParse(slot.StartsAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out start)
            && DateTime.TryParse(slot.EndsAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out end);
    }

    /// <summary>
    /// Safely converts a constraint payload value to double.
    /// Payload values come back as JsonElement when deserialized from JSON,
    /// so we need to handle that case explicitly.
    /// </summary>
    private static double ToDouble(object value) => value switch
    {
        System.Text.Json.JsonElement je => je.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Number => je.GetDouble(),
            System.Text.Json.JsonValueKind.String => double.TryParse(je.GetString(), out var d) ? d : 0.0,
            _ => 0.0
        },
        _ => Convert.ToDouble(value)
    };
}
