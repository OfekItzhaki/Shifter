"use client";

import { useTranslations } from "next-intl";
import { useSandboxStore, type SolverOutputDto, type HomeLeaveAssignmentDto } from "@/lib/store/sandboxStore";
import ScheduleTaskTable, { type TaskAssignment } from "@/components/schedule/ScheduleTaskTable";

/**
 * SandboxSchedulePreview — Right panel in the sandbox split view.
 *
 * Subscribes ONLY to simulation results (`lastSimulationResult`, `isSimulating`,
 * `simulationError`) to prevent unnecessary re-renders when the settings panel
 * changes override state.
 *
 * Renders:
 * - Loading indicator during simulation
 * - Error messages on failure (timeout, infeasibility)
 * - Assignment table (reuses ScheduleTaskTable)
 * - Home-leave preview section when group has home-leave enabled
 *
 * Requirements: 6.4, 6.5, 8.2, 8.3, 12.1, 12.3
 */
export default function SandboxSchedulePreview() {
  const t = useTranslations("sandbox");

  // Subscribe ONLY to simulation-related state — NOT override state
  const lastSimulationResult = useSandboxStore((s) => s.lastSimulationResult);
  const isSimulating = useSandboxStore((s) => s.isSimulating);
  const simulationError = useSandboxStore((s) => s.simulationError);
  const baseline = useSandboxStore((s) => s.baseline);

  // Determine if home-leave is enabled for this group
  const homeLeaveEnabled = baseline?.homeLeaveConfig?.enabled ?? false;

  // Loading state
  if (isSimulating) {
    return (
      <div className="flex flex-col items-center justify-center h-full py-16 gap-3">
        <svg className="animate-spin h-8 w-8 text-sky-500" fill="none" viewBox="0 0 24 24">
          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
        </svg>
        <p className="text-sm text-slate-500 dark:text-slate-400">{t("preview.simulating")}</p>
      </div>
    );
  }

  // Error state
  if (simulationError) {
    return (
      <div className="flex flex-col items-center justify-center h-full py-16 gap-3">
        <div className="w-12 h-12 rounded-full bg-red-100 dark:bg-red-900/30 flex items-center justify-center">
          <svg width="24" height="24" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2} className="text-red-500">
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
          </svg>
        </div>
        <p className="text-sm font-medium text-red-600 dark:text-red-400">{simulationError}</p>
      </div>
    );
  }

  // No result yet — show placeholder
  if (!lastSimulationResult) {
    return (
      <div className="flex flex-col items-center justify-center h-full py-16 gap-3 text-center px-6">
        <div className="w-12 h-12 rounded-full bg-slate-100 dark:bg-slate-800 flex items-center justify-center">
          <svg width="24" height="24" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5} className="text-slate-400">
            <path strokeLinecap="round" strokeLinejoin="round" d="M3 10h18M3 14h18m-9-4v8m-7 0h14a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v8a2 2 0 002 2z" />
          </svg>
        </div>
        <p className="text-sm text-slate-500 dark:text-slate-400">{t("preview.placeholder")}</p>
      </div>
    );
  }

  // Result received — check for timeout or infeasibility
  if (lastSimulationResult.timed_out) {
    return <TimedOutMessage t={t} />;
  }

  if (!lastSimulationResult.feasible) {
    return <InfeasibleMessage t={t} result={lastSimulationResult} />;
  }

  // Successful result — render assignment table and home-leave preview
  const assignments = mapAssignmentsForTable(lastSimulationResult, baseline);

  return (
    <div className="flex flex-col h-full overflow-y-auto p-4 gap-6">
      {/* Assignment table */}
      <div>
        <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200 mb-3">
          {t("preview.assignmentsTitle")}
        </h3>
        <ScheduleTaskTable assignments={assignments} />
      </div>

      {/* Home-leave preview section — only when group has home-leave enabled */}
      {homeLeaveEnabled && lastSimulationResult.home_leave_assignments.length > 0 && (
        <HomeLeavePreview
          t={t}
          homeLeaveAssignments={lastSimulationResult.home_leave_assignments}
          baseline={baseline}
        />
      )}

      {/* Solver metrics summary */}
      <div className="text-xs text-slate-400 dark:text-slate-500 border-t border-slate-100 dark:border-slate-700 pt-3">
        {t("preview.solverTime", { ms: lastSimulationResult.solver_time_ms })}
      </div>
    </div>
  );
}

// ── Helper: Map solver output to ScheduleTaskTable format ─────────────────────

function mapAssignmentsForTable(
  result: SolverOutputDto,
  baseline: ReturnType<typeof useSandboxStore.getState>["baseline"]
): TaskAssignment[] {
  // Build lookup maps from baseline data
  const slotMap = new Map<string, { taskTypeName: string; startsAt: string; endsAt: string }>();
  if (baseline?.taskSlots) {
    for (const slot of baseline.taskSlots) {
      slotMap.set(slot.slotId, {
        taskTypeName: slot.taskTypeName,
        startsAt: slot.startsAt,
        endsAt: slot.endsAt,
      });
    }
  }

  const personMap = new Map<string, string>();
  if (baseline?.people) {
    for (const p of baseline.people) {
      personMap.set(p.personId, p.personId); // personId as display name fallback
    }
  }

  return result.assignments.map((a) => {
    const slot = slotMap.get(a.slot_id);
    return {
      personId: a.person_id,
      personName: personMap.get(a.person_id) ?? a.person_id,
      taskTypeName: slot?.taskTypeName ?? a.source ?? "unknown",
      slotStartsAt: slot?.startsAt ?? "",
      slotEndsAt: slot?.endsAt ?? "",
    };
  });
}

