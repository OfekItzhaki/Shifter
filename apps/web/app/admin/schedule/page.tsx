"use client";

import { useEffect, useState } from "react";
import AppShell from "@/components/shell/AppShell";
import ScheduleTable from "@/components/schedule/ScheduleTable";
import DiffSummaryCard from "@/components/schedule/DiffSummaryCard";
import {
  getScheduleVersions, getVersionDetail,
  publishVersion, rollbackVersion, triggerSolve, downloadExport,
  ScheduleVersionDto, ScheduleVersionDetailDto
} from "@/lib/api/schedule";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useAuthStore } from "@/lib/store/authStore";
import { clsx } from "clsx";

export default function AdminSchedulePage() {
  const { currentSpaceId } = useSpaceStore();
  const { isAdminMode } = useAuthStore();

  const [versions, setVersions] = useState<ScheduleVersionDto[]>([]);
  const [selected, setSelected] = useState<ScheduleVersionDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [message, setMessage] = useState<{ text: string; type: "success" | "error" } | null>(null);

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
      // Auto-select the latest draft
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
      setMessage({ text: `Solver triggered. Run ID: ${runId}`, type: "success" });
      setTimeout(loadVersions, 3000); // reload after a few seconds
    } catch {
      setMessage({ text: "Failed to trigger solver.", type: "error" });
    } finally {
      setActionLoading(false);
    }
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
    } finally {
      setActionLoading(false);
    }
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
    } finally {
      setActionLoading(false);
    }
  }

  if (!isAdminMode) {
    return (
      <AppShell>
        <p className="text-gray-500 text-sm">Enter admin mode to manage schedules.</p>
      </AppShell>
    );
  }

  return (
    <AppShell>
      <div className="space-y-6 max-w-5xl">
        <div className="flex items-center justify-between">
          <h1 className="text-xl font-semibold">Schedule Management</h1>
          <button
            onClick={handleTrigger}
            disabled={actionLoading}
            className="bg-blue-600 text-white text-sm px-4 py-2 rounded-lg hover:bg-blue-700 disabled:opacity-50"
          >
            {actionLoading ? "..." : "Trigger Solve"}
          </button>
        </div>

        {message && (
          <div className={clsx(
            "text-sm px-4 py-3 rounded-lg",
            message.type === "success" ? "bg-green-50 text-green-700" : "bg-red-50 text-red-700"
          )}>
            {message.text}
          </div>
        )}

        <div className="grid grid-cols-3 gap-6">
          {/* Version list */}
          <div className="space-y-2">
            <h2 className="text-sm font-semibold text-gray-600 uppercase tracking-wide">Versions</h2>
            {loading && <p className="text-gray-400 text-sm">Loading...</p>}
            {versions.map(v => (
              <button
                key={v.id}
                onClick={() => handleSelect(v)}
                className={clsx(
                  "w-full text-start px-3 py-2 rounded-lg border text-sm transition-colors",
                  selected?.version.id === v.id
                    ? "border-blue-500 bg-blue-50"
                    : "border-gray-200 hover:bg-gray-50"
                )}
              >
                <div className="font-medium">v{v.versionNumber}</div>
                <div className={clsx(
                  "text-xs mt-0.5",
                  v.status === "Published" ? "text-green-600" :
                  v.status === "Draft" ? "text-amber-600" : "text-gray-400"
                )}>
                  {v.status}
                </div>
              </button>
            ))}
          </div>

          {/* Version detail */}
          <div className="col-span-2 space-y-4">
            {selected ? (
              <>
                <div className="flex items-center gap-3">
                  <span className="text-sm text-gray-500">
                    Version {selected.version.versionNumber} — {selected.version.status}
                  </span>

                  {selected.version.status === "Draft" && (
                    <button
                      onClick={handlePublish}
                      disabled={actionLoading}
                      className="bg-green-600 text-white text-xs px-3 py-1.5 rounded-lg hover:bg-green-700 disabled:opacity-50"
                    >
                      Publish
                    </button>
                  )}

                  {selected.version.status === "Published" && (
                    <button
                      onClick={() => handleRollback(selected.version.id)}
                      disabled={actionLoading}
                      className="bg-amber-500 text-white text-xs px-3 py-1.5 rounded-lg hover:bg-amber-600 disabled:opacity-50"
                    >
                      Rollback to this version
                    </button>
                  )}

                  <button
                    onClick={() => currentSpaceId && downloadExport(currentSpaceId, selected.version.id, "csv")}
                    className="text-xs text-gray-600 border border-gray-300 px-3 py-1.5 rounded-lg hover:bg-gray-50"
                  >
                    ↓ CSV
                  </button>
                  <button
                    onClick={() => currentSpaceId && downloadExport(currentSpaceId, selected.version.id, "pdf")}
                    className="text-xs text-gray-600 border border-gray-300 px-3 py-1.5 rounded-lg hover:bg-gray-50"
                  >
                    ↓ PDF
                  </button>
                </div>

                {selected.diff && <DiffSummaryCard diff={selected.diff} />}

                <ScheduleTable assignments={selected.assignments} />
              </>
            ) : (
              <p className="text-gray-400 text-sm">Select a version to view details.</p>
            )}
          </div>
        </div>
      </div>
    </AppShell>
  );
}
