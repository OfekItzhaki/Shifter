"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import AppShell from "@/components/shell/AppShell";
import MiniChart from "@/components/stats/MiniChart";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { getHistoricalStats, HistoricalStats } from "@/lib/api/stats";

const DAY_OPTIONS = [7, 14, 30, 90] as const;

export default function StatsPage() {
  const t = useTranslations("stats");
  const currentSpaceId = useSpaceStore((s) => s.currentSpaceId);
  const [stats, setStats] = useState<HistoricalStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [days, setDays] = useState<number>(30);

  useEffect(() => {
    if (!currentSpaceId) return;
    setLoading(true);
    getHistoricalStats(currentSpaceId, days)
      .then(setStats)
      .catch(() => setStats(null))
      .finally(() => setLoading(false));
  }, [currentSpaceId, days]);

  const dayLabel = (d: number) => {
    switch (d) {
      case 7: return t("last7days");
      case 14: return t("last14days");
      case 30: return t("last30days");
      case 90: return t("last90days");
      default: return `${d}d`;
    }
  };

  return (
    <AppShell>
      <div className="max-w-5xl mx-auto">
        {/* Header */}
        <div className="flex items-center justify-between mb-6">
          <h1 className="text-2xl font-bold text-slate-900 dark:text-white">
            {t("title")}
          </h1>
          <div className="flex gap-1 bg-slate-100 dark:bg-slate-800 rounded-lg p-1">
            {DAY_OPTIONS.map((d) => (
              <button
                key={d}
                onClick={() => setDays(d)}
                className={`px-3 py-1.5 text-sm font-medium rounded-md transition-colors ${
                  days === d
                    ? "bg-white dark:bg-slate-700 text-blue-600 dark:text-blue-400 shadow-sm"
                    : "text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white"
                }`}
              >
                {dayLabel(d)}
              </button>
            ))}
          </div>
        </div>

        {/* Loading state */}
        {loading && (
          <div className="flex items-center justify-center py-20">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500" />
          </div>
        )}

        {/* Empty state */}
        {!loading && !stats && (
          <div className="text-center py-20 text-slate-500 dark:text-slate-400">
            {t("noData")}
          </div>
        )}

        {/* Stats cards */}
        {!loading && stats && (
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
            {/* Assignments card */}
            <div className="bg-white dark:bg-slate-800 rounded-xl p-5 shadow-sm border border-slate-200 dark:border-slate-700">
              <div className="text-sm font-medium text-slate-500 dark:text-slate-400 mb-1">
                {t("assignments")}
              </div>
              <div className="text-3xl font-bold text-slate-900 dark:text-white mb-3">
                {stats.totalAssignments.toLocaleString()}
              </div>
              <MiniChart
                data={stats.assignmentsPerDay.map((p) => ({
                  label: p.date,
                  value: p.count,
                }))}
                color="#3b82f6"
                height={60}
              />
            </div>

            {/* Solver Runs card */}
            <div className="bg-white dark:bg-slate-800 rounded-xl p-5 shadow-sm border border-slate-200 dark:border-slate-700">
              <div className="text-sm font-medium text-slate-500 dark:text-slate-400 mb-1">
                {t("solverRuns")}
              </div>
              <div className="text-3xl font-bold text-slate-900 dark:text-white mb-3">
                {stats.totalSolverRuns.toLocaleString()}
              </div>
              <MiniChart
                data={stats.solverRunsPerDay.map((p) => ({
                  label: p.date,
                  value: p.count,
                }))}
                color="#8b5cf6"
                height={60}
              />
            </div>

            {/* Versions Published card */}
            <div className="bg-white dark:bg-slate-800 rounded-xl p-5 shadow-sm border border-slate-200 dark:border-slate-700">
              <div className="text-sm font-medium text-slate-500 dark:text-slate-400 mb-1">
                {t("versionsPublished")}
              </div>
              <div className="text-3xl font-bold text-slate-900 dark:text-white">
                {stats.totalVersionsPublished.toLocaleString()}
              </div>
            </div>
          </div>
        )}

        {/* Burden trend card */}
        {!loading && stats && stats.burdenScorePerWeek.length > 0 && (
          <div className="bg-white dark:bg-slate-800 rounded-xl p-5 shadow-sm border border-slate-200 dark:border-slate-700">
            <div className="text-sm font-medium text-slate-500 dark:text-slate-400 mb-3">
              {t("burdenTrend")}
            </div>
            <MiniChart
              data={stats.burdenScorePerWeek.map((p) => ({
                label: p.weekStart,
                value: Math.round(p.averageScore * 100),
              }))}
              color="#f59e0b"
              height={100}
              type="line"
            />
          </div>
        )}
      </div>
    </AppShell>
  );
}
