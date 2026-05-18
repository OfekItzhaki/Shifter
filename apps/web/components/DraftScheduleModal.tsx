"use client";

import { useEffect, useState } from "react";
import { useTranslations, useLocale } from "next-intl";
import { apiClient } from "@/lib/api/client";
import { useDateFormat } from "@/lib/hooks/useDateFormat";
import { useAuthStore } from "@/lib/store/authStore";
import ScheduleTaskTable from "@/components/schedule/ScheduleTaskTable";
import ScheduleDiffView from "@/components/schedule/ScheduleDiffView";
import { useSandboxStore } from "@/lib/store/sandboxStore";
import RecommendationBanner from "@/components/recommendations/RecommendationBanner";

interface Assignment {
  id: string;
  personId: string;
  personName: string;
  taskTypeName: string;
  slotStartsAt: string;
  slotEndsAt: string;
}

interface Props {
  open: boolean;
  onClose: () => void;
  spaceId: string;
  groupId: string;
  draftVersionId: string;
  /** The solver run ID that produced this draft — used for recommendation banner */
  sourceRunId?: string | null;
  /** Set of personIds belonging to this group — filters out cross-group assignments */
  groupMemberIds: Set<string>;
  isAdmin: boolean;
  onPublish: () => Promise<void>;
  onDiscard: () => Promise<void>;
  onRunAgain: () => void;
}

const DAY_NAMES_SHORT = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
const DAY_NAMES_HE   = ["א׳", "ב׳", "ג׳", "ד׳", "ה׳", "ו׳", "ש׳"];

function getWeekDates(anchor: string): string[] {
  // Parse as UTC to avoid timezone shifting the date
  const parts = anchor.split("-").map(Number);
  const start = new Date(Date.UTC(parts[0], parts[1] - 1, parts[2]));
  // Go back to Sunday
  const day = start.getUTCDay();
  start.setUTCDate(start.getUTCDate() - day);
  return Array.from({ length: 7 }, (_, i) => {
    const d = new Date(start);
    d.setUTCDate(start.getUTCDate() + i);
    return d.toISOString().split("T")[0];
  });
}

