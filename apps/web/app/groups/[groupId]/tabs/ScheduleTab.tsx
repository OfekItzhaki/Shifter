"use client";

import { useState } from "react";
import { useDateFormat } from "@/lib/hooks/useDateFormat";
import type { ScheduleAssignment } from "../types";
import ScheduleTable2D from "@/components/schedule/ScheduleTable2D";

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
  currentUserName?: string;
  /** Set of personIds belonging to this group — used to filter the space-wide schedule */
  memberIds?: Set<string>;
  membersLoading?: boolean;
  onOpenDraftModal: () => void;
  onPublish: () => Promise<void>;
  onDiscard: () => Promise<void>;
}

const DAY_NAMES = ["ראשון", "שני", "שלישי", "רביעי", "חמישי", "שישי", "שבת"];

function getWeekDates(fromDate: string): string[] {
  const dates: string[] = [];
  const start = new Date(fromDate + "T00:00:00");
  start.setDate(start.getDate() - start.getDay()); // go to Sunday
  for (let i = 0; i < 7; i++) {
    const d = new Date(start);
    d.setDate(start.getDate() + i);
    dates.push(d.toISOString().split("T")[0]);
  }
  return dates;
}

export default function ScheduleTab({
  solverHorizonDays, scheduleData, scheduleLoading, scheduleError,
  draftVersion, lastRunSummary, isAdmin, publishSaving, discardSaving, scheduleVersionError,
  currentUserName, memberIds, membersLoading,
  onOpenDraftModal, onPublish, onDiscard,
}: Props) {
  const today = new Date().toISOString().split("T")[0];
  const minDate = new Date(Date.now() - 2 * 86400000).toISOString().split("T")[0];
  const maxDate = new Date(Date.now() + solverHorizonDays * 86400000).toISOString().split("T")[0];

  // Week navigation — which week to show (anchored to a date in that week)
  const [weekAnchor, setWeekAnchor] = useState(today);
  // Which day tab is selected within the week (0 = Sunday … 6 = Saturday)
  const [selectedWeekDay, setSelectedWeekDay] = useState(new Date().getDay());
  const [personFilter, setPersonFilter] = useState("");
  const [showDiscardConfirm, setShowDiscardConfirm] = useState(false);

  const { fDateShort } = useDateFormat();

  const weekDates = getWeekDates(weekAnchor);
  const selectedDate = weekDates[selectedWeekDay] ?? weekDates[0];

  function prevWeek() {
    const d = new Date(weekAnchor + "T00:00:00");
    d.setDate(d.getDate() - 7);
    const next = d.toISOString().split("T")[0];
    if (next >= minDate) setWeekAnchor(next);
  }

  function nextWeek() {
    const d = new Date(weekAnchor + "T00:00:00");
    d.setDate(d.getDate() + 7);
    const next = d.toISOString().split("T")[0];
    if (next <= maxDate) setWeekAnchor(next);
  }

  function goToToday() {
    setWeekAnchor(today);
    setSelectedWeekDay(new Date().getDay());
  }

  // Only filter once members have loaded — if memberIds is undefined or empty
  // because members haven't loaded yet, show nothing rather than everything.
  const filtered = (scheduleData ?? []).filter(a => {
    // If members are still loading or memberIds not yet available, hide all
    if (!memberIds || memberIds.size === 0) return false;
    // Filter to this group's members only (by personId — reliable, not name-based)
    if (!memberIds.has(a.personId)) return false;
    // Optional text search filter
    if (personFilter && !a.personName.toLowerCase().includes(personFilter.toLowerCase())) return false;
    return true;
  });

  // Week range label e.g. "12–18 ינואר"
  const weekStart = weekDates[0];
  const weekEnd = weekDates[6];
  const weekLabel = weekStart && weekEnd
    ? `${fDateShort(weekStart + "T00:00:00")} – ${fDateShort(weekEnd + "T00:00:00")}`
    : "";

  return (
    <div className="space-y-4">
      {/* Infeasibility banner — admin only */}
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
      {isAdmin && draftVersion && (
        <div className="bg-amber-50 border border-amber-200 rounded-2xl p-4">
          <div className="flex items-center justify-between gap-3">
            <div className="flex items-center gap-2">
              <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-amber-100 text-amber-800 border border-amber-300">טיוטה</span>
              <span className="text-sm text-amber-800">סידור טיוטה מוכן לעיון ופרסום</span>
            </div>
            <div className="flex items-center gap-2 flex-shrink-0">
              <button onClick={onOpenDraftModal} className="text-xs text-amber-800 border border-amber-300 hover:bg-amber-100 px-3 py-1.5 rounded-lg transition-colors font-medium">
                👁 צפה בטיוטה
              </button>
              <button onClick={onPublish} disabled={publishSaving || discardSaving} className="bg-emerald-500 hover:bg-emerald-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors">
                {publishSaving ? "מפרסם..." : "פרסם סידור"}
              </button>
              <button onClick={() => setShowDiscardConfirm(true)} disabled={publishSaving || discardSaving} className="text-xs text-red-600 border border-red-200 hover:bg-red-50 px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors">
                בטל טיוטה
              </button>
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

      {/* Search filter */}
      <div className="relative max-w-xs">
        <input
          type="text"
          value={personFilter}
          onChange={e => setPersonFilter(e.target.value)}
          placeholder="סנן לפי שם..."
          className="w-full border border-slate-200 rounded-xl px-3.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 pr-9"
        />
        <svg className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
        </svg>
      </div>

      {/* Week navigation */}
      <div className="flex items-center gap-2">
        <button onClick={prevWeek} className="p-2 rounded-lg border border-slate-200 hover:bg-slate-50 transition-colors">
          <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
          </svg>
        </button>
        <button
          onClick={goToToday}
          className={`px-3 py-1.5 rounded-lg text-sm font-medium border transition-colors ${
            weekDates.includes(today) ? "bg-blue-500 text-white border-blue-500" : "border-slate-200 text-slate-600 hover:bg-slate-50"
          }`}
        >
          השבוע
        </button>
        <button onClick={nextWeek} className="p-2 rounded-lg border border-slate-200 hover:bg-slate-50 transition-colors">
          <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
          </svg>
        </button>
        <span className="text-sm text-slate-500 mr-1">{weekLabel}</span>
      </div>

      {/* Day-name tabs */}
      <div className="flex gap-1 overflow-x-auto pb-1">
        {weekDates.map((d, i) => (
          <button
            key={d}
            onClick={() => setSelectedWeekDay(i)}
            className={`flex-shrink-0 px-3 py-1.5 rounded-lg text-xs font-medium transition-all ${
              i === selectedWeekDay
                ? "bg-blue-500 text-white shadow-sm"
                : d === today
                ? "bg-blue-50 text-blue-600 border border-blue-200"
                : "bg-slate-100 text-slate-600 hover:bg-slate-200"
            }`}
          >
            {DAY_NAMES[i]}
            {d === today && i !== selectedWeekDay && (
              <span className="mr-1 text-blue-400">•</span>
            )}
          </button>
        ))}
      </div>

      {(scheduleLoading || membersLoading) && (
        <div className="flex items-center gap-3 text-slate-400 text-sm py-8">
          <svg className="animate-spin h-5 w-5 text-blue-400" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
          טוען...
        </div>
      )}
      {scheduleError && <p className="text-sm text-red-600 py-4">{scheduleError}</p>}

      {/* 2D schedule table for selected day */}
      {!scheduleLoading && !membersLoading && !scheduleError && (
        <ScheduleTable2D
          assignments={filtered}
          filterDate={selectedDate}
          currentUserName={currentUserName}
        />
      )}
    </div>
  );
}
