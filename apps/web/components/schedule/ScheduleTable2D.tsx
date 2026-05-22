"use client";

import { useTranslations } from "next-intl";
import { useAuthStore } from "@/lib/store/authStore";
import { formatLocalTime } from "@/lib/utils/formatTime";
import type { ScheduleAssignment } from "@/app/groups/[groupId]/types";
import type { TaskConfigSummaryDto } from "@/lib/api/groups";
import TaskInfoBadge from "./TaskInfoBadge";

/** Task type name used for home-leave assignments from the solver */
const HOME_LEAVE_TASK_TYPE = "home_leave";

/** Display label for home-leave assignments */
const HOME_LEAVE_LABEL = "בבית";

/** Check if a task type represents a home-leave assignment */
function isHomeLeaveTask(taskTypeName: string): boolean {
  return taskTypeName === HOME_LEAVE_TASK_TYPE;
}

/** Minimal shape required by the table — compatible with both ScheduleAssignment and AssignmentDto */
type TableAssignment = Pick<ScheduleAssignment, "personName" | "taskTypeName" | "slotStartsAt" | "slotEndsAt"> & { personId?: string };

interface ScheduleTable2DProps {
  assignments: TableAssignment[];
  currentUserName?: string;
  filterDate?: string;
  /** Optional map of personId → role hex color for visual indicators */
  roleColorMap?: Map<string, string | null>;
  /** Called when a cell is clicked — passes slotKey and taskName */
  onCellClick?: (slotKey: string, taskName: string, assignees: string[]) => void;
  /** Optional task configuration map keyed by task ID for displaying info badges */
  taskConfigurations?: Record<string, TaskConfigSummaryDto>;
}

/** Returns true if the assignment's slot overlaps the given date (YYYY-MM-DD). */
function overlapsDate(a: TableAssignment, dateStr: string): boolean {
  const dayStart = new Date(dateStr + "T00:00:00").getTime();
  const dayEnd   = new Date(dateStr + "T23:59:59").getTime();
  const slotStart = new Date(a.slotStartsAt).getTime();
  const slotEnd   = new Date(a.slotEndsAt).getTime();
  if (isNaN(slotStart) || isNaN(slotEnd)) return false;
  // Use strict > for slotEnd so a shift ending exactly at midnight (00:00:00)
  // does not bleed into the next day.
  return slotStart <= dayEnd && slotEnd > dayStart;
}

function formatTime(iso: string, timezoneId: string | null): string {
  return formatLocalTime(iso, timezoneId, "24h");
}

