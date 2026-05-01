"use client";

import { useEffect, useState } from "react";
import AppShell from "@/components/shell/AppShell";
import ScheduleTable2D from "@/components/schedule/ScheduleTable2D";
import OverrideModal, { OverridePerson } from "@/components/schedule/OverrideModal";
import DiffSummaryCard from "@/components/schedule/DiffSummaryCard";
import {
  getScheduleVersions, getVersionDetail,
  publishVersion, rollbackVersion, triggerSolve, downloadExport,
  applyManualOverride, removeManualOverride,
  ScheduleVersionDto, ScheduleVersionDetailDto
} from "@/lib/api/schedule";
import { getGroupMembers } from "@/lib/api/groups";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useAuthStore } from "@/lib/store/authStore";
import { clsx } from "clsx";

// ── StatusBadge ──────────────────────────────────────────────────────────────
function StatusBadge({ status }: { status: string }) {
  const styles: Record<string, string> = {
    Published: "bg-emerald-50 text-emerald-700 border-emerald-200",
    Draft: "bg-amber-50 text-amber-700 border-amber-200",
    Archived: "bg-slate-100 text-slate-500 border-slate-200",
    Failed: "bg-red-50 text-red-700 border-red-200",
  };
  const dots: Record<string, string> = {
    Published: "bg-emerald-500",
    Draft: "bg-amber-500",
    Archived: "bg-slate-400",
    Failed: "bg-red-500",
  };
  return (
    <span className={clsx(
      "inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-xs font-medium border",
      styles[status] ?? "bg-slate-100 text-slate-500 border-slate-200"
    )}>
      <span className={clsx("w-1.5 h-1.5 rounded-full", dots[status] ?? "bg-slate-400")} />
      {status}
    </span>
  );
}

// ── VersionListSidebar ───────────────────────────────────────────────────────
interface VersionListSidebarProps {
  versions: ScheduleVersionDto[];
  selectedId: string | undefined;
  loading: boolean;
  onSelect: (v: ScheduleVersionDto) => void;
}

function VersionListSidebar({ versions, selectedId, loading, onSelect }: VersionListSidebarProps) {
  return (
    <div className="space-y-3">
      <h2 className="text-xs font-semibold text-slate-500 uppercase tracking-wider">גרסאות</h2>
      {loading && (
        <div className="flex items-center gap-2 text-slate-400 text-sm py-4">
          <svg className="animate-spin h-4 w-4" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
          טוען...
        </div>
      )}
      <div className="space-y-1.5">
        {versions.map(v => (
          <button
            key={v.id}
            onClick={() => onSelect(v)}
            className={clsx(
              "w-full text-start px-3.5 py-3 rounded-xl border text-sm transition-all",
              selectedId === v.id
                ? "border-blue-300 bg-blue-50 shadow-sm"
                : "border-slate-200 bg-white hover:border-slate-300 hover:bg-slate-50"
            )}
          >
            <div className="font-semibold text-slate-900">v{v.versionNumber}</div>
            <div className="mt-1.5">
              <StatusBadge status={v.status} />
            </div>
          </button>
        ))}
      </div>
    </div>
  );
}

// ── InfeasibilityBanner ──────────────────────────────────────────────────────
interface InfeasibilityBannerProps {
  summaryJson: string | null | undefined;
}

function InfeasibilityBanner({ summaryJson }: InfeasibilityBannerProps) {
  if (!summaryJson) return null;
  let summary: {
    feasible?: boolean;
    explanation?: string[];
    hard_conflicts?: number;
    uncovered_slots?: number;
    conflict_details?: { rule_type: string; description: string; affected_slots: number }[];
  } = {};
  try { summary = JSON.parse(summaryJson); } catch { return null; }
  if (summary.feasible !== false) return null;

  const reasons = summary.explanation ?? [];
  const conflicts = summary.hard_conflicts ?? 0;
  const uncovered = summary.uncovered_slots ?? 0;
  const details = summary.conflict_details ?? [];

  return (
    <div className="flex flex-col gap-2 px-4 py-3 rounded-xl border bg-red-50 border-red-200 text-red-800 text-sm">
      <div className="flex items-center gap-2 font-semibold">
        <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
        </svg>
        הסידור לא ניתן לביצוע
      </div>
      {reasons.length > 0 && (
        <ul className="list-disc list-inside space-y-0.5 text-red-700">
          {reasons.map((r, i) => <li key={i}>{r}</li>)}
        </ul>
      )}
      {details.length > 0 && (
        <ul className="list-disc list-inside space-y-0.5 text-red-700 text-xs">
          {details.map((d, i) => <li key={i}>{d.description}</li>)}
        </ul>
      )}
      {(conflicts > 0 || uncovered > 0) && (
        <p className="text-red-600 text-xs">
          {conflicts > 0 && `${conflicts} אילוצים קשים לא ניתנים לסיפוק. `}
          {uncovered > 0 && `${uncovered} משמרות ללא כיסוי מספיק.`}
        </p>
      )}
      <p className="text-red-600 text-xs mt-1">
        ניתן לפתור על ידי: הוספת חברים נוספים, הרחבת אופק הזמן, או הקלת אילוצים.
      </p>
    </div>
  );
}

