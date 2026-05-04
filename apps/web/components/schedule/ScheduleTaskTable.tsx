"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import CantMakeItModal from "./CantMakeItModal";

/** Minimal assignment shape needed by this component */
export interface TaskAssignment {
  id?: string;
  personId: string;
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
  /** Called after a presence window is saved — parent decides whether to re-run solver */
  onPersonBlocked?: (personId: string, triggerRerun: boolean) => void;
}

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
}

function overlapsDate(a: TaskAssignment, dateStr: string): boolean {
  const dayStart = new Date(dateStr + "T00:00:00").getTime();
  const dayEnd   = new Date(dateStr + "T23:59:59").getTime();
  const slotStart = new Date(a.slotStartsAt).getTime();
  const slotEnd   = new Date(a.slotEndsAt).getTime();
  if (isNaN(slotStart) || isNaN(slotEnd)) return false;
  return slotStart <= dayEnd && slotEnd >= dayStart;
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
export default function ScheduleTaskTable({ assignments, currentUserName, filterDate, isAdmin, spaceId, onPersonBlocked }: Props) {
  const t = useTranslations("schedule");
  const [cantMakeIt, setCantMakeIt] = useState<{ personId: string; personName: string } | null>(null);
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

  // Group assignments by task type
  const byTask = new Map<string, TaskAssignment[]>();
  for (const a of visible) {
    const list = byTask.get(a.taskTypeName) ?? [];
    list.push(a);
    byTask.set(a.taskTypeName, list);
  }

  const taskNames = Array.from(byTask.keys()).sort();

  return (
    <div className="space-y-6">
      {taskNames.map(taskName => {
        const taskAssignments = byTask.get(taskName)!;

        // Group by slot key → list of people assigned to that slot
        const slotMap = new Map<string, { startsAt: string; endsAt: string; people: string[]; personIds: string[] }>();
        for (const a of taskAssignments) {
          const key = `${a.slotStartsAt}|${a.slotEndsAt}`;
          const slot = slotMap.get(key) ?? { startsAt: a.slotStartsAt, endsAt: a.slotEndsAt, people: [], personIds: [] };
          slot.people.push(a.personName);
          slot.personIds.push(a.personId);
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
              <span className="text-sm font-semibold text-slate-800">{taskName}</span>
              <span className="text-xs text-slate-400 bg-slate-100 px-2 py-0.5 rounded-full">
                {slots.length} {t("shifts")}
              </span>
            </div>

            <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
              <table className="w-full text-sm border-collapse">
                <thead>
                  <tr className="border-b border-slate-100 bg-slate-50/80">
                    <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider whitespace-nowrap sticky right-0 bg-slate-50/80 z-10">
                      {t("time")}
                    </th>
                    {personCols.map(i => (
                      <th key={i} className="px-4 py-3 text-center text-xs font-semibold text-slate-500 uppercase tracking-wider">
                        {maxPeople === 1 ? t("assignee") : t("assigneeN", { n: i + 1 })}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {slots.map(slot => {
                    const key = `${slot.startsAt}|${slot.endsAt}`;
                    return (
                      <tr key={key} className="hover:bg-slate-50/40 transition-colors">
                        {/* Time */}
                        <td className="px-4 py-3 text-xs tabular-nums text-slate-500 whitespace-nowrap sticky right-0 bg-white z-10 border-r border-slate-100">
                          {formatTime(slot.startsAt)}
                          <span className="mx-1 text-slate-300">–</span>
                          {formatTime(slot.endsAt)}
                        </td>
                        {/* Person columns */}
                        {personCols.map(i => {
                          const name = slot.people[i];
                          const personId = slot.personIds[i];
                          const isCurrentUser = name === currentUserName;
                          return (
                            <td key={i} className={`px-4 py-3 text-center ${isCurrentUser ? "bg-blue-50/60" : ""}`}>
                              {name ? (
                                <div className="flex items-center justify-center gap-1.5 group">
                                  <span className={`text-sm font-medium ${isCurrentUser ? "text-blue-700" : "text-slate-800"}`}>
                                    {name}
                                  </span>
                                  {isAdmin && spaceId && personId && (
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
                              ) : (
                                <span className="text-slate-300">—</span>
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
  );
}