export default function ScheduleTable2D({
  assignments,
  currentUserName,
  filterDate,
  roleColorMap,
  onCellClick,
  taskConfigurations,
}: ScheduleTable2DProps) {
  const t = useTranslations("schedule");
  const timezoneId = useAuthStore(s => s.timezoneId);
  // Filter to the requested date if provided
  const visible = filterDate
    ? assignments.filter(a => overlapsDate(a, filterDate))
    : assignments;

  if (visible.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-center bg-white rounded-xl border border-slate-200">
        <p className="text-sm text-slate-400">{t("noTasksThisDay")}</p>
      </div>
    );
  }

  // ── Derive unique task names (columns) sorted alphabetically ──────────────
  const taskNames = Array.from(new Set(visible.map(a => a.taskTypeName))).sort();

  // ── Derive unique time slots (rows) sorted by start time ──────────────────
  type SlotKey = string; // "${startsAt}|${endsAt}"
  const slotMap = new Map<SlotKey, { startsAt: string; endsAt: string }>();
  for (const a of visible) {
    const key: SlotKey = `${a.slotStartsAt}|${a.slotEndsAt}`;
    if (!slotMap.has(key)) slotMap.set(key, { startsAt: a.slotStartsAt, endsAt: a.slotEndsAt });
  }
  const slots = Array.from(slotMap.entries())
    .sort(([, a], [, b]) => new Date(a.startsAt).getTime() - new Date(b.startsAt).getTime());

  // ── Build cell map: slotKey → taskName → [{ name, personId }, ...] ────────────────
  const cellMap = new Map<SlotKey, Map<string, { name: string; personId?: string }[]>>();
  for (const [key] of slots) cellMap.set(key, new Map());
  for (const a of visible) {
    const key: SlotKey = `${a.slotStartsAt}|${a.slotEndsAt}`;
    const taskCell = cellMap.get(key);
    if (!taskCell) continue;
    const entries = taskCell.get(a.taskTypeName) ?? [];
    entries.push({ name: a.personName, personId: a.personId });
    taskCell.set(a.taskTypeName, entries);
  }

  // ── Determine which task column belongs to the current user ───────────────
  const currentUserTaskName = currentUserName
    ? visible.find(a => a.personName === currentUserName)?.taskTypeName
    : undefined;

  return (
    <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm -mx-2 sm:mx-0">
      <table className="w-full text-sm border-collapse min-w-[320px]">
        <thead>
          <tr className="border-b border-slate-100 bg-slate-50/80">
            {/* Row header column */}
            <th className="px-2.5 sm:px-4 py-2.5 sm:py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider whitespace-nowrap sticky right-0 bg-slate-50/80 z-10">
              {t("time")}
            </th>
            {taskNames.map(task => (
              <th
                key={task}
                className={`px-2.5 sm:px-4 py-2.5 sm:py-3 text-center text-xs font-semibold uppercase tracking-wider whitespace-nowrap ${
                  isHomeLeaveTask(task) ? "text-emerald-700 bg-emerald-50" : task === currentUserTaskName ? "bg-sky-50 text-slate-700" : "text-slate-700"
                }`}
              >
                <span className="inline-flex items-center gap-1">
                  {isHomeLeaveTask(task) ? HOME_LEAVE_LABEL : task}
                  {!isHomeLeaveTask(task) && (
                    <TaskInfoBadge config={taskConfigurations?.[task] ?? null} />
                  )}
                </span>
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {slots.map(([key, { startsAt, endsAt }]) => {
            const taskCell = cellMap.get(key)!;
            return (
              <tr key={key} className="hover:bg-slate-50/40 transition-colors">
                {/* Time slot row header */}
                <td className="px-2.5 sm:px-4 py-2.5 sm:py-3 text-xs tabular-nums text-slate-500 whitespace-nowrap sticky right-0 bg-white z-10 border-r border-slate-100">
                  {formatTime(startsAt, timezoneId)}
                  <span className="mx-1 text-slate-300">–</span>
                  {formatTime(endsAt, timezoneId)}
                </td>
                {taskNames.map(task => {
                  const entries = taskCell.get(task) ?? [];
                  const isUserTask = task === currentUserTaskName;
                  const isHomeLeave = isHomeLeaveTask(task);
                  const isClickable = !!onCellClick;
                  return (
                    <td
                      key={task}
                      onClick={isClickable ? () => onCellClick(key, task, entries.map(e => e.name)) : undefined}
                      className={[
                        "px-2.5 sm:px-4 py-2.5 sm:py-3 text-center align-top",
                        isHomeLeave ? "bg-emerald-50/60" : isUserTask ? "bg-sky-50/60" : "",
                        isClickable ? "cursor-pointer hover:bg-sky-50 transition-colors" : "",
                      ].filter(Boolean).join(" ")}
                    >
                      {entries.length === 0 ? (
                        <span className={isHomeLeave ? "text-emerald-300" : "text-slate-300"}>—</span>
                      ) : (
                        <div className="space-y-0.5">
                          {entries.map((entry, i) => {
                            const roleColor = entry.personId ? roleColorMap?.get(entry.personId) : undefined;
                            return (
                              <div
                                key={i}
                                className={`text-sm font-medium ${
                                  isHomeLeave
                                    ? "text-emerald-700"
                                    : entry.name === currentUserName
                                    ? "text-sky-700"
                                    : "text-slate-800"
                                }`}
                                style={
                                  roleColor
                                    ? { borderLeft: `3px solid ${roleColor}`, paddingLeft: '6px' }
                                    : undefined
                                }
                              >
                                {entry.name}
                              </div>
                            );
                          })}
                        </div>
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
  );
}
