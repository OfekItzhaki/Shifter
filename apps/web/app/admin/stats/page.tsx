"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import AppShell from "@/components/shell/AppShell";
import { useAuthStore } from "@/lib/store/authStore";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { getBurdenStats, BurdenStats } from "@/lib/api/schedule";
import StatsLeaderboard from "./_components/StatsLeaderboard";
import StatsPeopleTable from "./_components/StatsPeopleTable";

const card: React.CSSProperties = {
  background: "white",
  borderRadius: 14,
  border: "1px solid #e2e8f0",
  boxShadow: "0 1px 4px rgba(0,0,0,0.05)",
  padding: "1.25rem 1.5rem",
};

function SummaryCard({ label, value, sub }: { label: string; value: string | number; sub?: string }) {
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

export default function StatsPage() {
  const t = useTranslations("groups.stats_tab");
  const tAdmin = useTranslations("admin");
  const tStats = useTranslations("admin.stats");
  const { adminGroupId } = useAuthStore();
  const { currentSpaceId } = useSpaceStore();

  const [stats, setStats] = useState<BurdenStats | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!adminGroupId || !currentSpaceId) return;
    setLoading(true);
    setError(null);
    getBurdenStats(currentSpaceId)
      .then(setStats)
      .catch(() => setError(tStats("errorLoading")))
      .finally(() => setLoading(false));
  }, [currentSpaceId, adminGroupId]);

  return (
    <AppShell>
      <div style={{ maxWidth: 1100 }}>
        <div style={{ marginBottom: "1.5rem" }}>
          <h1 style={{ fontSize: "1.5rem", fontWeight: 800, color: "#0f172a", margin: 0 }}>{tStats("title")}</h1>
          <p style={{ fontSize: "0.875rem", color: "#64748b", margin: "0.25rem 0 0" }}>
            {tStats("subtitle")}
          </p>
        </div>

        {!adminGroupId && (
          <div style={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", padding: "5rem 0", textAlign: "center", background: "white", borderRadius: 16, border: "1px solid #e2e8f0" }}>
            <svg width="48" height="48" fill="none" viewBox="0 0 24 24" stroke="#cbd5e1" strokeWidth={1.5} style={{ marginBottom: 12 }}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
            </svg>
            <p style={{ fontSize: "0.875rem", color: "#64748b", margin: 0 }}>
              {tAdmin("adminRequired")}
            </p>
          </div>
        )}

        {adminGroupId && loading && (
          <div style={{ display: "flex", alignItems: "center", gap: 10, color: "#94a3b8", fontSize: "0.875rem", padding: "2rem 0" }}>
            <svg className="animate-spin" width="20" height="20" fill="none" viewBox="0 0 24 24">
              <circle style={{ opacity: 0.25 }} cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path style={{ opacity: 0.75 }} fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
            </svg>
            {tAdmin("loading")}
          </div>
        )}

        {error && <p style={{ color: "#dc2626", fontSize: "0.875rem" }}>{error}</p>}

        {stats && adminGroupId && (
          <>
            <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: "1rem", marginBottom: "1.5rem" }}>
              <SummaryCard label={t("activeMembers")} value={stats.totalPeople} />
              <SummaryCard label={t("totalAssignments")} value={stats.totalPublishedAssignments} />
              <SummaryCard label={t("avgPerPerson")} value={stats.averageAssignmentsPerPerson} />
              <SummaryCard label={tStats("publishedVersions")} value={stats.totalPublishedVersions} />
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem", marginBottom: "1.5rem" }}>
              <StatsLeaderboard title={t("mostAssignments")} entries={stats.mostAssignments} />
              <StatsLeaderboard title={t("mostHardTasks")} entries={stats.mostHatedTasks} valueColor="#dc2626" />
              <StatsLeaderboard title={t("highestBurdenScore")} entries={stats.highestBurdenScore} valueColor="#d97706" />
              <StatsLeaderboard title={t("bestBurdenBalance")} entries={stats.bestBurdenBalance} valueColor="#16a34a" />
            </div>

            <div style={{ marginBottom: "0.75rem" }}>
              <h2 style={{ fontSize: "1rem", fontWeight: 700, color: "#0f172a", margin: "0 0 0.75rem" }}>
                {t("detailByPerson")}
              </h2>
              <StatsPeopleTable people={stats.people} />
            </div>
          </>
        )}
      </div>
    </AppShell>
  );
}
