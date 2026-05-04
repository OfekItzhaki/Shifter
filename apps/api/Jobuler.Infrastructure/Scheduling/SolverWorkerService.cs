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

                run.MarkFailed("Orphaned — API restarted while run was in progress.");
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
            // Build solver input
            input = await normalizer.BuildAsync(
                job.SpaceId, job.RunId, job.TriggerMode, job.BaselineVersionId, job.GroupId, job.StartTime, ct);

            // ── Pre-flight checks ─────────────────────────────────────────────
            if (input.TaskSlots.Count == 0)
            {
                var (noTasksTitle, noTasksBody) = input.Locale switch {
                    "en" => ("No tasks to schedule", "No future tasks found in the current time window. Create tasks with future dates and try again."),
                    "ru" => ("Нет задач для планирования", "Не найдено будущих задач в текущем временном окне. Создайте задачи с будущими датами и повторите попытку."),
                    _    => ("אין משימות לסידור", "לא נמצאו משימות עתידיות בטווח הזמן הנוכחי. צור משימות עם תאריכים עתידיים ונסה שוב.")
                };
                run.MarkFailed(noTasksBody);
                await db.SaveChangesAsync(ct);
                await notifier.NotifySpaceAdminsAsync(
                    job.SpaceId, "solver_no_tasks", noTasksTitle, noTasksBody, ct: ct);
                return;
            }

            if (input.People.Count == 0)
            {
                var (noPeopleTitle, noPeopleBody) = input.Locale switch {
                    "en" => ("No active members", "No active members found in the group. Add members and try again."),
                    "ru" => ("Нет активных участников", "В группе не найдено активных участников. Добавьте участников и повторите попытку."),
                    _    => ("אין חברים פעילים", "לא נמצאו חברים פעילים בקבוצה. הוסף חברים ונסה שוב.")
                };
                run.MarkFailed(noPeopleBody);
                await db.SaveChangesAsync(ct);
                await notifier.NotifySpaceAdminsAsync(
                    job.SpaceId, "solver_no_people", noPeopleTitle, noPeopleBody, ct: ct);
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
                    "he" => $"נדרשים לפחות {totalMinPeople} חברים כדי לכסות את כל המשימות 24/7 ({taskSummary}), אך יש רק {input.People.Count} חברים פעילים בקבוצות. הוסף חברים ונסה שוב.",
                    "ru" => $"Для покрытия всех задач 24/7 ({taskSummary}) требуется минимум {totalMinPeople} участников, но активных только {input.People.Count}. Добавьте участников и повторите попытку.",
                    _    => $"At least {totalMinPeople} members are needed to cover all tasks 24/7 ({taskSummary}), but only {input.People.Count} active members are in groups. Add more members and try again."
                };
                var preflightTitle = preflightLocale switch {
                    "he" => "לא ניתן ליצור סידור — אין מספיק חברים",
                    "ru" => "Невозможно составить расписание — недостаточно участников",
                    _    => "Cannot schedule — not enough members"
                };
                run.MarkFailed(reason);
                await db.SaveChangesAsync(ct);
                await notifier.NotifySpaceAdminsAsync(
                    job.SpaceId, "solver_preflight_failed", preflightTitle, reason, ct: ct);
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

            // Determine next version number for this space
            var nextVersion = await db.ScheduleVersions
                .Where(v => v.SpaceId == job.SpaceId)
                .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;
            nextVersion++;

            // Create draft version
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
                }).ToList()
            });

            var version = ScheduleVersion.CreateDraft(
                job.SpaceId, nextVersion, job.BaselineVersionId,
                job.RunId, job.RequestedByUserId, summaryJson);

            // Build assignments list first so we can decide whether to keep the draft
            var assignments = output.Feasible
                ? output.Assignments.Select(a =>
                {
                    if (!Guid.TryParse(a.SlotId, out var slotGuid))
                    {
                        _logger.LogWarning("Cannot parse slot_id as GUID: '{SlotId}' — skipping assignment for person {PersonId}", a.SlotId, a.PersonId);
                        return null;
                    }
                    return Assignment.Create(
                        job.SpaceId, version.Id,
                        slotGuid, Guid.Parse(a.PersonId),
                        a.Source == "override" ? AssignmentSource.Override : AssignmentSource.Solver);
                }).Where(a => a != null).Cast<Assignment>().ToList()
                : new List<Assignment>();

            _logger.LogInformation(
                "Solver output: feasible={Feasible} timedOut={TimedOut} rawAssignments={Raw} parsedAssignments={Parsed}",
                output.Feasible, output.TimedOut, output.Assignments.Count, assignments.Count);

            // Do NOT persist a version if there's nothing useful to show the admin:
            // - solver proved infeasible, OR
            // - feasible but zero assignments (solver bug / all slots unparseable)
            // In all these cases, mark the run failed/timed-out and notify the admin.
            // A timed-out run with partial assignments IS kept — it has useful data.
            var shouldDiscard = !output.Feasible || assignments.Count == 0;

            if (!shouldDiscard)
            {
                db.ScheduleVersions.Add(version);
                await db.SaveChangesAsync(ct); // get version.Id

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
            }
            else
            {
                // No version created — just update the run status
                _logger.LogWarning(
                    "Skipping version creation: feasible={Feasible} assignments={Count}",
                    output.Feasible, assignments.Count);

                if (output.TimedOut)
                    run.MarkTimedOut(summaryJson);
                else if (!output.Feasible)
                    run.MarkFailed($"Solver returned infeasible. Hard conflicts: {output.HardConflicts.Count}");
                else
                    run.MarkFailed("Solver returned zero assignments despite reporting feasible.");

                await db.SaveChangesAsync(ct);
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
                    ? (locale == "he" ? $" {output.UncoveredSlotIds.Count} משמרות לא אויישו במלואן."
                       : locale == "ru" ? $" {output.UncoveredSlotIds.Count} смен не укомплектованы."
                       : $" {output.UncoveredSlotIds.Count} shift(s) not fully staffed.")
                    : (locale == "he" ? " כל המשמרות אויישו." : locale == "ru" ? " Все смены укомплектованы." : " All shifts fully staffed.");

                (notifTitle, notifBody) = locale switch {
                    "he" => (
                        output.TimedOut ? "הסידור הושלם (חלקי)" : "הסידור מוכן לעיון",
                        output.TimedOut
                            ? $"הסידור הגיע לגבול הזמן — שובצו {uniquePeople} אנשים ל-{uniqueSlots} משמרות.{coverageNote} בדוק ופרסם כשמוכן."
                            : $"שובצו {uniquePeople} אנשים ל-{uniqueSlots} משמרות.{coverageNote} בדוק ופרסם כשמוכן."
                    ),
                    "ru" => (
                        output.TimedOut ? "Расписание составлено (частично)" : "Расписание готово к проверке",
                        output.TimedOut
                            ? $"Решатель достиг лимита — назначено {uniquePeople} человек на {uniqueSlots} смен.{coverageNote} Проверьте и опубликуйте."
                            : $"Назначено {uniquePeople} человек на {uniqueSlots} смен.{coverageNote} Проверьте и опубликуйте."
                    ),
                    _ => (
                        output.TimedOut ? "Schedule ready (partial)" : "Schedule ready for review",
                        output.TimedOut
                            ? $"Solver reached time limit — {uniquePeople} people assigned to {uniqueSlots} shifts.{coverageNote} Review and publish when ready."
                            : $"{uniquePeople} people assigned to {uniqueSlots} shifts.{coverageNote} Review and publish when ready."
                    )
                };
            }
            else
            {
                var conflictDetails = output.HardConflicts.Count > 0
                    ? "\n\n" + string.Join("\n", output.HardConflicts.Select(c => $"• {c.Description}"))
                    : "";
                (notifTitle, notifBody) = locale switch {
                    "he" => ("לא נמצא סידור אפשרי",
                             $"לא ניתן היה ליצור סידור עם האילוצים הנוכחיים.{conflictDetails}\n\nבדוק שיש מספיק חברים ומשימות עתידיות, ונסה שוב."),
                    "ru" => ("Расписание невозможно составить",
                             $"Не удалось создать расписание при текущих ограничениях.{conflictDetails}\n\nПроверьте наличие достаточного количества участников и будущих задач, затем повторите попытку."),
                    _    => ("Schedule is infeasible",
                             $"Could not create a schedule under the current constraints.{conflictDetails}\n\nCheck that there are enough members and future tasks, then try again.")
                };
            }
            await notifier.NotifySpaceAdminsAsync(
                job.SpaceId, evt, notifTitle, notifBody,
                metadataJson: summaryJson, ct: ct);

            _logger.LogInformation(
                "Solver job completed: run_id={RunId} version={Version} feasible={Feasible} assignments={Count}",
                job.RunId, nextVersion, output.Feasible, assignments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Solver job failed: run_id={RunId}", job.RunId);
            run.MarkFailed(ex.Message);
            await db.SaveChangesAsync(ct);

            // System log — solver failed
            var sysLog2 = scope.ServiceProvider.GetRequiredService<ISystemLogger>();
            await sysLog2.LogAsync(job.SpaceId, "error", "solver_failed",
                $"Solver run {job.RunId} failed: {ex.Message}",
                actorUserId: job.RequestedByUserId, ct: ct);

            // In-app notification — failure
            var notifier2 = scope.ServiceProvider.GetRequiredService<INotificationService>();

            // Translate technical error to a user-friendly message in the space's locale
            var spaceLocale = input?.Locale ?? "en";
            var friendlyError = spaceLocale switch {
                "he" => ex.Message.Contains("422") || ex.Message.Contains("Unprocessable")
                    ? "הסולבר דחה את הנתונים — ייתכן שיש בעיה בפורמט המשימות או האילוצים."
                    : ex.Message.Contains("connect") || ex.Message.Contains("refused")
                        ? "שירות הסידור אינו זמין כרגע. ודא שהוא פועל ונסה שוב."
                        : "אירעה שגיאה בעת הרצת הסידור. נסה שוב מאוחר יותר.",
                "ru" => ex.Message.Contains("422") || ex.Message.Contains("Unprocessable")
                    ? "Решатель отклонил данные — возможно, проблема в формате задач или ограничений."
                    : ex.Message.Contains("connect") || ex.Message.Contains("refused")
                        ? "Служба планирования недоступна. Убедитесь, что она запущена, и повторите попытку."
                        : "Произошла ошибка при составлении расписания. Повторите попытку позже.",
                _ => ex.Message.Contains("422") || ex.Message.Contains("Unprocessable")
                    ? "The solver rejected the data — there may be an issue with the task or constraint format."
                    : ex.Message.Contains("connect") || ex.Message.Contains("refused")
                        ? "The scheduling service is unavailable. Ensure it is running and try again."
                        : "An error occurred while running the scheduler. Please try again later."
            };
            var (failTitle, _) = spaceLocale switch {
                "he" => ("הרצת הסידור נכשלה", ""),
                "ru" => ("Ошибка составления расписания", ""),
                _    => ("Scheduling run failed", "")
            };

            await notifier2.NotifySpaceAdminsAsync(
                job.SpaceId, "solver_failed",
                failTitle,
                friendlyError,
                ct: ct);
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