// ── VersionDetailPanel ───────────────────────────────────────────────────────
interface VersionDetailPanelProps {
  selected: ScheduleVersionDetailDto;
  actionLoading: boolean;
  spaceId: string | null;
  onPublish: () => void;
  onRollback: (id: string) => void;
  onCellClick?: (slotKey: string, taskName: string, assignees: string[]) => void;
}

function VersionDetailPanel({ selected, actionLoading, spaceId, onPublish, onRollback, onCellClick }: VersionDetailPanelProps) {
  const today = new Date().toISOString().split("T")[0];
  const [selectedDate, setSelectedDate] = useState(today);

  function prevDay() {
    const d = new Date(selectedDate + "T00:00:00");
    d.setDate(d.getDate() - 1);
    setSelectedDate(d.toISOString().split("T")[0]);
  }

  function nextDay() {
    const d = new Date(selectedDate + "T00:00:00");
    d.setDate(d.getDate() + 1);
    setSelectedDate(d.toISOString().split("T")[0]);
  }

  function formatDateLabel(dateStr: string): string {
    if (dateStr === today) return "היום";
    const yesterday = new Date(Date.now() - 86400000).toISOString().split("T")[0];
    const tomorrow = new Date(Date.now() + 86400000).toISOString().split("T")[0];
    if (dateStr === yesterday) return "אתמול";
    if (dateStr === tomorrow) return "מחר";
    return new Date(dateStr + "T00:00:00").toLocaleDateString("he-IL", { day: "numeric", month: "short" });
  }

  return (
    <div className="col-span-2 space-y-4">
      {/* Action bar */}
      <div className="flex items-center gap-2 flex-wrap">
        <div className="flex items-center gap-2 me-2">
          <span className="text-sm font-medium text-slate-700">
            גרסה {selected.version.versionNumber}
          </span>
          <StatusBadge status={selected.version.status} />
        </div>

        {selected.version.status === "Draft" && (
          <button
            onClick={onPublish}
            disabled={actionLoading}
            className="flex items-center gap-1.5 bg-emerald-500 hover:bg-emerald-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors"
          >
            <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
            </svg>
            פרסם
          </button>
        )}

        {selected.version.status === "Published" && (
          <button
            onClick={() => onRollback(selected.version.id)}
            disabled={actionLoading}
            className="flex items-center gap-1.5 bg-amber-500 hover:bg-amber-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors"
          >
            <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" />
            </svg>
            שחזר
          </button>
        )}

        <div className="flex items-center gap-1.5 ms-auto">
          <button
            onClick={() => spaceId && downloadExport(spaceId, selected.version.id, "csv")}
            className="flex items-center gap-1.5 text-xs text-slate-600 border border-slate-200 bg-white px-3 py-1.5 rounded-lg hover:bg-slate-50 transition-colors"
          >
            <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
            </svg>
            CSV
          </button>
          <button
            onClick={() => spaceId && downloadExport(spaceId, selected.version.id, "pdf")}
            className="flex items-center gap-1.5 text-xs text-slate-600 border border-slate-200 bg-white px-3 py-1.5 rounded-lg hover:bg-slate-50 transition-colors"
          >
            <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
            </svg>
            PDF
          </button>
        </div>
      </div>

      {/* Infeasibility banner */}
      <InfeasibilityBanner summaryJson={selected.version.summaryJson} />

      {selected.diff && <DiffSummaryCard diff={selected.diff} />}

      {/* Date navigation */}
      <div className="flex items-center gap-2">
        <button onClick={prevDay} className="p-2 rounded-lg border border-slate-200 hover:bg-slate-50 transition-colors">
          <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
          </svg>
        </button>
        <button
          onClick={() => setSelectedDate(today)}
          className={`px-3 py-1.5 rounded-lg text-sm font-medium border transition-colors ${
            selectedDate === today ? "bg-blue-500 text-white border-blue-500" : "border-slate-200 text-slate-600 hover:bg-slate-50"
          }`}
        >
          היום
        </button>
        <button onClick={nextDay} className="p-2 rounded-lg border border-slate-200 hover:bg-slate-50 transition-colors">
          <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
          </svg>
        </button>
        <span className="text-sm font-medium text-slate-700 mr-1">{formatDateLabel(selectedDate)}</span>
      </div>

      {/* 2D schedule table */}
      <ScheduleTable2D
        assignments={selected.assignments}
        filterDate={selectedDate}
        onCellClick={onCellClick}
      />
    </div>
  );
}

