"use client";

import { useState, useEffect } from "react";
import { useTranslations, useLocale } from "next-intl";
import Link from "next/link";
import { useDateFormat } from "@/lib/hooks/useDateFormat";
import { useAuthStore } from "@/lib/store/authStore";
import { formatLocalTime, formatLocalDate, getLocalToday } from "@/lib/utils/formatTime";
import type { ScheduleAssignment } from "../types";
import ScheduleTaskTable from "@/components/schedule/ScheduleTaskTable";
import ScheduleDiffView from "@/components/schedule/ScheduleDiffView";
import ScheduleHistory from "@/components/schedule/ScheduleHistory";
import RecommendationBanner from "@/components/recommendations/RecommendationBanner";
import { getHistoricalSchedule } from "@/lib/api/stats";
import { openFeedbackModal } from "@/components/shell/FeedbackFab";

interface DraftVersion { id: string; status: string; summaryJson?: string | null; sourceRunId?: string | null; }

interface Props {
  groupId: string;
  solverHorizonDays: number;
  scheduleData: ScheduleAssignment[] | null;
  scheduleLoading: boolean;
  scheduleError: string | null;
  scheduleIsOffline?: boolean;
  draftVersion: DraftVersion | null;
  lastRunSummary: string | null;
  solverError: string | null;
  isAdmin: boolean;
  publishSaving: boolean;
  discardSaving: boolean;
  scheduleVersionError: string | null;
  currentUserName?: string;
  groupName?: string;
  spaceId?: string;
  allowMembersViewHistory?: boolean;
  subscriptionActive?: boolean;
  onOpenDraftModal: () => void;
  onPublish: () => Promise<void>;
  onDiscard: () => Promise<void>;
  onTriggerSolver?: () => void;
}

const DAY_NAMES_SHORT = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
const DAY_NAMES_HE   = ["א׳", "ב׳", "ג׳", "ד׳", "ה׳", "ו׳", "ש׳"];

function getWeekDates(fromDate: string): string[] {
  const dates: string[] = [];
  // Parse as UTC to avoid timezone shifting the date
  const parts = fromDate.split("-").map(Number);
  const start = new Date(Date.UTC(parts[0], parts[1] - 1, parts[2]));
  // Go back to Sunday (UTC)
  const day = start.getUTCDay();
  start.setUTCDate(start.getUTCDate() - day);
  for (let i = 0; i < 7; i++) {
    const d = new Date(start);
    d.setUTCDate(start.getUTCDate() + i);
    dates.push(d.toISOString().split("T")[0]);
  }
  return dates;
}

