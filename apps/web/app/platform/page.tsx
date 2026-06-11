"use client";

import { useEffect, useState, useCallback } from "react";
import { useTranslations } from "next-intl";
import { useRouter } from "next/navigation";
import AppShell from "@/components/shell/AppShell";
import { getPlatformStats, PlatformStats } from "@/lib/api/platform";
import { apiClient } from "@/lib/api/client";
import BillingTestPanel from "@/components/platform/BillingTestPanel";
import PlatformSettings from "@/components/platform/PlatformSettings";
import ProviderHealthPanel from "@/components/platform/ProviderHealthPanel";
import ReAuthDialog from "@/components/admin/ReAuthDialog";
import { useAdminSessionStore } from "@/lib/store/adminSessionStore";
import { useEffectiveAuth } from "@/lib/hooks/useEffectiveAuth";

function StatCard({ label, value, sub, accent }: { label: string; value: string | number; sub?: string; accent?: string }) {
  return (
    <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm hover:shadow-md transition-shadow">
      <p className="text-xs font-semibold text-slate-400 dark:text-slate-500 uppercase tracking-wider mb-1">
        {label}
      </p>
      <p className={`text-2xl font-extrabold ${accent ?? "text-slate-900 dark:text-white"} leading-tight`}>
        {value}
      </p>
      {sub && <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">{sub}</p>}
    </div>
  );
}

function SolverHealthBar({ completed, failed, total }: { completed: number; failed: number; total: number }) {
  if (total === 0) return null;
  const successPct = Math.round((completed / total) * 100);
  const failPct = Math.round((failed / total) * 100);

  return (
    <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
      <div className="flex items-center justify-between mb-3">
        <p className="text-sm font-semibold text-slate-900 dark:text-white">Solver Health (24h)</p>
        <span className={`text-xs font-bold px-2 py-0.5 rounded-full ${successPct >= 80 ? "bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-400" : successPct >= 50 ? "bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400" : "bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400"}`}>
          {successPct}% success
        </span>
      </div>
      <div className="w-full h-3 rounded-full bg-slate-100 dark:bg-slate-700 overflow-hidden flex">
        <div className="h-full bg-emerald-500 transition-all" style={{ width: `${successPct}%` }} />
        <div className="h-full bg-red-500 transition-all" style={{ width: `${failPct}%` }} />
      </div>
      <div className="flex justify-between mt-2 text-xs text-slate-500 dark:text-slate-400">
        <span>{completed} completed</span>
        <span>{failed} failed</span>
        <span>{total} total</span>
      </div>
    </div>
  );
}

export default function PlatformPage() {
  const t = useTranslations("platform");
  const { isLoggedIn, isHydrated } = useEffectiveAuth();
  const router = useRouter();
  const enterElevatedMode = useAdminSessionStore((s) => s.enterElevatedMode);
  const isElevated = useAdminSessionStore((s) => s.isElevated);
  const elevatedMode = useAdminSessionStore((s) => s.elevatedMode);

  const [stats, setStats] = useState<PlatformStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [accessDenied, setAccessDenied] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [showReAuth, setShowReAuth] = useState(false);
  const [platformAuthenticated, setPlatformAuthenticated] = useState(false);
  const [platformTimeoutMinutes, setPlatformTimeoutMinutes] = useState<number>(15);
  useEffect(() => {
    if (!isHydrated) return;
    if (!isLoggedIn) { router.push("/login"); return; }

    if (isElevated && elevatedMode === "platform") {
      setPlatformAuthenticated(true);
    } else if (!platformAuthenticated) {
      setShowReAuth(true);
      apiClient.get<{ platformTimeoutMinutes: number }>("/platform/settings")
        .then(({ data }) => setPlatformTimeoutMinutes(data.platformTimeoutMinutes ?? 15))
        .catch(() => setPlatformTimeoutMinutes(15));
      return;
    }

    setLoading(true);
    const timeout = setTimeout(() => { setLoading(false); setError("Loading failed — try refreshing"); }, 10000);
    getPlatformStats()
      .then(setStats)
      .catch((err) => {
        if (err?.response?.status === 403) setAccessDenied(true);
        else setError(err?.response?.data?.error ?? err?.message ?? "Error loading platform data");
      })
      .finally(() => { setLoading(false); clearTimeout(timeout); });
  }, [isHydrated, isLoggedIn, router, platformAuthenticated, isElevated, elevatedMode]);

  const handleReAuthSuccess = useCallback(() => {
    setShowReAuth(false);
    setPlatformAuthenticated(true);
    enterElevatedMode("platform", undefined, platformTimeoutMinutes);
  }, [enterElevatedMode, platformTimeoutMinutes]);

  const handleReAuthCancel = useCallback(() => {
    setShowReAuth(false);
    router.push("/");
  }, [router]);

  if (!isHydrated || !isLoggedIn) {
    return (
      <AppShell>
        <div className="flex items-center justify-center min-h-[60vh]">
          <svg className="animate-spin h-8 w-8 text-sky-400" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
        </div>
      </AppShell>
    );
  }

  if (showReAuth) {
    return (
      <AppShell>
        <ReAuthDialog open={showReAuth} onSuccess={handleReAuthSuccess} onCancel={handleReAuthCancel} mode="platform" />
      </AppShell>
    );
  }

  return (
    <AppShell>
      <div style={{ maxWidth: 1100 }} className="w-full">
        {/* Header */}
        <div className="mb-6">
          <h1 className="text-2xl font-extrabold text-slate-900 dark:text-white">
            {t("title")}
          </h1>
          <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">
            System overview and administration
          </p>
        </div>

        {loading && (
          <div className="flex items-center gap-3 text-slate-400 text-sm py-8">
            <svg className="animate-spin" width="20" height="20" fill="none" viewBox="0 0 24 24">
              <circle style={{ opacity: 0.25 }} cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path style={{ opacity: 0.75 }} fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
            </svg>
          </div>
        )}

        {accessDenied && (
          <div className="flex flex-col items-center justify-center py-20 text-center bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700">
            <svg width="48" height="48" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5} className="text-red-500 mb-3">
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4.5c-.77-.833-2.694-.833-3.464 0L3.34 16.5c-.77.833.192 2.5 1.732 2.5z" />
            </svg>
            <p className="text-sm text-red-600 dark:text-red-400 font-semibold">{t("accessDenied")}</p>
          </div>
        )}

        {error && !accessDenied && (
          <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
        )}

        {stats && (
          <div className="space-y-5">
            {/* Row 1: Users & Infrastructure */}
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
              <StatCard label={t("totalUsers")} value={stats.totalUsers} accent="text-sky-600 dark:text-sky-400" />
              <StatCard label={t("activeUsers7d")} value={stats.activeUsersLast7d} accent="text-emerald-600 dark:text-emerald-400" />
              <StatCard label={t("totalSpaces")} value={stats.totalSpaces} />
              <StatCard label={t("totalGroups")} value={stats.totalGroups} />
            </div>

            {/* Solver Health Bar */}
            <SolverHealthBar
              completed={stats.solverStats.completedLast24h}
              failed={stats.solverStats.failedLast24h}
              total={stats.solverStats.totalRunsLast24h}
            />

            {/* Row 2: Solver details */}
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
              <StatCard label={t("solverRuns24h")} value={stats.solverStats.totalRunsLast24h} />
              <StatCard label={t("completed")} value={stats.solverStats.completedLast24h} accent="text-emerald-600 dark:text-emerald-400" />
              <StatCard label={t("failed")} value={stats.solverStats.failedLast24h} accent={stats.solverStats.failedLast24h > 0 ? "text-red-600 dark:text-red-400" : undefined} />
              <StatCard
                label={t("avgDuration")}
                value={stats.solverStats.avgDurationMs > 0 ? `${(stats.solverStats.avgDurationMs / 1000).toFixed(1)}s` : "—"}
              />
            </div>

            {/* Row 3: Storage */}
            <div className="grid grid-cols-3 gap-3">
              <StatCard label={t("totalAssignments")} value={stats.storageStats.totalAssignments.toLocaleString()} />
              <StatCard label={t("totalConstraints")} value={stats.storageStats.totalConstraints} />
              <StatCard label={t("totalTasks")} value={stats.storageStats.totalTasks} />
            </div>

            {/* Billing Test */}
            <BillingTestPanel />

            {/* Provider Health */}
            <ProviderHealthPanel />

            {/* Platform Settings */}
            <PlatformSettings />
          </div>
        )}
      </div>
    </AppShell>
  );
}
