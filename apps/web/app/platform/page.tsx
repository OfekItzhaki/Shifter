"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { useRouter } from "next/navigation";
import AppShell from "@/components/shell/AppShell";
import { useAuthStore } from "@/lib/store/authStore";
import { getPlatformStats, PlatformStats } from "@/lib/api/platform";
import CouponManager from "@/components/platform/CouponManager";

const card: React.CSSProperties = {
  background: "white",
  borderRadius: 14,
  border: "1px solid #e2e8f0",
  boxShadow: "0 1px 4px rgba(0,0,0,0.05)",
  padding: "1.25rem 1.5rem",
  minWidth: 0,
};

function StatCard({ label, value, sub }: { label: string; value: string | number; sub?: string }) {
  return (
    <div style={card}>
      <p style={{ fontSize: "0.75rem", fontWeight: 600, color: "#94a3b8", textTransform: "uppercase", letterSpacing: "0.05em", margin: "0 0 0.375rem" }}>
        {label}
      </p>
      <p style={{ fontSize: "1.75rem", fontWeight: 800, color: "#0f172a", margin: 0, lineHeight: 1.1 }}>
        {value}
      </p>
      {sub && <p style={{ fontSize: "0.75rem", color: "#64748b", margin: "0.25rem 0 0" }}>{sub}</p>}
    </div>
  );
}

export default function PlatformPage() {
  const t = useTranslations("platform");
  const { isAuthenticated } = useAuthStore();
  const router = useRouter();

  const [stats, setStats] = useState<PlatformStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [accessDenied, setAccessDenied] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    setHydrated(true);
  }, []);

  useEffect(() => {
    if (!hydrated) return;
    if (!isAuthenticated) {
      router.push("/login");
      return;
    }

    setLoading(true);
    getPlatformStats()
      .then(setStats)
      .catch((err) => {
        if (err?.response?.status === 403) {
          setAccessDenied(true);
        } else {
          setError(err?.message ?? "Unknown error");
        }
      })
      .finally(() => setLoading(false));
  }, [hydrated, isAuthenticated, router]);

  if (!hydrated || !isAuthenticated) return null;

  return (
    <AppShell>
      <div style={{ maxWidth: 1100 }}>
        <div style={{ marginBottom: "1.5rem" }}>
          <h1 style={{ fontSize: "1.5rem", fontWeight: 800, color: "#0f172a", margin: 0 }}>
            {t("title")}
          </h1>
        </div>

        {loading && (
          <div style={{ display: "flex", alignItems: "center", gap: 10, color: "#94a3b8", fontSize: "0.875rem", padding: "2rem 0" }}>
            <svg className="animate-spin" width="20" height="20" fill="none" viewBox="0 0 24 24">
              <circle style={{ opacity: 0.25 }} cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path style={{ opacity: 0.75 }} fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
            </svg>
          </div>
        )}

        {accessDenied && (
          <div style={{
            display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center",
            padding: "5rem 0", textAlign: "center", background: "white", borderRadius: 16, border: "1px solid #e2e8f0"
          }}>
            <svg width="48" height="48" fill="none" viewBox="0 0 24 24" stroke="#dc2626" strokeWidth={1.5} style={{ marginBottom: 12 }}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4.5c-.77-.833-2.694-.833-3.464 0L3.34 16.5c-.77.833.192 2.5 1.732 2.5z" />
            </svg>
            <p style={{ fontSize: "0.875rem", color: "#dc2626", margin: 0, fontWeight: 600 }}>
              {t("accessDenied")}
            </p>
          </div>
        )}

        {error && !accessDenied && (
          <p style={{ color: "#dc2626", fontSize: "0.875rem" }}>{error}</p>
        )}

        {stats && (
          <>
            {/* Row 1: Users & Spaces */}
            <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: "1rem", marginBottom: "1.25rem" }}>
              <StatCard label={t("totalUsers")} value={stats.totalUsers} />
              <StatCard label={t("activeUsers7d")} value={stats.activeUsersLast7d} />
              <StatCard label={t("totalSpaces")} value={stats.totalSpaces} />
              <StatCard label={t("totalGroups")} value={stats.totalGroups} />
            </div>

            {/* Row 2: Solver */}
            <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: "1rem", marginBottom: "1.25rem" }}>
              <StatCard label={t("solverRuns24h")} value={stats.solverStats.totalRunsLast24h} />
              <StatCard label={t("completed")} value={stats.solverStats.completedLast24h} />
              <StatCard label={t("failed")} value={stats.solverStats.failedLast24h} />
              <StatCard
                label={t("avgDuration")}
                value={stats.solverStats.avgDurationMs > 0 ? stats.solverStats.avgDurationMs : "—"}
                sub={stats.solverStats.avgDurationMs > 0 ? t("ms") : undefined}
              />
            </div>

            {/* Row 3: Storage */}
            <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: "1rem", marginBottom: "2rem" }}>
              <StatCard label={t("totalAssignments")} value={stats.storageStats.totalAssignments} />
              <StatCard label={t("totalConstraints")} value={stats.storageStats.totalConstraints} />
              <StatCard label={t("totalTasks")} value={stats.storageStats.totalTasks} />
            </div>

            {/* Coupon Management */}
            <CouponManager />
          </>
        )}
      </div>
    </AppShell>
  );
}