export default function ScheduleTab({
  groupId, solverHorizonDays, scheduleData, scheduleLoading, scheduleError, scheduleIsOffline = false,
  draftVersion, lastRunSummary, solverError, isAdmin, publishSaving, discardSaving, scheduleVersionError,
  currentUserName, groupName, spaceId, allowMembersViewHistory = true, subscriptionActive = true,
  onOpenDraftModal, onPublish, onDiscard, onTriggerSolver,
}: Props) {
  const t = useTranslations("groups.schedule_tab");
  const tSchedule = useTranslations("schedule");
  const tCommon = useTranslations("common");
  const tAdmin = useTranslations("admin");
  const locale = useLocale();
  const dayNames = locale === "he" ? DAY_NAMES_HE : DAY_NAMES_SHORT;
  const { fDateShort } = useDateFormat();
  const timezoneId = useAuthStore(s => s.timezoneId);
  const today = getLocalToday(timezoneId);
  const minDate = "2020-01-01"; // Allow viewing any past schedule
  const maxDate = (() => {
    // Allow viewing up to 30 days ahead (independent of solver horizon)
    const d = new Date(Date.now() + 30 * 86400000);
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
  })();

  // Week navigation — which week to show (anchored to a date in that week)
  const [weekAnchor, setWeekAnchor] = useState(today);
  // Which day tab is selected within the week (0 = Sunday … 6 = Saturday) — use UTC day
  const [selectedWeekDay, setSelectedWeekDay] = useState(() => {
    const parts = today.split("-").map(Number);
    return new Date(Date.UTC(parts[0], parts[1] - 1, parts[2])).getUTCDay();
  });
  const [personFilter, setPersonFilter] = useState("");
  const [showDiscardConfirm, setShowDiscardConfirm] = useState(false);
  const [showDiff, setShowDiff] = useState(false);
  const [showHistory, setShowHistory] = useState(false);
  const [historicalAssignments, setHistoricalAssignments] = useState<ScheduleAssignment[] | null>(null);
  const [historicalLoading, setHistoricalLoading] = useState(false);
  const [isViewingHistory, setIsViewingHistory] = useState(false);

  const weekDates = getWeekDates(weekAnchor);

  // Auto-navigate to the first day with assignments when data loads
  // This handles the case where today has no assignments but tomorrow does (timezone offset)
  useEffect(() => {
    if (!scheduleData || scheduleData.length === 0) return;
    const currentDate = weekDates[selectedWeekDay];
    // Convert UTC slot timestamps to local dates before comparing
    const hasAssignmentsToday = scheduleData.some(a => {
      const localDate = new Date(a.slotStartsAt).toLocaleDateString("sv", { timeZone: timezoneId || "Asia/Jerusalem" });
      return localDate === currentDate;
    });
    if (!hasAssignmentsToday) {
      // Find the first day in the current week that has assignments
      for (let i = 0; i < 7; i++) {
        const date = weekDates[i];
        if (scheduleData.some(a => {
          const localDate = new Date(a.slotStartsAt).toLocaleDateString("sv", { timeZone: timezoneId || "Asia/Jerusalem" });
          return localDate === date;
        })) {
          setSelectedWeekDay(i);
          return;
        }
      }
      // No assignments in this week — try next week
      const nextWeekStart = new Date(weekAnchor + "T00:00:00");
      nextWeekStart.setDate(nextWeekStart.getDate() + 7);
      const nextAnchor = `${nextWeekStart.getFullYear()}-${String(nextWeekStart.getMonth() + 1).padStart(2, "0")}-${String(nextWeekStart.getDate()).padStart(2, "0")}`;
      if (nextAnchor <= maxDate) {
        setWeekAnchor(nextAnchor);
        setSelectedWeekDay(0);
      }
    }
  }, [scheduleData]); // eslint-disable-line react-hooks/exhaustive-deps
  const selectedDate = weekDates[selectedWeekDay] ?? weekDates[0];

  // Determine if the selected week is entirely in the past
  const weekEndDate = weekDates[6];
  const isPastWeek = weekEndDate < today;

  // Fetch historical data when viewing a past week
  useEffect(() => {
    if (!isPastWeek || !spaceId || !groupId) {
      setIsViewingHistory(false);
      setHistoricalAssignments(null);
      return;
    }

    let cancelled = false;
    setHistoricalLoading(true);
    setIsViewingHistory(true);

    getHistoricalSchedule(spaceId, groupId, weekDates[0], weekDates[6])
      .then(res => {
        if (cancelled) return;
        // Map snapshot DTOs to ScheduleAssignment format
        const mapped: ScheduleAssignment[] = (res?.assignments ?? []).map(snap => ({
          id: snap.id,
          personId: snap.personId,
          personName: snap.personName || "",
          taskTypeName: snap.taskTypeName ?? snap.burdenLevel ?? "",
          slotStartsAt: snap.shiftStart ?? `${snap.snapshotDate}T00:00:00`,
          slotEndsAt: snap.shiftEnd ?? `${snap.snapshotDate}T23:59:59`,
          source: "history",
        }));
        setHistoricalAssignments(mapped);
      })
      .catch(() => {
        if (!cancelled) setHistoricalAssignments([]);
      })
      .finally(() => {
        if (!cancelled) setHistoricalLoading(false);
      });

    return () => { cancelled = true; };
  }, [isPastWeek, spaceId, groupId, weekAnchor]);

  function prevWeek() {
    const d = new Date(weekAnchor + "T00:00:00");
    d.setDate(d.getDate() - 7);
    const next = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
    // If history viewing is disabled for non-admins, prevent navigating to past weeks
    if (!allowMembersViewHistory && !isAdmin) {
      const nextWeekDates = getWeekDates(next);
      const nextWeekEnd = nextWeekDates[6];
      if (nextWeekEnd < today) return;
    }
    if (next >= minDate) setWeekAnchor(next);
  }

  function nextWeek() {
    const d = new Date(weekAnchor + "T00:00:00");
    d.setDate(d.getDate() + 7);
    const next = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
    if (next <= maxDate) {
      setWeekAnchor(next);
      // When navigating to a new week, select the first day that has assignments,
      // or Sunday (0) if none. Never stay on a day that's in the past.
      const newWeekDates = getWeekDates(next);
      const firstFutureIdx = newWeekDates.findIndex(d => d >= today);
      setSelectedWeekDay(firstFutureIdx >= 0 ? firstFutureIdx : 0);
    }
  }

  function goToToday() {
    setWeekAnchor(today);
    const parts = today.split("-").map(Number);
    setSelectedWeekDay(new Date(Date.UTC(parts[0], parts[1] - 1, parts[2])).getUTCDay());
  }

  // Data is already group-scoped from the API — just apply the optional text search filter
  // When viewing a past week, use historical data instead of live schedule
  const activeData = isViewingHistory ? (historicalAssignments ?? []) : (scheduleData ?? []);
  const filtered = activeData.filter(a => {
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
          date: formatLocalDate(a.slotStartsAt, timezoneId),
          startTime: formatLocalTime(a.slotStartsAt, timezoneId, "24h"),
          endTime: formatLocalTime(a.slotEndsAt, timezoneId, "24h"),
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

      {/* Solver error banner — shows when the last run crashed (admin only) */}
      {isAdmin && solverError && !draftVersion && (() => {
        const subscriptionKeywords = ["trial", "subscription", "expired", "שדרג", "תקופת הניסיון", "ניסיון", "מנוי", "подписк"];
        const isSubscriptionError = subscriptionKeywords.some(kw => solverError.toLowerCase().includes(kw.toLowerCase()));
        return (
          <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-2xl p-4 space-y-2">
            <div className="flex items-center gap-2">
              <svg className="w-5 h-5 text-red-500 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              <span className="text-sm font-semibold text-red-800 dark:text-red-300">{tAdmin("solverLastFailed")}</span>
            </div>
            <p className="text-sm text-red-700 dark:text-red-400">{solverError}</p>
            <p className="text-xs text-slate-600 dark:text-slate-400">{tAdmin("solverSolutions")}</p>
            {isSubscriptionError ? (
              <Link
                href="/pricing"
                className="mt-2 inline-block text-xs font-medium bg-sky-500 hover:bg-sky-600 text-white px-3 py-1.5 rounded-lg transition-colors"
              >
                {t("upgrade") || "שדרג"}
              </Link>
            ) : (
              <div className="flex items-center gap-3 mt-2">
                {onTriggerSolver && (
                  <button
                    onClick={onTriggerSolver}
                    className="text-xs font-medium text-red-700 dark:text-red-300 border border-red-300 dark:border-red-700 hover:bg-red-100 dark:hover:bg-red-900/30 px-3 py-1.5 rounded-lg transition-colors"
                  >
                    {t("runAgain") || tAdmin("runSolver")}
                  </button>
                )}
                <button
                  type="button"
                  onClick={() => openFeedbackModal({
                    type: "bug",
                    initialDescription: `[Solver Error]\n${solverError}\n\nGroup: ${groupId}`,
                  })}
                  className="text-xs text-slate-500 dark:text-slate-400 hover:text-red-600 dark:hover:text-red-400 underline underline-offset-2 transition-colors"
                >
                  {tAdmin("reportProblem")}
                </button>
              </div>
            )}
          </div>
        );
      })()}

      {/* Draft banner — admin only */}
      {isAdmin && draftVersion && (
        <div className="bg-amber-50 border border-amber-200 rounded-2xl p-3 sm:p-4">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div className="flex items-center gap-2">
              <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-amber-100 text-amber-800 border border-amber-300">{t("draftBadge")}</span>
              <span className="text-sm text-amber-800">{t("draftReady")}</span>
            </div>
            <div className="flex items-center gap-2 flex-shrink-0 flex-wrap">
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

      {/* Double-shift recommendation banner — shown when a solver run has recommendations */}
      {isAdmin && spaceId && draftVersion?.sourceRunId && (
        <RecommendationBanner
          spaceId={spaceId}
          runId={draftVersion.sourceRunId}
          groupId={groupId}
          subscriptionActive={subscriptionActive}
        />
      )}

      {/* Search filter + export */}
      <div className="flex flex-col sm:flex-row items-stretch sm:items-center gap-2">
        <div className="relative flex-1">
          <input
            type="text"
            value={personFilter}
            onChange={e => setPersonFilter(e.target.value)}
            placeholder={t("filterByName")}
            className="w-full border border-slate-200 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-200 rounded-xl px-3.5 py-2.5 sm:py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-500 pr-9"
          />
          <svg className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>
        {scheduleData && scheduleData.length > 0 && (
          <button
            onClick={exportCSV}
            className="flex items-center justify-center gap-1.5 text-xs text-slate-600 dark:text-slate-300 border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-800 hover:bg-slate-50 dark:hover:bg-slate-700 px-3 py-2.5 sm:py-2 rounded-xl transition-colors flex-shrink-0"
            title={t("exportCsv") ?? "Export CSV"}
          >
            <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
            </svg>
            {t("exportCsv")}
          </button>
        )}
        {isAdmin && scheduleData && scheduleData.length > 0 && (
          <button
            onClick={() => setShowDiff(true)}
            className="flex items-center justify-center gap-1.5 text-xs text-sky-600 border border-sky-200 bg-white hover:bg-sky-50 px-3 py-2.5 sm:py-2 rounded-xl transition-colors flex-shrink-0"
          >
            <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4" />
            </svg>
            {t("showDiff")}
          </button>
        )}
        {isAdmin && spaceId && (
          <button
            onClick={() => setShowHistory(!showHistory)}
            className="flex items-center justify-center gap-1.5 text-xs text-slate-600 dark:text-slate-300 border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-800 hover:bg-slate-50 dark:hover:bg-slate-700 px-3 py-2.5 sm:py-2 rounded-xl transition-colors flex-shrink-0"
          >
            <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            {t("history")}
          </button>
        )}
      </div>

      {/* Schedule History panel */}
      {showHistory && spaceId && (
        <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-4 sm:p-5">
          <ScheduleHistory spaceId={spaceId} onClose={() => setShowHistory(false)} />
        </div>
      )}

      {/* Week navigation */}
      <div className="sticky top-0 z-20 bg-slate-50 dark:bg-slate-900 pb-2 -mx-2 px-2 sm:mx-0 sm:px-0 space-y-3 pt-2">
      <div className="flex items-center gap-2">
        <button onClick={prevWeek} className="p-2 rounded-lg border border-slate-200 dark:border-slate-600 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors">
          <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
          </svg>
        </button>
        <button
          onClick={goToToday}
          className={`px-3 py-1.5 rounded-lg text-sm font-medium border transition-colors ${
            weekDates.includes(today) ? "bg-sky-500 text-white border-sky-500" : "border-slate-200 dark:border-slate-600 text-slate-600 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-700"
          }`}
        >
          {t("thisWeek")}
        </button>
        <button onClick={nextWeek} className="p-2 rounded-lg border border-slate-200 dark:border-slate-600 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors">
          <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
          </svg>
        </button>
        <span className="text-sm text-slate-500 mr-1">{weekLabel}</span>
      </div>

      {/* Day-name tabs */}
      <div className="flex gap-1.5 overflow-x-auto pb-1">
        {weekDates.map((d, i) => {
          const dayNum = new Date(d + "T00:00:00").getDate();
          const isSelected = i === selectedWeekDay;
          const isToday = d === today;
          return (
            <button
              key={d}
              onClick={() => setSelectedWeekDay(i)}
              className={`flex-shrink-0 flex flex-col items-center px-3 py-2.5 sm:py-2 rounded-xl text-xs font-medium transition-all min-w-[44px] sm:min-w-[48px] ${
                isSelected
                  ? "bg-sky-500 text-white shadow-sm"
                  : isToday
                  ? "bg-sky-50 text-sky-600 border border-sky-200"
                  : "bg-slate-100 text-slate-600 hover:bg-slate-200"
              }`}
            >
              <span className={`text-[10px] font-normal mb-0.5 ${isSelected ? "text-sky-100" : "text-slate-400"}`}>
                {dayNames[i]}
              </span>
              <span className={`text-sm font-bold leading-none ${isToday && !isSelected ? "text-sky-600" : ""}`}>
                {dayNum}
              </span>
              {isToday && !isSelected && (
                <span className="mt-1 w-1 h-1 rounded-full bg-sky-400" />
              )}
            </button>
          );
        })}
      </div>
      </div>

      {/* Historical view banner */}
      {isViewingHistory && !historicalLoading && (
        <div className="flex items-center gap-2 px-4 py-3 rounded-xl text-sm border bg-sky-50 border-sky-200 text-sky-800">
          <svg className="w-4 h-4 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <span>צפייה בהיסטוריה — {weekLabel}</span>
        </div>
      )}

      {(scheduleLoading || historicalLoading) && (
        <div className="flex items-center gap-3 text-slate-400 text-sm py-8">
          <svg className="animate-spin h-5 w-5 text-sky-400" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
          {tCommon("loading")}
        </div>
      )}
      {scheduleError && (
        <div className={`flex items-center gap-3 px-5 py-4 rounded-xl text-base font-semibold border-2 shadow-sm ${
          scheduleIsOffline
            ? "bg-amber-50 border-amber-300 text-amber-900"
            : "bg-red-50 border-red-300 text-red-800"
        }`}>
          <svg className="w-5 h-5 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
            {scheduleIsOffline
              ? <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
              : <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            }
          </svg>
          {scheduleError}
        </div>
      )}

      {/* Per-task schedule tables — show even when offline (cached data) */}
      {!scheduleLoading && !historicalLoading && (
        <>
          {/* Diff view — shown when admin clicks "Show Changes" */}
          {showDiff && spaceId && (
            <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-4 sm:p-5">
              <ScheduleDiffView
                spaceId={spaceId}
                currentVersionId="current"
                onClose={() => setShowDiff(false)}
              />
            </div>
          )}
          {/* Selected day label */}
          <div className="flex items-center gap-2">
            <span className="text-sm font-semibold text-slate-700 dark:text-slate-200">
              {new Date(selectedDate + "T00:00:00").toLocaleDateString(
                locale === "he" ? "he-IL" : locale === "ru" ? "ru-RU" : "en-US",
                { weekday: "long", day: "numeric", month: "long", timeZone: timezoneId || "Asia/Jerusalem" }
              )}
            </span>
            {selectedDate === today && (
              <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold bg-sky-100 text-sky-700 border border-sky-200">
                {tAdmin("today")}
              </span>
            )}
            {(() => {
              const dayShiftCount = filtered.filter(a => {
                const d = new Date(a.slotStartsAt);
                return d.toLocaleDateString("sv", { timeZone: timezoneId || "Asia/Jerusalem" }) === selectedDate;
              }).length;
              return dayShiftCount > 0 ? (
                <span className="text-xs px-2 py-0.5 rounded-full text-slate-400 bg-slate-100 dark:bg-slate-700 dark:text-slate-300">
                  {dayShiftCount} {tSchedule("shifts")}
                </span>
              ) : null;
            })()}
          </div>
          <ScheduleTaskTable
            assignments={filtered}
            filterDate={selectedDate}
            currentUserName={currentUserName}
            isAdmin={isAdmin}
            spaceId={spaceId}
            onPersonBlocked={(_personId, triggerRerun) => {
              if (triggerRerun && onTriggerSolver) onTriggerSolver();
            }}
          />
        </>
      )}
    </div>
  );
}