// ── Sub-components ────────────────────────────────────────────────────────────

function TimedOutMessage({ t }: { t: ReturnType<typeof useTranslations> }) {
  return (
    <div className="flex flex-col items-center justify-center h-full py-16 gap-3 text-center px-6">
      <div className="w-12 h-12 rounded-full bg-amber-100 dark:bg-amber-900/30 flex items-center justify-center">
        <svg width="24" height="24" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2} className="text-amber-500">
          <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
      </div>
      <p className="text-sm font-medium text-amber-600 dark:text-amber-400">{t("preview.timedOut")}</p>
      <p className="text-xs text-slate-500 dark:text-slate-400">{t("preview.timedOutHint")}</p>
    </div>
  );
}

function InfeasibleMessage({ t, result }: { t: ReturnType<typeof useTranslations>; result: SolverOutputDto }) {
  return (
    <div className="flex flex-col items-center justify-center h-full py-12 gap-4 text-center px-6">
      <div className="w-12 h-12 rounded-full bg-red-100 dark:bg-red-900/30 flex items-center justify-center">
        <svg width="24" height="24" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2} className="text-red-500">
          <path strokeLinecap="round" strokeLinejoin="round" d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636" />
        </svg>
      </div>
      <p className="text-sm font-medium text-red-600 dark:text-red-400">{t("preview.infeasible")}</p>
      <p className="text-xs text-slate-500 dark:text-slate-400">{t("preview.infeasibleHint")}</p>

      {/* Hard conflicts list */}
      {result.hard_conflicts.length > 0 && (
        <div className="w-full max-w-md mt-2">
          <h4 className="text-xs font-semibold text-slate-600 dark:text-slate-300 mb-2 text-start">
            {t("preview.conflicts")}
          </h4>
          <ul className="space-y-1.5 text-start">
            {result.hard_conflicts.map((conflict, idx) => (
              <li
                key={conflict.constraint_id || idx}
                className="text-xs bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg px-3 py-2 text-red-700 dark:text-red-300"
              >
                <span className="font-medium">{conflict.rule_type}</span>
                {conflict.description && (
                  <span className="text-red-500 dark:text-red-400"> — {conflict.description}</span>
                )}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

function HomeLeavePreview({
  t,
  homeLeaveAssignments,
  baseline,
}: {
  t: ReturnType<typeof useTranslations>;
  homeLeaveAssignments: HomeLeaveAssignmentDto[];
  baseline: ReturnType<typeof useSandboxStore.getState>["baseline"];
}) {
  // Build a person ID → name lookup from baseline people
  const personNameMap = new Map<string, string>();
  if (baseline?.people) {
    for (const p of baseline.people) {
      personNameMap.set(p.personId, p.personId); // personId used as fallback name
    }
  }

  return (
    <div className="border border-emerald-200 dark:border-emerald-700 rounded-xl bg-emerald-50/50 dark:bg-emerald-950/20 p-4">
      <div className="flex items-center gap-2 mb-3">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="text-emerald-600 dark:text-emerald-400">
          <path strokeLinecap="round" strokeLinejoin="round" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6" />
        </svg>
        <h3 className="text-sm font-semibold text-emerald-700 dark:text-emerald-300">
          {t("preview.homeLeaveTitle")}
        </h3>
        <span className="text-xs px-2 py-0.5 rounded-full text-emerald-600 bg-emerald-100 dark:bg-emerald-900/40 dark:text-emerald-300">
          {homeLeaveAssignments.length}
        </span>
      </div>

      <div className="overflow-x-auto rounded-lg border border-emerald-200 dark:border-emerald-700">
        <table className="w-full text-sm border-collapse">
          <thead>
            <tr className="border-b border-emerald-100 dark:border-emerald-800 bg-emerald-50/80 dark:bg-emerald-950/50">
              <th className="px-3 py-2 text-start text-xs font-semibold uppercase tracking-wider text-emerald-600 dark:text-emerald-400">
                {t("preview.member")}
              </th>
              <th className="px-3 py-2 text-start text-xs font-semibold uppercase tracking-wider text-emerald-600 dark:text-emerald-400">
                {t("preview.leaveWindow")}
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-emerald-100 dark:divide-emerald-800">
            {homeLeaveAssignments.map((hla, idx) => (
              <tr key={`${hla.person_id}-${idx}`} className="hover:bg-emerald-100/40 dark:hover:bg-emerald-900/30">
                <td className="px-3 py-2 text-sm text-emerald-800 dark:text-emerald-200">
                  {personNameMap.get(hla.person_id) ?? hla.person_id}
                </td>
                <td className="px-3 py-2 text-xs tabular-nums text-emerald-600 dark:text-emerald-400">
                  {formatLeaveWindow(hla.starts_at, hla.ends_at)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ── Utility ───────────────────────────────────────────────────────────────────

function formatLeaveWindow(startsAt: string, endsAt: string): string {
  const start = new Date(startsAt);
  const end = new Date(endsAt);
  const dateOpts: Intl.DateTimeFormatOptions = { month: "short", day: "numeric" };
  const timeOpts: Intl.DateTimeFormatOptions = { hour: "2-digit", minute: "2-digit", hour12: false };

  const startDate = start.toLocaleDateString(undefined, dateOpts);
  const startTime = start.toLocaleTimeString(undefined, timeOpts);
  const endDate = end.toLocaleDateString(undefined, dateOpts);
  const endTime = end.toLocaleTimeString(undefined, timeOpts);

  if (startDate === endDate) {
    return `${startDate} ${startTime} – ${endTime}`;
  }
  return `${startDate} ${startTime} – ${endDate} ${endTime}`;
}
