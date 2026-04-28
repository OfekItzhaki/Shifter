"use client";

import { useEffect, useState } from "react";
import AppShell from "@/components/shell/AppShell";
import { useAuthStore } from "@/lib/store/authStore";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { getBurdenStats, BurdenStats } from "@/lib/api/schedule";
import StatsLeaderboard from "./_components/StatsLeaderboard";
import StatsPeopleTable from "./_components/StatsPeopleTable";
import { useRouter } from "next/navigation";

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
  const { adminGroupId } = useAuthStore();
  const { currentSpaceId } = useSpaceStore();
  const router = useRouter();

  const [stats, setStats] = useState<BurdenStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!adminGroupId) { router.replace("/profile"); return; }
    if (!currentSpaceId) return;
    getBurdenStats(currentSpaceId)
      .then(setStats)
      .catch(() => setError("שגיאה בטעינת הסטטיסטיקות"))
      .finally(() => setLoading(false));
  }, [currentSpaceId, adminGroupId]);

  if (!adminGroupId) return null;

  return (
    <AppShell>
      <div style={{ maxWidth: 1100, direction: "rtl" }}>
        <div style={{ marginBottom: "1.5rem" }}>
          <h1 style={{ fontSize: "1.5rem", fontWeight: 800, color: "#0f172a", margin: 0 }}>סטטיסטיקות</h1>
          <p style={{ fontSize: "0.875rem", color: "#64748b", margin: "0.25rem 0 0" }}>
            נתוני עומס והוגנות לפי אדם
          </p>
        </div>

        {loading && (
          <div style={{ display: "flex", alignItems: "center", gap: 10, color: "#94a3b8", fontSize: "0.875rem", padding: "2rem 0" }}>
            <svg className="animate-spin" width="20" height="20" fill="none" viewBox="0 0 24 24">
              <circle style={{ opacity: 0.25 }} cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path style={{ opacity: 0.75 }} fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
            </svg>
            טוען...
          </div>
        )}

        {error && <p style={{ color: "#dc2626", fontSize: "0.875rem" }}>{error}</p>}

        {stats && (
          <>
            {/* Summary cards */}
            <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: "1rem", marginBottom: "1.5rem" }}>
              <SummaryCard label="אנשים פעילים" value={stats.totalPeople} />
              <SummaryCard label="שיבוצים סה״כ" value={stats.totalPublishedAssignments} />
              <SummaryCard label="ממוצע לאדם" value={stats.averageAssignmentsPerPerson} sub="שיבוצים" />
              <SummaryCard label="גרסאות פורסמו" value={stats.totalPublishedVersions} />
            </div>

            {/* Leaderboards 2x2 */}
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem", marginBottom: "1.5rem" }}>
              <StatsLeaderboard title="הכי הרבה שיבוצים" entries={stats.mostAssignments} />
              <StatsLeaderboard title="הכי הרבה משימות שנואות" entries={stats.mostHatedTasks} valueColor="#dc2626" />
              <StatsLeaderboard title="ציון עומס גבוה ביותר" entries={stats.highestBurdenScore} valueColor="#d97706" />
              <StatsLeaderboard title="איזון עומס הטוב ביותר" entries={stats.bestBurdenBalance} valueColor="#16a34a" />
            </div>

            {/* People table */}
            <div style={{ marginBottom: "0.75rem" }}>
              <h2 style={{ fontSize: "1rem", fontWeight: 700, color: "#0f172a", margin: "0 0 0.75rem" }}>
                פירוט לפי אדם
              </h2>
              <StatsPeopleTable people={stats.people} />
            </div>
          </>
        )}
      </div>
    </AppShell>
  );
}
