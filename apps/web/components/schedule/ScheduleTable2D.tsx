"use client";

import { useTranslations } from "next-intl";
import type { ScheduleAssignment } from "@/app/groups/[groupId]/types";

/** Minimal shape required by the table — compatible with both ScheduleAssignment and AssignmentDto */
type TableAssignment = Pick<ScheduleAssignment, "personName" | "taskTypeName" | "slotStartsAt" | "slotEndsAt">;

interface ScheduleTable2DProps {
  assignments: TableAssignment[];
  currentUserName?: string;
  filterDate?: string;
  /** Called when a cell is clicked — passes slotKey and taskName */
  onCellClick?: (slotKey: string, taskName: string, assignees: string[]) => void;
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

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
}

export default function ScheduleTable2D({
  assignments,
  currentUserName,
  filterDate,
  onCellClick,
}: ScheduleTable2DProps) {
  const t = useTranslations("schedule");
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

  // ── Build cell map: slotKey → taskName → [personName, ...] ────────────────
  const cellMap = new Map<SlotKey, Map<string, string[]>>();
  for (const [key] of slots) cellMap.set(key, new Map());
  for (const a of visible) {
    const key: SlotKey = `${a.slotStartsAt}|${a.slotEndsAt}`;
    const taskCell = cellMap.get(key);
    if (!taskCell) continue;
    const names = taskCell.get(a.taskTypeName) ?? [];
    names.push(a.personName);
    taskCell.set(a.taskTypeName, names);
  }

  // ── Determine which task column belongs to the current user ───────────────
  const currentUserTaskName = currentUserName
    ? visible.find(a => a.personName === currentUserName)?.taskTypeName
    : undefined;

  return (
    <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
      <table className="w-full text-sm border-collapse">
        <thead>
          <tr className="border-b border-slate-100 bg-slate-50/80">
            {/* Row header column */}
            <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider whitespace-nowrap sticky right-0 bg-slate-50/80 z-10">
              {t("time")}
            </th>
            {taskNames.map(task => (
              <th
                key={task}
                className={`px-4 py-3 text-center text-xs font-semibold text-slate-700 uppercase tracking-wider whitespace-nowrap ${
                  task === currentUserTaskName ? "bg-blue-50" : ""
                }`}
              >
                {task}
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
                <td className="px-4 py-3 text-xs tabular-nums text-slate-500 whitespace-nowrap sticky right-0 bg-white z-10 border-r border-slate-100">
                  {formatTime(startsAt)}
                  <span className="mx-1 text-slate-300">–</span>
                  {formatTime(endsAt)}
                </td>
                {taskNames.map(task => {
                  const names = taskCell.get(task) ?? [];
                  const isUserTask = task === currentUserTaskName;
                  const isClickable = !!onCellClick;
                  return (
                    <td
                      key={task}
                      onClick={isClickable ? () => onCellClick(key, task, names) : undefined}
                      className={[
                        "px-4 py-3 text-center align-top",
                        isUserTask ? "bg-blue-50/60" : "",
                        isClickable ? "cursor-pointer hover:bg-blue-50 transition-colors" : "",
                      ].filter(Boolean).join(" ")}
                    >
                      {names.length === 0 ? (
                        <span className="text-slate-300">—</span>
                      ) : (
                        <div className="space-y-0.5">
                          {names.map((name, i) => (
                            <div
                              key={i}
                              className={`text-sm font-medium ${
                                name === currentUserName
                                  ? "text-blue-700"
                                  : "text-slate-800"
                              }`}
                            >
                              {name}
                            </div>
                          ))}
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