export default function DraftScheduleModal({
  open, onClose, spaceId, groupId, draftVersionId, sourceRunId, groupMemberIds,
  isAdmin, onPublish, onDiscard, onRunAgain,
}: Props) {
  const t = useTranslations("draftModal");
  const tSchedule = useTranslations("schedule");
  const tAdmin = useTranslations("admin");
  const locale = useLocale();
  const dayNames = locale === "he" ? DAY_NAMES_HE : DAY_NAMES_SHORT;
  const [assignments, setAssignments] = useState<Assignment[]>([]);
  const [loading, setLoading] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [discarding, setDiscarding] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showDiscardConfirm, setShowDiscardConfirm] = useState(false);
  const [viewMode, setViewMode] = useState<"schedule" | "diff">("schedule");
  const { fDateShort } = useDateFormat();
  const timezoneId = useAuthStore(s => s.timezoneId);

  const enterSandbox = useSandboxStore((s) => s.enterSandbox);
  const [enteringSimulation, setEnteringSimulation] = useState(false);

  const today = new Date().toISOString().split("T")[0];
  const [weekAnchor, setWeekAnchor] = useState(today);
  const [selectedDay, setSelectedDay] = useState(new Date().getUTCDay());

  useEffect(() => {
    if (!open || !draftVersionId) return;
    setLoading(true);
    setError(null);
    apiClient.get(`/spaces/${spaceId}/schedule-versions/${draftVersionId}`)
      .then(r => {
        const detail = r.data;
        const all: Assignment[] = (detail.assignments ?? []).map((a: any) => ({
          id: a.id,
          personId: a.personId,
          personName: a.personName,
          taskTypeName: a.taskTypeName,
          slotStartsAt: a.slotStartsAt,
          slotEndsAt: a.slotEndsAt,
        }));
        // Filter to this group's members only
        const filtered = groupMemberIds.size > 0
          ? all.filter(a => groupMemberIds.has(a.personId))
          : all;
        setAssignments(filtered);

        // Jump to today if it has assignments, otherwise the first future date,
        // otherwise the first assignment date. Never jump backwards into the past.
        if (filtered.length > 0) {
          const dates = filtered.map(a => a.slotStartsAt.split("T")[0]).sort();
          const todayDate = new Date().toISOString().split("T")[0];
          const hasToday = dates.some(d => d === todayDate);
          const firstFuture = dates.find(d => d >= todayDate);
          const anchorDate = hasToday ? todayDate : (firstFuture ?? dates[0]);
          if (anchorDate) {
            setWeekAnchor(anchorDate);
            const parts = anchorDate.split("-").map(Number);
            setSelectedDay(new Date(Date.UTC(parts[0], parts[1] - 1, parts[2])).getUTCDay());
          }
        }
      })
      .catch(() => setError(t("errorLoading")))
      .finally(() => setLoading(false));
  }, [open, draftVersionId, spaceId]);

  async function handlePublish() {
    setPublishing(true);
    setError(null);
    try { await onPublish(); onClose(); }
    catch (e: any) { setError(e?.response?.data?.error ?? t("errorPublish")); }
    finally { setPublishing(false); }
  }

  async function handleDiscard() {
    setDiscarding(true);
    setError(null);
    try { await onDiscard(); onClose(); }
    catch (e: any) { setError(e?.response?.data?.error ?? t("errorDiscard")); }
    finally { setDiscarding(false); setShowDiscardConfirm(false); }
  }

  async function handleEnterSimulation() {
    setEnteringSimulation(true);
    setError(null);
    try {
      const { data: baseline } = await apiClient.get(
        `/spaces/${spaceId}/groups/${groupId}/solver-baseline`
      );
      enterSandbox(groupId, draftVersionId, baseline);
      onClose();
    } catch (e: any) {
      setError(e?.response?.data?.error ?? t("errorEnterSimulation"));
    } finally {
      setEnteringSimulation(false);
    }
  }

  if (!open) return null;

  const weekDates = getWeekDates(weekAnchor);
  const selectedDate = weekDates[selectedDay] ?? weekDates[0];
  const weekLabel = weekDates[0] && weekDates[6]
    ? `${fDateShort(weekDates[0] + "T00:00:00")} – ${fDateShort(weekDates[6] + "T00:00:00")}`
    : "";

  function prevWeek() {
    const parts = weekAnchor.split("-").map(Number);
    const d = new Date(Date.UTC(parts[0], parts[1] - 1, parts[2]));
    d.setUTCDate(d.getUTCDate() - 7);
    setWeekAnchor(d.toISOString().split("T")[0]);
  }
  function nextWeek() {
    const parts = weekAnchor.split("-").map(Number);
    const d = new Date(Date.UTC(parts[0], parts[1] - 1, parts[2]));
    d.setUTCDate(d.getUTCDate() + 7);
    const next = d.toISOString().split("T")[0];
    setWeekAnchor(next);
    // Select the first day of the new week that is today or in the future
    const newWeekDates = getWeekDates(next);
    const todayStr = new Date().toISOString().split("T")[0];
    const firstFutureIdx = newWeekDates.findIndex(d => d >= todayStr);
    setSelectedDay(firstFutureIdx >= 0 ? firstFutureIdx : 0);
  }

  // Convert to ScheduleTaskTable format
  const tableAssignments = assignments.map(a => ({
    id: a.id,
    personId: a.personId,
    personName: a.personName,
    taskTypeName: a.taskTypeName,
    slotStartsAt: a.slotStartsAt,
    slotEndsAt: a.slotEndsAt,
    source: "solver",
  }));

  return (
    <div
      style={{
        position: "fixed", inset: 0, zIndex: 60,
        background: "rgba(0,0,0,0.5)",
        display: "flex", alignItems: "flex-end", justifyContent: "center",
        padding: 0,
      }}
      className="sm:!items-center sm:!p-4"
      onClick={onClose}
    >
      <div
        style={{
          background: "white", borderRadius: "20px 20px 0 0",
          boxShadow: "0 24px 64px rgba(0,0,0,0.18)",
          width: "100%", maxWidth: 900,
          maxHeight: "95vh",
          display: "flex", flexDirection: "column",
        }}
        className="sm:!rounded-[20px]"
        onClick={e => e.stopPropagation()}
      >
        {/* Drag handle for mobile */}
        <div className="flex justify-center pt-2 pb-0 sm:hidden">
          <div style={{ width: 36, height: 4, borderRadius: 2, background: "#e2e8f0" }} />
        </div>

        {/* Header */}
        <div style={{
          padding: "1rem 1rem",
          borderBottom: "1px solid #e2e8f0",
          display: "flex", alignItems: "center", justifyContent: "space-between",
          flexShrink: 0,
        }}
        className="sm:!px-6 sm:!py-5"
        >
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <span style={{
              background: "#fef3c7", color: "#92400e", border: "1px solid #fde68a",
              borderRadius: 999, padding: "2px 10px", fontSize: 12, fontWeight: 700,
            }}>{t("draftBadge")}</span>
            <h2 style={{ fontSize: "1rem", fontWeight: 700, color: "#0f172a", margin: 0 }}>
              {t("title")}
            </h2>
          </div>
          <button onClick={onClose} style={{ background: "none", border: "none", cursor: "pointer", color: "#94a3b8", padding: 4 }}>
            <svg width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Body */}
        <div style={{ flex: 1, overflowY: "auto", padding: "1rem" }} className="sm:!px-6">
          {loading ? (
            <div style={{ display: "flex", justifyContent: "center", padding: "3rem 0", color: "#94a3b8" }}>
              <svg className="animate-spin" width="24" height="24" fill="none" viewBox="0 0 24 24">
                <circle style={{ opacity: 0.25 }} cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path style={{ opacity: 0.75 }} fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
              </svg>
            </div>
          ) : error ? (
            <p style={{ color: "#dc2626", fontSize: 14, textAlign: "center", padding: "2rem 0" }}>{error}</p>
          ) : assignments.length === 0 ? (
            <div style={{ textAlign: "center", padding: "2rem 0" }}>
              <p style={{ color: "#94a3b8", fontSize: 14, marginBottom: 12 }}>{t("emptyDraft")}</p>
              <p style={{ color: "#64748b", fontSize: 13, marginBottom: 16 }}>{t("emptyDraftHint")}</p>
              {isAdmin && (
                <button onClick={() => { onClose(); onRunAgain(); }}
                  style={{ background: "#3b82f6", color: "white", border: "none", borderRadius: 10, padding: "9px 20px", fontSize: 13, fontWeight: 600, cursor: "pointer" }}>
                  🔄 {t("runAgain")}
                </button>
              )}
            </div>
          ) : (
            <div className="space-y-4">
              {/* Double-shift recommendation banner */}
              {sourceRunId && (
                <RecommendationBanner
                  spaceId={spaceId}
                  runId={sourceRunId}
                  groupId={groupId}
                />
              )}

              {/* View toggle: Schedule vs Changes */}
              <div className="flex gap-1 bg-slate-100 dark:bg-slate-700 p-1 rounded-xl w-fit">
                <button
                  onClick={() => setViewMode("schedule")}
                  className={`px-3 py-1.5 rounded-lg text-xs font-medium transition-all ${
                    viewMode === "schedule" ? "bg-white dark:bg-slate-600 text-slate-900 dark:text-white shadow-sm" : "text-slate-500 hover:text-slate-700"
                  }`}
                >
                  {tSchedule("title")}
                </button>
                <button
                  onClick={() => setViewMode("diff")}
                  className={`px-3 py-1.5 rounded-lg text-xs font-medium transition-all ${
                    viewMode === "diff" ? "bg-white dark:bg-slate-600 text-slate-900 dark:text-white shadow-sm" : "text-slate-500 hover:text-slate-700"
                  }`}
                >
                  {tSchedule("diff.title")}
                </button>
              </div>

              {viewMode === "diff" ? (
                <ScheduleDiffView
                  spaceId={spaceId}
                  currentVersionId={draftVersionId}
                  onClose={() => setViewMode("schedule")}
                />
              ) : (
              <>
              {/* Week navigation */}
              <div className="flex items-center gap-2">
                <button onClick={prevWeek} className="p-2 rounded-lg border border-slate-200 hover:bg-slate-50 transition-colors">
                  <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
                  </svg>
                </button>
                <button onClick={() => { setWeekAnchor(today); setSelectedDay(new Date().getDay()); }}
                  className={`px-3 py-1.5 rounded-lg text-xs font-medium border transition-colors ${weekDates.includes(today) ? "bg-blue-500 text-white border-blue-500" : "border-slate-200 text-slate-600 hover:bg-slate-50"}`}>
                  {tSchedule("thisWeek")}
                </button>
                <button onClick={nextWeek} className="p-2 rounded-lg border border-slate-200 hover:bg-slate-50 transition-colors">
                  <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
                  </svg>
                </button>
                <span className="text-xs text-slate-500 ml-1">{weekLabel}</span>
              </div>

              {/* Day tabs */}
              <div className="flex gap-1 overflow-x-auto pb-1">
                {weekDates.map((d, i) => {
                  const dayNum = new Date(d + "T00:00:00").getDate();
                  const isSelected = i === selectedDay;
                  const isToday = d === today;
                  return (
                    <button
                      key={d}
                      onClick={() => setSelectedDay(i)}
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

              {/* Selected day label */}
              <div className="flex items-center gap-2">
                <span className="text-sm font-semibold text-slate-700">
                  {new Date(selectedDate + "T00:00:00").toLocaleDateString(
                    locale === "he" ? "he-IL" : locale === "ru" ? "ru-RU" : "en-US",
                    { weekday: "long", day: "numeric", month: "long", timeZone: timezoneId || "Asia/Jerusalem" }
                  )}
                </span>
                {selectedDate === today && (
                  <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold bg-blue-100 text-blue-700 border border-blue-200">
                    {tAdmin("today")}
                  </span>
                )}
              </div>

              {/* Per-task schedule tables */}
              <ScheduleTaskTable
                assignments={tableAssignments}
                filterDate={selectedDate}
              />
              </>
              )}
            </div>
          )}
        </div>

        {/* Footer */}
        {isAdmin && (
          <div style={{
            padding: "0.75rem 1rem",
            borderTop: "1px solid #e2e8f0",
            display: "flex", alignItems: "center", gap: 8,
            flexShrink: 0, flexWrap: "wrap",
          }}
          className="sm:!px-6 sm:!py-4 sm:!gap-[10px]"
          >
            {showDiscardConfirm ? (
              <>
                <p style={{ fontSize: 13, color: "#dc2626", flex: 1, margin: 0 }}>{t("discardConfirmText")}</p>
                <button onClick={handleDiscard} disabled={discarding}
                  style={{ background: "#ef4444", color: "white", border: "none", borderRadius: 10, padding: "8px 16px", fontSize: 13, fontWeight: 600, cursor: "pointer" }}>
                  {discarding ? t("discarding") : t("yesDiscard")}
                </button>
                <button onClick={() => setShowDiscardConfirm(false)}
                  style={{ background: "none", border: "1px solid #e2e8f0", borderRadius: 10, padding: "8px 14px", fontSize: 13, color: "#64748b", cursor: "pointer" }}>
                  {t("back")}
                </button>
              </>
            ) : (
              <>
                <button onClick={handlePublish} disabled={publishing || discarding || loading}
                  style={{ background: "#10b981", color: "white", border: "none", borderRadius: 10, padding: "9px 20px", fontSize: 13, fontWeight: 600, cursor: "pointer", opacity: publishing ? 0.6 : 1 }}>
                  {publishing ? t("publishing") : `✓ ${t("publish")}`}
                </button>
                <button onClick={handleEnterSimulation} disabled={publishing || discarding || enteringSimulation}
                  style={{ background: "#8b5cf6", color: "white", border: "none", borderRadius: 10, padding: "9px 20px", fontSize: 13, fontWeight: 600, cursor: "pointer", opacity: enteringSimulation ? 0.6 : 1 }}>
                  {enteringSimulation ? t("enteringSimulation") : `🧪 ${t("enterSimulation")}`}
                </button>
                <button onClick={() => { onClose(); onRunAgain(); }} disabled={publishing || discarding}
                  style={{ background: "#3b82f6", color: "white", border: "none", borderRadius: 10, padding: "9px 20px", fontSize: 13, fontWeight: 600, cursor: "pointer" }}>
                  🔄 {t("runAgain")}
                </button>
                <button onClick={() => setShowDiscardConfirm(true)} disabled={publishing || discarding}
                  style={{ background: "none", border: "1px solid #fca5a5", color: "#dc2626", borderRadius: 10, padding: "9px 16px", fontSize: 13, cursor: "pointer", marginLeft: "auto" }}>
                  ✕ {t("discardDraft")}
                </button>
                {error && <p style={{ fontSize: 12, color: "#dc2626", margin: 0 }}>{error}</p>}
              </>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
