"use client";

import { useEffect, useState } from "react";
import { apiClient } from "@/lib/api/client";

interface RotationEntry {
  personId: string;
  displayName: string;
  cycleNumber: number;
  completionPercentage: number;
  completedCount: number;
  totalQualified: number;
}

interface RotationProgressCardProps {
  spaceId: string;
  groupId: string;
}

export default function RotationProgressCard({
  spaceId,
  groupId,
}: RotationProgressCardProps) {
  const [entries, setEntries] = useState<RotationEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    apiClient
      .get(`/spaces/${spaceId}/stats/rotation`, { params: { groupId } })
      .then(({ data }) => {
        if (!cancelled) {
          // API returns { people: [...] } — extract the array
          const arr = Array.isArray(data) ? data : (data?.people ?? data?.entries ?? []);
          setEntries(Array.isArray(arr) ? arr : []);
        }
      })
      .catch((err) => {
        if (!cancelled) {
          // 404 means group doesn't have rotation data — not an error, just hide
          if (err?.response?.status === 404) {
            setEntries([]);
          } else {
            setError("שגיאה בטעינת נתוני רוטציה");
          }
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => { cancelled = true; };
  }, [spaceId, groupId]);

  // Don't render anything if no rotation data or 404
  if (!loading && entries.length === 0 && !error) return null;

  if (loading) {
    return (
      <div className="bg-white border border-slate-200 rounded-xl p-4">
        <p className="text-xs text-slate-400">טוען נתוני רוטציה...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="bg-white border border-slate-200 rounded-xl p-4">
        <p className="text-xs text-red-500">{error}</p>
      </div>
    );
  }

  return (
    <div className="bg-white border border-slate-200 rounded-xl p-4">
      <h3 className="text-sm font-semibold text-slate-700 mb-3">
        התקדמות רוטציה
      </h3>
      <div className="space-y-3">
        {entries.map((entry) => (
          <div key={entry.personId} className="flex items-center gap-3">
            <div className="flex-1 min-w-0">
              <div className="flex items-center justify-between mb-1">
                <span className="text-sm font-medium text-slate-700 truncate">
                  {entry.displayName}
                </span>
                <span className="text-xs text-slate-500 shrink-0 mr-2">
                  מחזור {entry.cycleNumber} • {Math.round(entry.completionPercentage)}%
                </span>
              </div>
              <div className="w-full bg-slate-100 rounded-full h-2">
                <div
                  className="bg-sky-500 h-2 rounded-full transition-all"
                  style={{ width: `${Math.min(entry.completionPercentage, 100)}%` }}
                />
              </div>
              <p className="text-xs text-slate-400 mt-0.5">
                {entry.completedCount ?? 0}/{entry.totalQualified ?? 0} סוגי משימות
              </p>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
