"use client";

import { useState } from "react";
import { useDateFormat } from "@/lib/hooks/useDateFormat";
import type { ScheduleAssignment } from "../types";

interface DraftVersion { id: string; status: string; summaryJson?: string | null; }

interface Props {
  groupId: string;
  solverHorizonDays: number;
  scheduleData: ScheduleAssignment[] | null;
  scheduleLoading: boolean;
  scheduleError: string | null;
  draftVersion: DraftVersion | null;
  lastRunSummary: string | null;
  isAdmin: boolean;
  publishSaving: boolean;
  discardSaving: boolean;
  scheduleVersionError: string | null;
  onOpenDraftModal: () => void;
  onPublish: () => Promise<void>;
  onDiscard: () => Promise<void>;
}

function formatDateLabel(dateStr: string, fDateShort: (d: string) => string): string {
  const today = new Date().toISOString().split("T")[0];
  const yesterday = new Date(Date.now() - 86400000).toISOString().split("T")[0];
  const tomorrow = new Date(Date.now() + 86400000).toISOString().split("T")[0];
  if (dateStr === today) return "היום";
  if (dateStr === yesterday) return "אתמול";
  if (dateStr === tomorrow) return "מחר";
  return fDateShort(dateStr + "T00:00:00");
}

function getWeekDates(fromDate: string): string[] {
  const dates: string[] = [];
  const start = new Date(fromDate + "T00:00:00");
  start.setDate(start.getDate() - start.getDay());
  for (let i = 0; i < 7; i++) {
    const d = new Date(start);
    d.setDate(start.getDate() + i);
    dates.push(d.toISOString().split("T")[0]);
  }
  return dates;
}

/** An assignment is visible on a date if the task window overlaps that day */
function overlapsDate(a: ScheduleAssignment, dateStr: string): boolean {
  const dayStart = new Date(dateStr + "T00:00:00").getTime();
  const dayEnd   = new Date(dateStr + "T23:59:59").getTime();
  const slotStart = new Date(a.slotStartsAt).getTime();
  const slotEnd   = new Date(a.slotEndsAt).getTime();
  if (isNaN(slotStart) || isNaN(slotEnd)) return false;
  return slotStart <= dayEnd && slotEnd >= dayStart;
}

