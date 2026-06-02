"use client";

import { useState } from "react";
import { useTranslations, useLocale } from "next-intl";
import { useAuthStore } from "@/lib/store/authStore";
import { formatLocalTime } from "@/lib/utils/formatTime";
import CantMakeItModal from "./CantMakeItModal";

/** Task type name used for home-leave assignments from the solver */
const HOME_LEAVE_TASK_TYPE = "home_leave";

/** Display label for home-leave assignments */
const HOME_LEAVE_LABEL = "בבית";

/** Check if a task type represents a home-leave assignment */
function isHomeLeaveTask(taskTypeName: string): boolean {
  return taskTypeName === HOME_LEAVE_TASK_TYPE;
}

/** Minimal assignment shape needed by this component */
export interface TaskAssignment {
  id?: string;
  personId?: string;  // optional — only needed for admin "can't make it" action
  personName: string;
  taskTypeName: string;
  slotStartsAt: string;
  slotEndsAt: string;
}

interface Props {
  assignments: TaskAssignment[];
  currentUserName?: string;
  filterDate?: string;
  /** Admin-only: enables the "can't make it" button per person */
  isAdmin?: boolean;
  spaceId?: string;
  /** Optional map of personId → role hex color for visual indicators */
  roleColorMap?: Map<string, string | null>;
  /** Called after a presence window is saved — parent decides whether to re-run solver */
  onPersonBlocked?: (personId: string, triggerRerun: boolean) => void;
}

function formatTime(iso: string, timezoneId: string | null): string {
  return formatLocalTime(iso, timezoneId, "24h");
}

function formatShiftTime(startIso: string, endIso: string, locale?: string, timezoneId?: string | null): string {
  const start = new Date(startIso);
  const end = new Date(endIso);
  const durationHours = (end.getTime() - start.getTime()) / (1000 * 60 * 60);

  // For 24h shifts, show "24 שעות" with the start time
  if (durationHours >= 23.5) {
    return `${formatTime(startIso, timezoneId ?? null)} (24h)`;
  }

  // Use directional arrow: ← for RTL (Hebrew), → for LTR
  const arrow = locale === "he" ? "←" : "→";
  return `${formatTime(startIso, timezoneId ?? null)} ${arrow} ${formatTime(endIso, timezoneId ?? null)}`;
}

function overlapsDate(a: TaskAssignment, dateStr: string): boolean {
  const dayStart = new Date(dateStr + "T00:00:00").getTime();
  const dayEnd   = new Date(dateStr + "T23:59:59").getTime();
  const slotStart = new Date(a.slotStartsAt).getTime();
  const slotEnd   = new Date(a.slotEndsAt).getTime();
  if (isNaN(slotStart) || isNaN(slotEnd)) return false;

  const durationHours = (slotEnd - slotStart) / (1000 * 60 * 60);

  // For long shifts (>=12h like 24h tasks), only show on the day they START
  // This prevents yesterday's 24h shift from appearing on today's view
  if (durationHours >= 12) {
    const slotStartDate = new Date(a.slotStartsAt).toLocaleDateString("sv"); // YYYY-MM-DD format
    return slotStartDate === dateStr;
  }

  // For normal shifts, show if they overlap with the selected day
  return slotStart <= dayEnd && slotEnd > dayStart;
}

/**
 * Renders one table per task type.
 * Each table has:
 *   - Rows = time slots for that task (sorted chronologically)
 *   - Columns = person slots (one column per person assigned to that slot, up to max headcount)
 *   - Empty columns shown as "—" when a slot has fewer people than the max
 *
 * This cleanly handles tasks with different shift times and multiple people per shift.
 */
