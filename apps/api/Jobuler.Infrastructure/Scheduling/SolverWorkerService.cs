using Jobuler.Application.Notifications;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Commands;
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

        try
        {
            // Build solver input
            var input = await normalizer.BuildAsync(
                job.SpaceId, job.RunId, job.TriggerMode, job.BaselineVersionId, ct);

            // ── Pre-flight checks ─────────────────────────────────────────────
            if (input.TaskSlots.Count == 0)
            {
                run.MarkFailed("No future tasks to schedule.");
                await db.SaveChangesAsync(ct);
                var (noTasksTitle, noTasksBody) = input.Locale switch {
                    "en" => ("No tasks to schedule", "No future tasks found in the current time window. Create tasks with future dates and try again."),
                    "ru" => ("Нет задач для планирования", "Не найдено будущих задач в текущем временном окне. Создайте задачи с будущими датами и повторите попытку."),
                    _    => ("אין משימות לסידור", "לא נמצאו משימות עתידיות בטווח הזמן הנוכחי. צור משימות עם תאריכים עתידיים ונסה שוב.")
                };
                await notifier.NotifySpaceAdminsAsync(
                    job.SpaceId, "solver_no_tasks", noTasksTitle, noTasksBody, ct: ct);
                return;
            }

            if (input.People.Count == 0)
            {
                run.MarkFailed("No active members to schedule.");
                await db.SaveChangesAsync(ct);
                var (noPeopleTitle, noPeopleBody) = input.Locale switch {
                    "en" => ("No active members", "No active members found in the group. Add members and try again."),
                    "ru" => ("Нет активных участников", "В группе не найдено активных участников. Добавьте участников и повторите попытку."),
                    _    => ("אין חברים פעילים", "לא נמצאו חברים פעילים בקבוצה. הוסף חברים ונסה שוב.")
                };
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

            var inputHash = ComputeHash(input);
            run.MarkRunning(inputHash);
            await db.SaveChangesAsync(ct);

            // Call solver
            var output = await client.SolveAsync(input, ct);

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

            db.ScheduleVersions.Add(version);
            await db.SaveChangesAsync(ct); // get version.Id

            // Persist assignments
            // Slot IDs from GroupTask shifts are composite: "<taskId>:shift:<n>"
            // Store the base task ID as the task_slot_id for traceability
            var assignments = output.Assignments.Select(a =>
            {
                var rawSlotId = a.SlotId;
                var baseSlotId = rawSlotId.Contains(":shift:")
                    ? rawSlotId.Split(":shift:")[0]
                    : rawSlotId;
                return Assignment.Create(
                    job.SpaceId, version.Id,
                    Guid.Parse(baseSlotId), Guid.Parse(a.PersonId),
                    a.Source == "override" ? AssignmentSource.Override : AssignmentSource.Solver);
            }).ToList();

            db.Assignments.AddRange(assignments);

            // Compute and store diff summary
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

            // Update fairness counters after successful solve
            await mediator.Send(new UpdateFairnessCountersCommand(job.SpaceId, version.Id), ct);

            // System log — solver completed
            var sev = output.TimedOut ? "warning" : (output.Feasible ? "info" : "error");
            var evt = output.Feasible ? "solver_completed" : "solver_infeasible";
            await sysLog.LogAsync(job.SpaceId, sev, evt,
                $"Solver run {job.RunId} finished. Feasible={output.Feasible} TimedOut={output.TimedOut} Assignments={assignments.Count}",
                detailsJson: summaryJson, actorUserId: job.RequestedByUserId, ct: ct);

            // In-app notification — locale-aware
            var locale = input.Locale ?? "en";
            string notifTitle, notifBody;

            if (output.Feasible)
            {
                (notifTitle, notifBody) = locale switch {
                    "he" => (
                        output.TimedOut ? "הסידור הושלם (חלקי)" : "הסידור מוכן לעיון",
                        output.TimedOut
                            ? $"הסידור הגיע לגבול הזמן — נוצרה טיוטה עם {assignments.Count} שיבוצים.{(output.UncoveredSlotIds.Count > 0 ? $" {output.UncoveredSlotIds.Count} משימות לא אויישו." : " כל המשימות אויישו.")} בדוק ופרסם כשמוכן."
                            : $"נוצרה טיוטה עם {assignments.Count} שיבוצים.{(output.UncoveredSlotIds.Count > 0 ? $" {output.UncoveredSlotIds.Count} משימות לא אויישו." : " כל המשימות אויישו.")} בדוק ופרסם כשמוכן."
                    ),
                    "ru" => (
                        output.TimedOut ? "Расписание составлено (частично)" : "Расписание готово к проверке",
                        output.TimedOut
                            ? $"Решатель достиг лимита времени — создан черновик с {assignments.Count} назначениями.{(output.UncoveredSlotIds.Count > 0 ? $" {output.UncoveredSlotIds.Count} смен не укомплектованы." : " Все смены укомплектованы.")} Проверьте и опубликуйте."
                            : $"Создан черновик с {assignments.Count} назначениями.{(output.UncoveredSlotIds.Count > 0 ? $" {output.UncoveredSlotIds.Count} смен не укомплектованы." : " Все смены укомплектованы.")} Проверьте и опубликуйте."
                    ),
                    _ => (
                        output.TimedOut ? "Schedule ready (partial)" : "Schedule ready for review",
                        output.TimedOut
                            ? $"Solver reached time limit — draft created with {assignments.Count} assignments.{(output.UncoveredSlotIds.Count > 0 ? $" {output.UncoveredSlotIds.Count} slot(s) not fully staffed." : " All slots staffed.")} Review and publish when ready."
                            : $"Draft created with {assignments.Count} assignments.{(output.UncoveredSlotIds.Count > 0 ? $" {output.UncoveredSlotIds.Count} slot(s) not fully staffed." : " All slots staffed.")} Review and publish when ready."
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
}