export default function ScheduleTab({
  solverHorizonDays, scheduleData, scheduleLoading, scheduleError,
  draftVersion, lastRunSummary, isAdmin, publishSaving, discardSaving, scheduleVersionError,
  onOpenDraftModal, onPublish, onDiscard,
}: Props) {
  const today = new Date().toISOString().split("T")[0];
  const minDate = new Date(Date.now() - 2 * 86400000).toISOString().split("T")[0];
  const maxDate = new Date(Date.now() + solverHorizonDays * 86400000).toISOString().split("T")[0];

  const [scheduleDate, setScheduleDate] = useState(today);
  const [scheduleView, setScheduleView] = useState<"day" | "week">("day");
  const [personFilter, setPersonFilter] = useState("");
  const [showDiscardConfirm, setShowDiscardConfirm] = useState(false);

  const { fTime, fDateShort } = useDateFormat();

  function prevDay() {
    const d = new Date(scheduleDate + "T00:00:00");
    d.setDate(d.getDate() - 1);
    const next = d.toISOString().split("T")[0];
    if (next >= minDate) setScheduleDate(next);
  }

  function nextDay() {
    const d = new Date(scheduleDate + "T00:00:00");
    d.setDate(d.getDate() + 1);
    const next = d.toISOString().split("T")[0];
    if (next <= maxDate) setScheduleDate(next);
  }

  const filtered = (scheduleData ?? []).filter(a =>
    !personFilter || a.personName.toLowerCase().includes(personFilter.toLowerCase())
  );

  const dayAssignments = filtered.filter(a => overlapsDate(a, scheduleDate));
  const weekDates = getWeekDates(scheduleDate);
  const weekAssignments = weekDates.reduce<Record<string, ScheduleAssignment[]>>((acc, d) => {
    acc[d] = filtered.filter(a => overlapsDate(a, d));
    return acc;
  }, {});

  return (
    <div className="space-y-4">
      {/* Infeasibility banner — admin only, shown when last solver run failed */}
      {isAdmin && !draftVersion && lastRunSummary && (() => {
        try {
          const s = JSON.parse(lastRunSummary);
          if (s.feasible === false) {
            const conflicts: { description: string }[] = s.conflict_details ?? [];
            return (
              <div className="bg-red-50 border border-red-200 rounded-2xl p-4 space-y-2">
                <div className="flex items-center gap-2">
                  <svg className="w-5 h-5 text-red-500 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
                  </svg>
                  <span className="text-sm font-semibold text-red-800">לא ניתן היה ליצור סידור</span>
                </div>
                <p className="text-sm text-red-700">הסולבר לא הצליח לבנות סידור עם האילוצים הנוכחיים.</p>
                {conflicts.length > 0 && (
                  <ul className="space-y-1">
                    {conflicts.map((c, i) => (
                      <li key={i} className="text-sm text-red-700 flex items-start gap-1.5">
                        <span className="mt-0.5 flex-shrink-0">•</span>
                        <span>{c.description}</span>
                      </li>
                    ))}
                  </ul>
                )}
                <p className="text-xs text-red-600 mt-1">
                  ניתן לפתור על ידי: הוספת חברים נוספים, הרחבת אופק הזמן, או הקלת אילוצים.
                </p>
              </div>
            );
          }
        } catch { /* ignore */ }
        return null;
      })()}

      {/* Draft banner — admin only */}
      {isAdmin && draftVersion && (        <div className="bg-amber-50 border border-amber-200 rounded-2xl p-4">
          <div className="flex items-center justify-between gap-3">
            <div className="flex items-center gap-2">
              <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-amber-100 text-amber-800 border border-amber-300">טיוטה</span>
              <span className="text-sm text-amber-800">סידור טיוטה מוכן לעיון ופרסום</span>
            </div>
            <div className="flex items-center gap-2 flex-shrink-0">
              <button onClick={onOpenDraftModal} className="text-xs text-amber-800 border border-amber-300 hover:bg-amber-100 px-3 py-1.5 rounded-lg transition-colors font-medium">
                👁 צפה בטיוטה
              </button>
              {isAdmin && (
                <>
                  <button onClick={onPublish} disabled={publishSaving || discardSaving} className="bg-emerald-500 hover:bg-emerald-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors">
                    {publishSaving ? "מפרסם..." : "פרסם סידור"}
                  </button>
                  <button onClick={() => setShowDiscardConfirm(true)} disabled={publishSaving || discardSaving} className="text-xs text-red-600 border border-red-200 hover:bg-red-50 px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors">
                    בטל טיוטה
                  </button>
                </>
              )}
            </div>
          </div>
          {scheduleVersionError && <p className="text-xs text-red-600 mt-2">{scheduleVersionError}</p>}
          {showDiscardConfirm && (
            <div className="mt-3 bg-red-50 border border-red-200 rounded-xl p-3 space-y-2">
              <p className="text-sm text-red-700">האם אתה בטוח שברצונך לבטל את הטיוטה? פעולה זו אינה הפיכה.</p>
              <div className="flex gap-2">
                <button onClick={() => { onDiscard(); setShowDiscardConfirm(false); }} disabled={discardSaving} className="bg-red-500 hover:bg-red-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors">
                  {discardSaving ? "מבטל..." : "כן, בטל טיוטה"}
                </button>
                <button onClick={() => setShowDiscardConfirm(false)} className="text-xs text-slate-500 hover:text-slate-700 px-2">ביטול</button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Filter */}
      <div className="relative max-w-xs">
        <input type="text" value={personFilter} onChange={e => setPersonFilter(e.target.value)} placeholder="סנן לפי שם..." className="w-full border border-slate-200 rounded-xl px-3.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 pr-9" />
        <svg className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
        </svg>
      </div>

      {/* Date nav */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <button onClick={prevDay} disabled={scheduleDate <= minDate} className="p-2 rounded-lg border border-slate-200 hover:bg-slate-50 disabled:opacity-40 transition-colors">
            <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" /></svg>
          </button>
          <button onClick={() => setScheduleDate(today)} className={`px-3 py-1.5 rounded-lg text-sm font-medium border transition-colors ${scheduleDate === today ? "bg-blue-500 text-white border-blue-500" : "border-slate-200 text-slate-600 hover:bg-slate-50"}`}>היום</button>
          <button onClick={nextDay} disabled={scheduleDate >= maxDate} className="p-2 rounded-lg border border-slate-200 hover:bg-slate-50 disabled:opacity-40 transition-colors">
            <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" /></svg>
          </button>
          <span className="text-sm font-medium text-slate-700 mr-2">{formatDateLabel(scheduleDate, fDateShort)}</span>
        </div>
        <div className="flex gap-1 bg-slate-100 p-1 rounded-lg">
          {(["day", "week"] as const).map(v => (
            <button key={v} onClick={() => setScheduleView(v)} className={`px-3 py-1 rounded-md text-xs font-medium transition-all ${scheduleView === v ? "bg-white text-slate-900 shadow-sm" : "text-slate-500"}`}>
              {v === "day" ? "יום" : "שבוע"}
            </button>
          ))}
        </div>
      </div>

      {scheduleLoading && (
        <div className="flex items-center gap-3 text-slate-400 text-sm py-8">
          <svg className="animate-spin h-5 w-5 text-blue-400" fill="none" viewBox="0 0 24 24"><circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" /><path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" /></svg>
          טוען...
        </div>
      )}
      {scheduleError && <p className="text-sm text-red-600 py-4">{scheduleError}</p>}

      {/* Day view */}
      {!scheduleLoading && !scheduleError && scheduleView === "day" && (
        dayAssignments.length === 0 ? (
          <p className="text-sm text-slate-400 py-8 text-center">אין משימות ב{formatDateLabel(scheduleDate, fDateShort)}</p>
        ) : (
          <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-100 bg-slate-50/80">
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">שם</th>
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">משימה</th>
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">שעות</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {dayAssignments.map((a, i) => (
                  <tr key={i} className="hover:bg-slate-50/60">
                    <td className="px-4 py-3.5 font-medium text-slate-900">{a.personName}</td>
                    <td className="px-4 py-3.5 text-slate-600">{a.taskTypeName}</td>
                    <td className="px-4 py-3.5 text-slate-500 text-xs tabular-nums">
                      {fTime(a.slotStartsAt)}<span className="mx-1 text-slate-300">–</span>{fTime(a.slotEndsAt)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )
      )}

      {/* Week view */}
      {!scheduleLoading && !scheduleError && scheduleView === "week" && (
        <div className="space-y-4">
          {weekDates.map(d => {
            const items = weekAssignments[d] ?? [];
            return (
              <div key={d}>
                <h3 className={`text-xs font-semibold uppercase tracking-wider mb-2 ${d === today ? "text-blue-600" : "text-slate-500"}`}>
                  {formatDateLabel(d, fDateShort)}
                  {d === today && <span className="mr-2 text-blue-500 normal-case font-normal">• היום</span>}
                </h3>
                {items.length === 0 ? (
                  <p className="text-xs text-slate-400 py-2 pr-2">אין משימות</p>
                ) : (
                  <div className="space-y-1.5">
                    {items.map((a, i) => (
                      <div key={i} className="flex items-center gap-3 bg-white border border-slate-200 rounded-xl px-4 py-2.5">
                        <div className="text-xs tabular-nums text-slate-500 w-20 shrink-0">
                          {fTime(a.slotStartsAt)}<span className="mx-1 text-slate-300">–</span>{fTime(a.slotEndsAt)}
                        </div>
                        <div className="flex-1 min-w-0">
                          <p className="text-sm font-medium text-slate-900 truncate">{a.personName}</p>
                          <p className="text-xs text-slate-400 truncate">{a.taskTypeName}</p>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
