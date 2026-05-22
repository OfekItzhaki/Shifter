"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { apiClient } from "@/lib/api/client";
import { useAuthStore } from "@/lib/store/authStore";
import { formatLocalTime, formatLocalDate } from "@/lib/utils/formatTime";
import type { AssignmentDto, DiffSummaryDto } from "@/lib/api/schedule";

interface Props {
  spaceId: string;
  currentVersionId: string;
  /** If provided, compare against this version. Otherwise compare against the previous published version. */
  baselineVersionId?: string;
  onClose: () => void;
}

interface DiffEntry {
  type: "added" | "removed" | "changed";
  personName: string;
  taskName: string;
  slotStart: string;
  slotEnd: string;
  /** For "changed" entries — who was previously assigned */
  previousPersonName?: string;
}

function formatTime(iso: string, timezoneId: string | null): string {
  return formatLocalTime(iso, timezoneId, "24h");
}

function formatDate(iso: string, timezoneId: string | null): string {
  return formatLocalDate(iso, timezoneId);
}

export default function ScheduleDiffView({ spaceId, currentVersionId, baselineVersionId, onClose }: Props) {
  const t = useTranslations("schedule.diff");
  const timezoneId = useAuthStore(s => s.timezoneId);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [diffEntries, setDiffEntries] = useState<DiffEntry[]>([]);
  const [summary, setSummary] = useState<DiffSummaryDto | null>(null);
  const [filter, setFilter] = useState<"all" | "added" | "removed" | "changed">("all");

  useEffect(() => {
    loadDiff();
  }, [currentVersionId, baselineVersionId]);

  async function loadDiff() {
    setLoading(true);
    setError(null);
    try {
      // If "current", fetch the current published version first
      let versionId = currentVersionId;
      let currentDetail: any;

      if (versionId === "current") {
        const currentRes = await apiClient.get(`/spaces/${spaceId}/schedule-versions/current`);
        currentDetail = currentRes.data;
        versionId = currentDetail.version?.id;
      } else {
        const currentRes = await apiClient.get(`/spaces/${spaceId}/schedule-versions/${versionId}`);
        currentDetail = currentRes.data;
      }

      if (!versionId || !currentDetail) {
        setError(t("errorLoading"));
        setLoading(false);
        return;
      }

      const currentAssignments: AssignmentDto[] = currentDetail.assignments ?? [];
      const diffSummary: DiffSummaryDto | null = currentDetail.diff ?? null;
      setSummary(diffSummary);

      // Determine baseline version
      let baselineId = baselineVersionId ?? currentDetail.version?.baselineVersionId;

      if (!baselineId) {
        // Try to find the previous published version
        const versionsRes = await apiClient.get(`/spaces/${spaceId}/schedule-versions?status=published`);
        const published = (versionsRes.data ?? []) as Array<{ id: string; versionNumber: number }>;
        const sorted = published.sort((a, b) => b.versionNumber - a.versionNumber);
        const currentNum = currentDetail.version?.versionNumber ?? 0;
        const prev = sorted.find(v => v.versionNumber < currentNum);
        baselineId = prev?.id;
      }

      if (!baselineId) {
        // No baseline — everything is "added"
        const entries: DiffEntry[] = currentAssignments.map(a => ({
          type: "added",
          personName: a.personName,
          taskName: a.taskTypeName,
          slotStart: a.slotStartsAt,
          slotEnd: a.slotEndsAt,
        }));
        setDiffEntries(entries);
        setLoading(false);
        return;
      }

      // Load baseline version
      const baselineRes = await apiClient.get(`/spaces/${spaceId}/schedule-versions/${baselineId}`);
      const baselineAssignments: AssignmentDto[] = baselineRes.data.assignments ?? [];

      // Compute diff
      const entries = computeDiff(baselineAssignments, currentAssignments);
      setDiffEntries(entries);
    } catch {
      setError(t("errorLoading"));
    } finally {
      setLoading(false);
    }
  }

  function computeDiff(baseline: AssignmentDto[], current: AssignmentDto[]): DiffEntry[] {
    const entries: DiffEntry[] = [];

    // Build maps: slotKey → assignments
    // slotKey = taskSlotId (unique per time slot)
    const baselineBySlot = new Map<string, AssignmentDto[]>();
    for (const a of baseline) {
      const list = baselineBySlot.get(a.taskSlotId) ?? [];
      list.push(a);
      baselineBySlot.set(a.taskSlotId, list);
    }

    const currentBySlot = new Map<string, AssignmentDto[]>();
    for (const a of current) {
      const list = currentBySlot.get(a.taskSlotId) ?? [];
      list.push(a);
      currentBySlot.set(a.taskSlotId, list);
    }

    // Find added and changed
    for (const [slotId, currentSlotAssignments] of currentBySlot) {
      const baselineSlotAssignments = baselineBySlot.get(slotId) ?? [];
      const baselinePersonIds = new Set(baselineSlotAssignments.map(a => a.personId));

      for (const a of currentSlotAssignments) {
        if (!baselinePersonIds.has(a.personId)) {
          // Check if someone else was in this slot before (changed) or it's new (added)
          const previousInSlot = baselineSlotAssignments.find(b => !currentBySlot.get(slotId)?.some(c => c.personId === b.personId));
          if (previousInSlot) {
            entries.push({
              type: "changed",
              personName: a.personName,
              taskName: a.taskTypeName,
              slotStart: a.slotStartsAt,
              slotEnd: a.slotEndsAt,
              previousPersonName: previousInSlot.personName,
            });
          } else {
            entries.push({
              type: "added",
              personName: a.personName,
              taskName: a.taskTypeName,
              slotStart: a.slotStartsAt,
              slotEnd: a.slotEndsAt,
            });
          }
        }
      }
    }

    // Find removed
    for (const [slotId, baselineSlotAssignments] of baselineBySlot) {
      const currentSlotAssignments = currentBySlot.get(slotId) ?? [];
      const currentPersonIds = new Set(currentSlotAssignments.map(a => a.personId));

      for (const a of baselineSlotAssignments) {
        if (!currentPersonIds.has(a.personId)) {
          // Only add as "removed" if not already captured as "changed"
          const alreadyCaptured = entries.some(
            e => e.type === "changed" && e.previousPersonName === a.personName &&
              e.slotStart === a.slotStartsAt && e.taskName === a.taskTypeName
          );
          if (!alreadyCaptured) {
            entries.push({
              type: "removed",
              personName: a.personName,
              taskName: a.taskTypeName,
              slotStart: a.slotStartsAt,
              slotEnd: a.slotEndsAt,
            });
          }
        }
      }
    }

    // Sort by date, then by type priority (changed > added > removed)
    const typePriority = { changed: 0, added: 1, removed: 2 };
    entries.sort((a, b) => {
      const dateCompare = new Date(a.slotStart).getTime() - new Date(b.slotStart).getTime();
      if (dateCompare !== 0) return dateCompare;
      return typePriority[a.type] - typePriority[b.type];
    });

    return entries;
  }

  const filtered = filter === "all" ? diffEntries : diffEntries.filter(e => e.type === filter);

  const addedCount = diffEntries.filter(e => e.type === "added").length;
  const removedCount = diffEntries.filter(e => e.type === "removed").length;
  const changedCount = diffEntries.filter(e => e.type === "changed").length;

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h3 className="text-base font-semibold text-slate-900">{t("title")}</h3>
        <button
          onClick={onClose}
          className="text-xs text-slate-500 hover:text-slate-700 border border-slate-200 px-3 py-1.5 rounded-lg"
        >
          {t("close")}
        </button>
      </div>

      {loading ? (
        <div className="flex items-center justify-center py-12">
          <svg className="animate-spin h-6 w-6 text-sky-400" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
        </div>
      ) : error ? (
        <p className="text-sm text-red-600 text-center py-8">{error}</p>
      ) : diffEntries.length === 0 ? (
        <div className="text-center py-12 bg-white rounded-xl border border-slate-200">
          <p className="text-sm text-slate-400">{t("noChanges")}</p>
        </div>
      ) : (
        <>
          {/* Summary cards */}
          <div className="grid grid-cols-3 gap-3">
            <button
              onClick={() => setFilter(filter === "added" ? "all" : "added")}
              className={`rounded-xl p-3 text-center transition-all border ${
                filter === "added" ? "border-emerald-400 bg-emerald-50 ring-2 ring-emerald-200" : "border-slate-200 bg-white hover:border-emerald-200"
              }`}
            >
              <p className="text-2xl font-bold text-emerald-600">{addedCount}</p>
              <p className="text-xs text-emerald-700 mt-0.5">{t("added")}</p>
            </button>
            <button
              onClick={() => setFilter(filter === "removed" ? "all" : "removed")}
              className={`rounded-xl p-3 text-center transition-all border ${
                filter === "removed" ? "border-red-400 bg-red-50 ring-2 ring-red-200" : "border-slate-200 bg-white hover:border-red-200"
              }`}
            >
              <p className="text-2xl font-bold text-red-600">{removedCount}</p>
              <p className="text-xs text-red-700 mt-0.5">{t("removed")}</p>
            </button>
            <button
              onClick={() => setFilter(filter === "changed" ? "all" : "changed")}
              className={`rounded-xl p-3 text-center transition-all border ${
                filter === "changed" ? "border-amber-400 bg-amber-50 ring-2 ring-amber-200" : "border-slate-200 bg-white hover:border-amber-200"
              }`}
            >
              <p className="text-2xl font-bold text-amber-600">{changedCount}</p>
              <p className="text-xs text-amber-700 mt-0.5">{t("changed")}</p>
            </button>
          </div>

          {/* Diff list */}
          <div className="space-y-2 max-h-[60vh] overflow-y-auto">
            {filtered.map((entry, i) => (
              <DiffEntryCard key={i} entry={entry} />
            ))}
          </div>
        </>
      )}
    </div>
  );
}