// ── AdminSchedulePage ────────────────────────────────────────────────────────
export default function AdminSchedulePage() {
  const { currentSpaceId } = useSpaceStore();
  const { isAdminMode } = useAuthStore();

  const [versions, setVersions] = useState<ScheduleVersionDto[]>([]);
  const [selected, setSelected] = useState<ScheduleVersionDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [message, setMessage] = useState<{ text: string; type: "success" | "error" } | null>(null);

  // ── Override modal state ─────────────────────────────────────────────────
  const [overrideCell, setOverrideCell] = useState<{
    slotKey: string; taskName: string; assignees: string[];
  } | null>(null);
  const [eligiblePeople, setEligiblePeople] = useState<OverridePerson[]>([]);
  const [overrideSaving, setOverrideSaving] = useState(false);
  const [overrideError, setOverrideError] = useState<string | null>(null);

  useEffect(() => {
    if (!currentSpaceId || !isAdminMode) { setLoading(false); return; }
    loadVersions();
  }, [currentSpaceId, isAdminMode]);

  async function loadVersions() {
    if (!currentSpaceId) return;
    setLoading(true);
    try {
      const v = await getScheduleVersions(currentSpaceId);
      setVersions(v);
      const draft = v.find(x => x.status === "Draft");
      if (draft) {
        const detail = await getVersionDetail(currentSpaceId, draft.id);
        setSelected(detail);
      }
    } finally {
      setLoading(false);
    }
  }

  async function handleSelect(v: ScheduleVersionDto) {
    if (!currentSpaceId) return;
    const detail = await getVersionDetail(currentSpaceId, v.id);
    setSelected(detail);
  }

  async function handleTrigger() {
    if (!currentSpaceId) return;
    setActionLoading(true);
    try {
      const { runId } = await triggerSolve(currentSpaceId);
      setMessage({ text: `סולבר הופעל. מזהה: ${runId}`, type: "success" });
      setTimeout(loadVersions, 3000);
    } catch {
      setMessage({ text: "שגיאה בהפעלת הסולבר.", type: "error" });
    } finally { setActionLoading(false); }
  }

  async function handleEmergencyTrigger() {
    if (!currentSpaceId) return;
    setActionLoading(true);
    try {
      const { runId } = await triggerSolve(currentSpaceId, "emergency");
      setMessage({ text: `סולבר חירום הופעל. מזהה: ${runId}`, type: "success" });
      setTimeout(loadVersions, 3000);
    } catch {
      setMessage({ text: "שגיאה בהפעלת סולבר חירום.", type: "error" });
    } finally { setActionLoading(false); }
  }

  async function handlePublish() {
    if (!currentSpaceId || !selected) return;
    setActionLoading(true);
    try {
      await publishVersion(currentSpaceId, selected.version.id);
      setMessage({ text: "Version published successfully.", type: "success" });
      await loadVersions();
    } catch {
      setMessage({ text: "Failed to publish.", type: "error" });
    } finally { setActionLoading(false); }
  }

  async function handleRollback(versionId: string) {
    if (!currentSpaceId) return;
    setActionLoading(true);
    try {
      const { newVersionId } = await rollbackVersion(currentSpaceId, versionId);
      setMessage({ text: `Rollback created. New draft version ID: ${newVersionId}`, type: "success" });
      await loadVersions();
    } catch {
      setMessage({ text: "Failed to rollback.", type: "error" });
    } finally { setActionLoading(false); }
  }

  // ── Override handlers ────────────────────────────────────────────────────
  async function handleCellClick(slotKey: string, taskName: string, assignees: string[]) {
    if (!currentSpaceId || !selected) return;
    setOverrideCell({ slotKey, taskName, assignees });
    setOverrideError(null);

    // Load eligible people from all assignments in the current version
    // (use unique person IDs from the selected version's assignments as a proxy)
    const uniquePersons = Array.from(
      new Map(selected.assignments.map(a => [a.personId, a.personName])).entries()
    ).map(([personId, displayName]) => ({ personId, displayName }));
    setEligiblePeople(uniquePersons);
  }

  async function handleOverrideConfirm(slotKey: string, newPersonIds: string[]) {
    if (!currentSpaceId) return;
    setOverrideSaving(true);
    setOverrideError(null);
    // slotKey = "${startsAt}|${endsAt}" — we need the actual slot ID
    // The slot ID is the taskSlotId from the assignment matching this slotKey
    const slotId = selected?.assignments.find(a =>
      `${a.slotStartsAt}|${a.slotEndsAt}` === slotKey
    )?.taskSlotId;
    if (!slotId) {
      setOverrideError("לא ניתן למצוא את המשמרת");
      setOverrideSaving(false);
      return;
    }
    try {
      await applyManualOverride(currentSpaceId, slotId, newPersonIds);
      setMessage({ text: "עקיפה הוחלה בהצלחה — טיוטה עודכנה", type: "success" });
      setOverrideCell(null);
      await loadVersions();
    } catch {
      setOverrideError("שגיאה בהחלת העקיפה");
    } finally {
      setOverrideSaving(false);
    }
  }

  async function handleOverrideClear(slotKey: string) {
    if (!currentSpaceId) return;
    setOverrideSaving(true);
    setOverrideError(null);
    const slotId = selected?.assignments.find(a =>
      `${a.slotStartsAt}|${a.slotEndsAt}` === slotKey
    )?.taskSlotId;
    if (!slotId) {
      setOverrideError("לא ניתן למצוא את המשמרת");
      setOverrideSaving(false);
      return;
    }
    try {
      await removeManualOverride(currentSpaceId, slotId);
      setMessage({ text: "המשמרת נוקתה — טיוטה עודכנה", type: "success" });
      setOverrideCell(null);
      await loadVersions();
    } catch {
      setOverrideError("שגיאה בניקוי המשמרת");
    } finally {
      setOverrideSaving(false);
    }
  }

  if (!isAdminMode) {
    return (
      <AppShell>
        <div className="flex flex-col items-center justify-center py-20 text-center">
          <svg className="w-12 h-12 text-slate-200 mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
          </svg>
          <p className="text-slate-500 text-sm">יש להיכנס למצב מנהל כדי לנהל סידורים.</p>
        </div>
      </AppShell>
    );
  }

  return (
    <AppShell>
      <div className="max-w-5xl space-y-6">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-slate-900">ניהול סידור</h1>
            <p className="text-sm text-slate-500 mt-1">ניהול ופרסום גרסאות סידור</p>
          </div>
          <div className="flex items-center gap-2">
            <button
              onClick={handleEmergencyTrigger}
              disabled={actionLoading}
              className="flex items-center gap-2 bg-red-500 hover:bg-red-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl shadow-sm shadow-red-500/20 disabled:opacity-50 transition-all"
            >
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
              </svg>
              סידור חירום
            </button>
            <button
              onClick={handleTrigger}
              disabled={actionLoading}
              className="flex items-center gap-2 bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl shadow-sm shadow-blue-500/20 disabled:opacity-50 transition-all"
            >
              {actionLoading ? (
                <svg className="animate-spin h-4 w-4" fill="none" viewBox="0 0 24 24">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                </svg>
              ) : (
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M13 10V3L4 14h7v7l9-11h-7z" />
                </svg>
              )}
              הפעל סולבר
            </button>
          </div>
        </div>

        {/* Message banner */}
        {message && (
          <div className={clsx(
            "flex items-center gap-3 px-4 py-3 rounded-xl border text-sm",
            message.type === "success"
              ? "bg-emerald-50 border-emerald-200 text-emerald-700"
              : "bg-red-50 border-red-200 text-red-700"
          )}>
            <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              {message.type === "success"
                ? <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                : <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              }
            </svg>
            {message.text}
          </div>
        )}

        <div className="grid grid-cols-3 gap-6">
          <VersionListSidebar
            versions={versions}
            selectedId={selected?.version.id}
            loading={loading}
            onSelect={handleSelect}
          />

          {selected ? (
            <VersionDetailPanel
              selected={selected}
              actionLoading={actionLoading}
              spaceId={currentSpaceId}
              onPublish={handlePublish}
              onRollback={handleRollback}
              onCellClick={handleCellClick}
            />
          ) : (
            <div className="col-span-2 flex flex-col items-center justify-center py-16 text-center bg-white rounded-xl border border-slate-200">
              <svg className="w-10 h-10 text-slate-200 mb-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
              </svg>
              <p className="text-slate-400 text-sm">בחר גרסה לצפייה בפרטים.</p>
            </div>
          )}
        </div>
      </div>

      {/* Override modal */}
      {overrideCell && (
        <OverrideModal
          open={!!overrideCell}
          slotKey={overrideCell.slotKey}
          taskName={overrideCell.taskName}
          currentAssignees={overrideCell.assignees}
          eligiblePeople={eligiblePeople}
          saving={overrideSaving}
          error={overrideError}
          onConfirm={handleOverrideConfirm}
          onClear={handleOverrideClear}
          onClose={() => { setOverrideCell(null); setOverrideError(null); }}
        />
      )}
    </AppShell>
  );
}
