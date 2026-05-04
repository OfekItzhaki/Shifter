"use client";

import { useState } from "react";
import { useTranslations, useLocale } from "next-intl";
import { useDateFormat } from "@/lib/hooks/useDateFormat";
import type { ScheduleAssignment } from "../types";
import ScheduleTaskTable from "@/components/schedule/ScheduleTaskTable";

interface DraftVersion { id: string; status: string; summaryJson?: string | null; }

interface Props {
  groupId: string;
  solverHorizonDays: number;
  scheduleData: ScheduleAssignment[] | null;
  scheduleLoading: boolean;
  scheduleError: string | null;
  scheduleIsOffline?: boolean;
  draftVersion: DraftVersion | null;
  lastRunSummary: string | null;
  isAdmin: boolean;
  publishSaving: boolean;
  discardSaving: boolean;
  scheduleVersionError: string | null;
  currentUserName?: string;
  groupName?: string;
  onOpenDraftModal: () => void;
  onPublish: () => Promise<void>;
  onDiscard: () => Promise<void>;
}

const DAY_NAMES_SHORT = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
const DAY_NAMES_HE   = ["א׳", "ב׳", "ג׳", "ד׳", "ה׳", "ו׳", "ש׳"];

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
  solverHorizonDays, scheduleData, scheduleLoading, scheduleError, scheduleIsOffline = false,
  draftVersion, lastRunSummary, isAdmin, publishSaving, discardSaving, scheduleVersionError,
  currentUserName, groupName,
  onOpenDraftModal, onPublish, onDiscard,
}: Props) {
  const t = useTranslations("groups.schedule_tab");
  const tCommon = useTranslations("common");
  const tAdmin = useTranslations("admin");
  const locale = useLocale();
  const dayNames = locale === "he" ? DAY_NAMES_HE : DAY_NAMES_SHORT;
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

  // Data is already group-scoped from the API — just apply the optional text search filter
  const filtered = (scheduleData ?? []).filter(a => {
    if (personFilter && !a.personName.toLowerCase().includes(personFilter.toLowerCase())) return false;
    return true;
  });

  function exportCSV() {
    if (!scheduleData || scheduleData.length === 0) return;

    // Group by task type, then by date within each task
    const byTask = new Map<string, typeof scheduleData>();
    for (const a of scheduleData) {
      const list = byTask.get(a.taskTypeName) ?? [];
      list.push(a);
      byTask.set(a.taskTypeName, list);
    }

    const rows: string[][] = [];
    const taskNames = Array.from(byTask.keys()).sort();

    for (const taskName of taskNames) {
      const taskAssignments = byTask.get(taskName)!;

      // Task header
      rows.push([taskName]);
      rows.push(["Date", "Start Time", "End Time", "Assignee 1", "Assignee 2", "Assignee 3"]);

      // Group by slot key
      const slotMap = new Map<string, { date: string; startTime: string; endTime: string; people: string[] }>();
      for (const a of taskAssignments) {
        const key = `${a.slotStartsAt}|${a.slotEndsAt}`;
        const slot = slotMap.get(key) ?? {
          date: new Date(a.slotStartsAt).toLocaleDateString(undefined, { weekday: "long", day: "numeric", month: "long" }),
          startTime: new Date(a.slotStartsAt).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" }),
          endTime: new Date(a.slotEndsAt).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" }),
          people: [],
        };
        slot.people.push(a.personName);
        slotMap.set(key, slot);
      }

      const slots = Array.from(slotMap.values())
        .sort((a, b) => a.date.localeCompare(b.date) || a.startTime.localeCompare(b.startTime));

      let lastDate = "";
      for (const slot of slots) {
        const dateCell = slot.date !== lastDate ? slot.date : "";
        lastDate = slot.date;
        rows.push([
          dateCell,
          slot.startTime,
          slot.endTime,
          slot.people[0] ?? "",
          slot.people[1] ?? "",
          slot.people[2] ?? "",
        ]);
      }

      // Empty separator row between tasks
      rows.push([]);
    }

    const csv = rows.map(r => r.map(c => `"${c}"`).join(",")).join("\n");
    const blob = new Blob(["\uFEFF" + csv], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `schedule-${groupName ?? "group"}-${new Date().toISOString().split("T")[0]}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

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
                  <span className="text-sm font-semibold text-red-800">{t("infeasible")}</span>
                </div>
                <p className="text-sm text-red-700">{t("infeasibleDesc")}</p>
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
                  {t("infeasibleSolution")}
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
              <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-amber-100 text-amber-800 border border-amber-300">{t("draftBadge")}</span>
              <span className="text-sm text-amber-800">{t("draftReady")}</span>
            </div>
            <div className="flex items-center gap-2 flex-shrink-0">
              <button onClick={onOpenDraftModal} className="text-xs text-amber-800 border border-amber-300 hover:bg-amber-100 px-3 py-1.5 rounded-lg transition-colors font-medium">
                {t("viewDraft")}
              </button>
              <button onClick={onPublish} disabled={publishSaving || discardSaving} className="bg-emerald-500 hover:bg-emerald-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors">
                {publishSaving ? t("publishing") : t("publishSchedule")}
              </button>
              <button onClick={() => setShowDiscardConfirm(true)} disabled={publishSaving || discardSaving} className="text-xs text-red-600 border border-red-200 hover:bg-red-50 px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors">
                {t("cancelDraft")}
              </button>
            </div>
          </div>
          {scheduleVersionError && <p className="text-xs text-red-600 mt-2">{scheduleVersionError}</p>}
          {showDiscardConfirm && (
            <div className="mt-3 bg-red-50 border border-red-200 rounded-xl p-3 space-y-2">
              <p className="text-sm text-red-700">{t("discardConfirmText")}</p>
              <div className="flex gap-2">
                <button onClick={() => { onDiscard(); setShowDiscardConfirm(false); }} disabled={discardSaving} className="bg-red-500 hover:bg-red-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors">
                  {discardSaving ? t("discarding") : t("yesDiscard")}
                </button>
                <button onClick={() => setShowDiscardConfirm(false)} className="text-xs text-slate-500 hover:text-slate-700 px-2">{t("cancel")}</button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Search filter + export */}
      <div className="flex items-center gap-2">
        <div className="relative flex-1 max-w-xs">
          <input
            type="text"
            value={personFilter}
            onChange={e => setPersonFilter(e.target.value)}
            placeholder={t("filterByName")}
            className="w-full border border-slate-200 rounded-xl px-3.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 pr-9"
          />
          <svg className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>
        {scheduleData && scheduleData.length > 0 && (
          <button
            onClick={exportCSV}
            className="flex items-center gap-1.5 text-xs text-slate-600 border border-slate-200 bg-white hover:bg-slate-50 px-3 py-2 rounded-xl transition-colors flex-shrink-0"
            title={t("exportCsv") ?? "Export CSV"}
          >
            <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
            </svg>
            {t("exportCsv")}
          </button>
        )}
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
          {t("thisWeek")}
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
        {weekDates.map((d, i) => {
          const dayNum = new Date(d + "T00:00:00").getDate();
          const isSelected = i === selectedWeekDay;
          const isToday = d === today;
          return (
            <button
              key={d}
              onClick={() => setSelectedWeekDay(i)}
              className={`flex-shrink-0 flex flex-col items-center px-3 py-2 rounded-xl text-xs font-medium transition-all min-w-[48px] ${
                isSelected
                  ? "bg-blue-500 text-white shadow-sm"
                  : isToday
                  ? "bg-blue-50 text-blue-600 border border-blue-200"
                  : "bg-slate-100 text-slate-600 hover:bg-slate-200"
              }`}
            >
              <span className={`text-[10px] font-normal mb-0.5 ${isSelected ? "text-blue-100" : "text-slate-400"}`}>
                {dayNames[i]}
              </span>
              <span className={`text-sm font-bold leading-none ${isToday && !isSelected ? "text-blue-600" : ""}`}>
                {dayNum}
              </span>
              {isToday && !isSelected && (
                <span className="mt-1 w-1 h-1 rounded-full bg-blue-400" />
              )}
            </button>
          );
        })}
      </div>

      {(scheduleLoading) && (
        <div className="flex items-center gap-3 text-slate-400 text-sm py-8">
          <svg className="animate-spin h-5 w-5 text-blue-400" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
          {tCommon("loading")}
        </div>
      )}
      {scheduleError && (
        <div className={`flex items-center gap-2 px-4 py-3 rounded-xl text-sm border ${
          scheduleIsOffline
            ? "bg-amber-50 border-amber-200 text-amber-800"
            : "bg-red-50 border-red-200 text-red-700"
        }`}>
          <svg className="w-4 h-4 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            {scheduleIsOffline
              ? <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
              : <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            }
          </svg>
          {scheduleError}
        </div>
      )}

      {/* Per-task schedule tables — show even when offline (cached data) */}
      {!scheduleLoading && (
        <>
          {/* Selected day label */}
          <div className="flex items-center gap-2">
            <span className="text-sm font-semibold text-slate-700">
              {new Date(selectedDate + "T00:00:00").toLocaleDateString(
                locale === "he" ? "he-IL" : locale === "ru" ? "ru-RU" : "en-US",
                { weekday: "long", day: "numeric", month: "long" }
              )}
            </span>
            {selectedDate === today && (
              <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold bg-blue-100 text-blue-700 border border-blue-200">
                {tAdmin("today")}
              </span>
            )}
          </div>
          <ScheduleTaskTable
            assignments={filtered}
            filterDate={selectedDate}
            currentUserName={currentUserName}
          />
        </>
      )}
    </div>
  );
}