function DiffEntryCard({ entry }: { entry: DiffEntry }) {
  const t = useTranslations("schedule.diff");
  const timezoneId = useAuthStore(s => s.timezoneId);

  const isHomeLeave = entry.taskName === "home_leave";
  const displayTaskName = isHomeLeave ? "בבית" : entry.taskName;

  const config = {
    added: { bg: "bg-emerald-50", border: "border-emerald-200", icon: "+", iconColor: "text-emerald-600", label: t("assignedTo") },
    removed: { bg: "bg-red-50", border: "border-red-200", icon: "−", iconColor: "text-red-600", label: t("removedFrom") },
    changed: { bg: "bg-amber-50", border: "border-amber-200", icon: "↔", iconColor: "text-amber-600", label: t("replacedBy") },
  }[entry.type];

  return (
    <div className={`${config.bg} border ${config.border} rounded-xl px-4 py-3 flex items-start gap-3`}>
      {/* Type icon */}
      <span className={`${config.iconColor} text-lg font-bold mt-0.5 w-5 text-center flex-shrink-0`}>
        {config.icon}
      </span>

      {/* Content */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          {isHomeLeave ? (
            <span className="inline-flex items-center gap-1 text-sm font-semibold text-emerald-700">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="flex-shrink-0">
                <path strokeLinecap="round" strokeLinejoin="round" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6" />
              </svg>
              {displayTaskName}
            </span>
          ) : (
            <span className="text-sm font-semibold text-slate-900">{displayTaskName}</span>
          )}
          <span className="text-xs text-slate-400">•</span>
          <span className="text-xs text-slate-500">
            {formatDate(entry.slotStart, timezoneId)} {formatTime(entry.slotStart, timezoneId)}–{formatTime(entry.slotEnd, timezoneId)}
          </span>
        </div>

        <div className="mt-1 text-sm text-slate-700">
          {entry.type === "changed" ? (
            <span>
              <span className="text-red-600 line-through">{entry.previousPersonName}</span>
              {" → "}
              <span className="text-emerald-700 font-medium">{entry.personName}</span>
            </span>
          ) : entry.type === "added" ? (
            <span className="text-emerald-700 font-medium">{entry.personName}</span>
          ) : (
            <span className="text-red-600">{entry.personName}</span>
          )}
        </div>
      </div>
    </div>
  );
}
