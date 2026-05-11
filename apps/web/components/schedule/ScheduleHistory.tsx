"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { apiClient } from "@/lib/api/client";
import { useDateFormat } from "@/lib/hooks/useDateFormat";
import ScheduleDiffView from "./ScheduleDiffView";

interface VersionSummary {
  id: string;
  versionNumber: number;
  status: string;
  createdAt: string;
  publishedAt: string | null;
}

interface Props {
  spaceId: string;
  onClose: () => void;
}

export default function ScheduleHistory({ spaceId, onClose }: Props) {
  const t = useTranslations("schedule.history");
  const { fDateTime } = useDateFormat();
  const [versions, setVersions] = useState<VersionSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [showDiff, setShowDiff] = useState(false);

  useEffect(() => {
    apiClient.get(`/spaces/${spaceId}/schedule-versions`)
      .then(res => {
        const all = (res.data ?? []) as VersionSummary[];
        // Show published and archived versions, sorted newest first
        const published = all.filter(v => v.status.toLowerCase() === "published" || v.status.toLowerCase() === "archived");
        setVersions(published);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [spaceId]);

  if (showDiff && selectedId) {
    return (
      <ScheduleDiffView
        spaceId={spaceId}
        currentVersionId={selectedId}
        onClose={() => setShowDiff(false)}
      />
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-base font-semibold text-slate-900 dark:text-white">{t("title")}</h3>
        <button onClick={onClose} className="text-xs text-slate-500 hover:text-slate-700 border border-slate-200 px-3 py-1.5 rounded-lg">
          {t("close")}
        </button>
      </div>

      {loading ? (
        <div className="flex justify-center py-8">
          <svg className="animate-spin h-6 w-6 text-blue-400" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
        </div>
      ) : versions.length === 0 ? (
        <p className="text-sm text-slate-400 text-center py-8">{t("noVersions")}</p>
      ) : (
        <div className="space-y-2 max-h-[60vh] overflow-y-auto">
          {versions.map((v, i) => (
            <div key={v.id} className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl px-4 py-3 flex items-center justify-between gap-3">
              <div className="flex items-center gap-3 min-w-0">
                <div className="w-8 h-8 rounded-lg bg-blue-50 dark:bg-blue-900/30 flex items-center justify-center flex-shrink-0">
                  <span className="text-xs font-bold text-blue-600">v{v.versionNumber}</span>
                </div>
                <div className="min-w-0">
                  <p className="text-sm font-medium text-slate-800 dark:text-slate-200">
                    {t("version")} {v.versionNumber}
                    {i === 0 && <span className="ml-2 text-xs bg-emerald-100 text-emerald-700 px-2 py-0.5 rounded-full">{t("current")}</span>}
                  </p>
                  <p className="text-xs text-slate-400">
                    {v.publishedAt ? fDateTime(v.publishedAt) : fDateTime(v.createdAt)}
                  </p>
                </div>
              </div>
              <div className="flex items-center gap-2 flex-shrink-0">
                {i > 0 && (
                  <button
                    onClick={() => { setSelectedId(v.id); setShowDiff(true); }}
                    className="text-xs text-blue-600 border border-blue-200 hover:bg-blue-50 px-3 py-1.5 rounded-lg transition-colors"
                  >
                    {t("viewChanges")}
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