export default function ScheduleTaskTable({ assignments, currentUserName, filterDate, isAdmin, spaceId, roleColorMap, onPersonBlocked }: Props) {
  const t = useTranslations("schedule");
  const locale = useLocale();
  const timezoneId = useAuthStore(s => s.timezoneId);
  const [cantMakeIt, setCantMakeIt] = useState<{ personId: string; personName: string } | null>(null);

  const visible = filterDate
    ? assignments.filter(a => overlapsDate(a, filterDate))
    : assignments;

  if (visible.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-center bg-white dark:bg-slate-800 rounded-xl border border-slate-200 dark:border-slate-700">
        <p className="text-sm text-slate-400">{t("noTasksThisDay")}</p>
      </div>
    );
  }

  // Group assignments by task type
  const byTask = new Map<string, TaskAssignment[]>();
  for (const a of visible) {
    const list = byTask.get(a.taskTypeName) ?? [];
    list.push(a);
    byTask.set(a.taskTypeName, list);
  }

  const taskNames = Array.from(byTask.keys()).sort();

  return (
    <>
      <div className="space-y-6">
        {taskNames.map(taskName => {
        const taskAssignments = byTask.get(taskName)!;
        const isHomeLeave = isHomeLeaveTask(taskName);
        const displayName = isHomeLeave ? HOME_LEAVE_LABEL : taskName;

        // Group by slot key → list of people assigned to that slot
        const slotMap = new Map<string, { startsAt: string; endsAt: string; people: string[]; personIds: string[] }>();
        for (const a of taskAssignments) {
          const key = `${a.slotStartsAt}|${a.slotEndsAt}`;
          const slot = slotMap.get(key) ?? { startsAt: a.slotStartsAt, endsAt: a.slotEndsAt, people: [], personIds: [] };
          // Deduplicate: don't add the same person twice to the same slot
          if (!slot.personIds.includes(a.personId ?? "")) {
            slot.people.push(a.personName);
            slot.personIds.push(a.personId ?? "");
          }
          slotMap.set(key, slot);
        }

        const slots = Array.from(slotMap.values())
          .sort((a, b) => new Date(a.startsAt).getTime() - new Date(b.startsAt).getTime());

        // Max people in any single slot = number of person columns
        const maxPeople = Math.max(...slots.map(s => s.people.length), 1);
        const personCols = Array.from({ length: maxPeople }, (_, i) => i);

        return (
          <div key={taskName}>
            {/* Task header */}
            <div className="flex items-center gap-2 mb-2">
              {isHomeLeave && (
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="text-emerald-600 dark:text-emerald-400 flex-shrink-0">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6" />
                </svg>
              )}
              <span className={`text-sm font-semibold ${isHomeLeave ? "text-emerald-700 dark:text-emerald-300" : "text-slate-800 dark:text-slate-200"}`}>
                {displayName}
              </span>
            </div>

            <div className={`overflow-x-auto rounded-xl border shadow-sm -mx-2 sm:mx-0 ${isHomeLeave ? "border-emerald-200 dark:border-emerald-700 bg-emerald-50 dark:bg-emerald-950/30" : "border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800"}`}>
              <table className="w-full text-sm border-collapse table-fixed min-w-[280px]">
                <thead>
                  <tr className={`border-b ${isHomeLeave ? "border-emerald-100 dark:border-emerald-800 bg-emerald-50/80 dark:bg-emerald-950/50" : "border-slate-100 dark:border-slate-700 bg-slate-50/80 dark:bg-slate-800/80"}`}>
                    <th className={`px-2.5 sm:px-4 py-2.5 sm:py-3 text-start text-xs font-semibold uppercase tracking-wider whitespace-nowrap sticky right-0 z-10 ${isHomeLeave ? "text-emerald-600 dark:text-emerald-400 bg-emerald-50/80 dark:bg-emerald-950/50" : "text-slate-500 dark:text-slate-400 bg-slate-50/80 dark:bg-slate-800/80"}`}>
                      {t("time")}
                    </th>
                    {personCols.map(i => (
                      <th key={i} className={`px-2.5 sm:px-4 py-2.5 sm:py-3 text-center text-xs font-semibold uppercase tracking-wider ${isHomeLeave ? "text-emerald-600 dark:text-emerald-400" : "text-slate-500 dark:text-slate-400"}`}>
                        {maxPeople === 1 ? t("assignee") : t("assigneeN", { n: i + 1 })}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody className={`divide-y ${isHomeLeave ? "divide-emerald-100 dark:divide-emerald-800" : "divide-slate-100 dark:divide-slate-700"}`}>
                  {slots.map(slot => {
                    const key = `${slot.startsAt}|${slot.endsAt}`;
                    return (
                      <tr key={key} className={`transition-colors ${isHomeLeave ? "hover:bg-emerald-100/40 dark:hover:bg-emerald-900/30" : "hover:bg-slate-50/40 dark:hover:bg-slate-700/40"}`}>
                        {/* Time */}
                        <td className={`px-2.5 sm:px-4 py-2.5 sm:py-3 text-xs tabular-nums whitespace-nowrap sticky right-0 z-10 border-r ${isHomeLeave ? "text-emerald-600 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-950/30 border-emerald-100 dark:border-emerald-800" : "text-slate-500 dark:text-slate-400 bg-white dark:bg-slate-800 border-slate-100 dark:border-slate-700"}`}>
                          {formatShiftTime(slot.startsAt, slot.endsAt, locale, timezoneId)}
                        </td>
                        {/* Person columns */}
                        {personCols.map(i => {
                          const name = slot.people[i];
                          const personId = slot.personIds[i];
                          const isCurrentUser = name === currentUserName;
                          return (
                            <td key={i} className={`px-2.5 sm:px-4 py-2.5 sm:py-3 text-center ${isCurrentUser && !isHomeLeave ? "bg-sky-50/60 dark:bg-sky-900/20" : ""} ${isCurrentUser && isHomeLeave ? "bg-emerald-100/60 dark:bg-emerald-900/30" : ""}`}>
                              {name ? (
                                <div className="flex flex-col items-center gap-0.5">
                                  <div className="flex items-center justify-center gap-1.5 group">
                                    <span
                                      className={`text-xs sm:text-sm font-medium ${isHomeLeave ? "text-emerald-800 dark:text-emerald-200" : isCurrentUser ? "text-sky-700 dark:text-sky-300" : "text-slate-800 dark:text-slate-200"}`}
                                      style={
                                        personId && roleColorMap?.get(personId)
                                          ? { borderLeft: `3px solid ${roleColorMap.get(personId)}`, paddingLeft: '6px' }
                                          : undefined
                                      }
                                    >
                                      {name}
                                    </span>
                                    {isAdmin && spaceId && personId && !isHomeLeave && (
                                      <button
                                        onClick={() => setCantMakeIt({ personId, personName: name })}
                                        title="Can't make it"
                                        className="opacity-0 group-hover:opacity-100 transition-opacity flex-shrink-0 text-amber-500 hover:text-red-500"
                                      >
                                        <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                                          <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
                                        </svg>
                                      </button>
                                    )}
                                  </div>
                                </div>
                              ) : (
                                <span className={isHomeLeave ? "text-emerald-300" : "text-slate-300"}>—</span>
                              )}
                            </td>
                          );
                        })}
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        );
      })}
      </div>

      {/* Can't make it modal */}
      {cantMakeIt && spaceId && (
        <CantMakeItModal
          open={true}
          onClose={() => setCantMakeIt(null)}
          spaceId={spaceId}
          personId={cantMakeIt.personId}
          personName={cantMakeIt.personName}
          onSaved={(triggerRerun) => {
            setCantMakeIt(null);
            onPersonBlocked?.(cantMakeIt.personId, triggerRerun);
          }}
        />
      )}
    </>
  );
}
