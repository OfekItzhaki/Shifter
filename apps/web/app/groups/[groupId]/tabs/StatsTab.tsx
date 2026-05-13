"use client";

import { useEffect, useState, useCallback } from "react";
import { useTranslations } from "next-intl";
import { getBurdenStats, BurdenStats } from "@/lib/api/schedule";
import { apiClient } from "@/lib/api/client";
import StatsLeaderboard from "@/app/admin/stats/_components/StatsLeaderboard";
import StatsPeopleTable from "@/app/admin/stats/_components/StatsPeopleTable";
import AssignmentsBarChart from "@/components/stats/AssignmentsBarChart";
import BurdenBreakdownChart from "@/components/stats/BurdenBreakdownChart";
import BurdenTrendChart from "@/components/stats/BurdenTrendChart";
import FairnessComparisonChart from "@/components/stats/FairnessComparisonChart";
import RotationProgressCard from "@/components/stats/RotationProgressCard";

interface Props {
  groupId: string;
  spaceId: string;
}

interface HistoricalDataPoint {
  personId: string;
  displayName: string;
  date: string;
  totalAssignments: number;
  hardCount: number;
  normalCount: number;
  easyCount: number;
  burdenScore: number;
}

type TimeRange = "7d" | "14d" | "30d" | "90d";

const TIME_RANGE_OPTIONS: { value: TimeRange; label: string }[] = [
  { value: "7d", label: "7 ימים" },
  { value: "14d", label: "14 ימים" },
  { value: "30d", label: "30 ימים" },
  { value: "90d", label: "90 ימים" },
];

function getDateRange(range: TimeRange): { startDate: string; endDate: string } {
  const end = new Date();
  const start = new Date();
  const days = parseInt(range);
  start.setDate(start.getDate() - days);
  return {
    startDate: start.toISOString().split("T")[0],
    endDate: end.toISOString().split("T")[0],
  };
}

export default function StatsTab({ spaceId, groupId }: Props) {
  const t = useTranslations("groups.stats_tab");
  const [stats, setStats] = useState<BurdenStats | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [timeRange, setTimeRange] = useState<TimeRange>("30d");
  const [historicalData, setHistoricalData] = useState<HistoricalDataPoint[]>([]);
  const [historicalLoading, setHistoricalLoading] = useState(false);

  // Fetch burden stats
  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    const safetyTimeout = setTimeout(() => {
      if (!cancelled) {
        setLoading(false);
        setError("timeout");
      }
    }, 15000);

    getBurdenStats(spaceId, groupId)
      .then(data => { if (!cancelled) setStats(data); })
      .catch(() => { if (!cancelled) setError("loadError"); })
      .finally(() => { if (!cancelled) setLoading(false); clearTimeout(safetyTimeout); });

    return () => { cancelled = true; clearTimeout(safetyTimeout); };
  }, [spaceId, groupId]);

  // Fetch historical data for graphs
  const fetchHistorical = useCallback(async () => {
    setHistoricalLoading(true);
    try {
      const { startDate, endDate } = getDateRange(timeRange);
      const { data } = await apiClient.get(
        `/spaces/${spaceId}/stats/historical/persons`,
        { params: { startDate, endDate, groupId } }
      );
      setHistoricalData(data.dataPoints ?? []);
    } catch {
      // Silently fail — graphs just won't show data
      setHistoricalData([]);
    } finally {
      setHistoricalLoading(false);
    }
  }, [spaceId, groupId, timeRange]);

  useEffect(() => {
    fetchHistorical();
  }, [fetchHistorical]);

  // Transform historical data for charts
  const assignmentsBarData = (() => {
    const byPerson: Record<string, { name: string; total: number }> = {};
    for (const dp of historicalData) {
      if (!byPerson[dp.personId]) {
        byPerson[dp.personId] = { name: dp.displayName, total: 0 };
      }
      byPerson[dp.personId].total += dp.totalAssignments;
    }
    return Object.values(byPerson);
  })();

  const burdenBreakdownData = (() => {
    const byPerson: Record<string, { name: string; hard: number; normal: number; easy: number }> = {};
    for (const dp of historicalData) {
      if (!byPerson[dp.personId]) {
        byPerson[dp.personId] = { name: dp.displayName, hard: 0, normal: 0, easy: 0 };
      }
      byPerson[dp.personId].hard += dp.hardCount;
      byPerson[dp.personId].normal += dp.normalCount;
      byPerson[dp.personId].easy += dp.easyCount;
    }
    return Object.values(byPerson);
  })();

  const { trendData, trendPeople } = (() => {
    const dates = [...new Set(historicalData.map(dp => dp.date))].sort();
    const people = [...new Set(historicalData.map(dp => dp.displayName))];
    const data = dates.map(date => {
      const row: { date: string; [personName: string]: number | string } = { date };
      for (const person of people) {
        const dp = historicalData.find(d => d.date === date && d.displayName === person);
        row[person] = dp?.burdenScore ?? 0;
      }
      return row;
    });
    return { trendData: data, trendPeople: people };
  })();

  const fairnessData = (() => {
    if (assignmentsBarData.length === 0) return [];
    // Compute burden score per person and deviation from average
    const byPerson: Record<string, { name: string; score: number }> = {};
    for (const dp of historicalData) {
      if (!byPerson[dp.personId]) {
        byPerson[dp.personId] = { name: dp.displayName, score: 0 };
      }
      byPerson[dp.personId].score += dp.burdenScore;
    }
    const scores = Object.values(byPerson);
    if (scores.length === 0) return [];
    const avg = scores.reduce((s, p) => s + p.score, 0) / scores.length;
    return scores.map(p => ({
      name: p.name,
      deviation: Math.round((p.score - avg) * 10) / 10,
    }));
  })();

  if (loading) {
    return (
      <div className="flex items-center gap-3 py-12 text-slate-400 text-sm">
        <svg className="animate-spin h-5 w-5" fill="none" viewBox="0 0 24 24">
          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
        </svg>
        {t("loading")}
      </div>
    );
  }

  if (error) {
    const errorMessage = error === "timeout" ? t("connectionTimeout") : t("errorLoading");
    return <p className="text-sm text-red-600 py-8">{errorMessage}</p>;
  }

  if (!stats || stats.people.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-16 bg-white rounded-xl border border-slate-200">
        <p className="text-slate-400 text-sm">{t("noStats")}</p>
        <p className="text-slate-300 text-xs mt-1">{t("publishToSeeStats")}</p>
      </div>
    );
  }

  // Compute group-scoped totals from the filtered people data
  const totalAssignments = stats.people.reduce((s, p) => s + p.totalAssignmentsAllTime, 0);
  const avgPerPerson = stats.people.length > 0 ? Math.round(totalAssignments / stats.people.length) : 0;
  const totalHard = stats.people.reduce((s, p) => s + (p.hardTasks7d ?? 0), 0);

  const hasHistoricalData = historicalData.length > 0;

  return (
    <div className="space-y-5">
      {/* Summary — group-scoped totals */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {[
          { label: t("activeMembers"), value: stats.people.length },
          { label: t("totalAssignments"), value: totalAssignments },
          { label: t("avgPerPerson"), value: avgPerPerson },
          { label: t("hardTasks"), value: totalHard },
        ].map(c => (
          <div key={c.label} className="bg-white border border-slate-200 rounded-xl p-4">
            <p className="text-xs font-semibold text-slate-400 uppercase tracking-wide mb-1">{c.label}</p>
            <p className="text-2xl font-bold text-slate-900">{c.value}</p>
          </div>
        ))}
      </div>

      {/* Time Range Selector */}
      <div className="flex items-center gap-2">
        <span className="text-xs text-slate-500 font-medium">טווח זמן:</span>
        {TIME_RANGE_OPTIONS.map(opt => (
          <button
            key={opt.value}
            onClick={() => setTimeRange(opt.value)}
            className={`px-3 py-1.5 text-xs font-medium rounded-lg border transition-colors ${
              timeRange === opt.value
                ? "bg-blue-50 text-blue-700 border-blue-200"
                : "bg-white text-slate-600 border-slate-200 hover:bg-slate-50"
            }`}
          >
            {opt.label}
          </button>
        ))}
        {historicalLoading && (
          <svg className="animate-spin h-4 w-4 text-slate-400 mr-2" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
        )}
      </div>

      {/* Graphs Section */}
      {hasHistoricalData ? (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <div className="bg-white border border-slate-200 rounded-xl p-4">
            <AssignmentsBarChart data={assignmentsBarData} timeWindow={timeRange} />
          </div>
          <div className="bg-white border border-slate-200 rounded-xl p-4">
            <BurdenBreakdownChart data={burdenBreakdownData} />
          </div>
          <div className="bg-white border border-slate-200 rounded-xl p-4">
            <BurdenTrendChart data={trendData} people={trendPeople} />
          </div>
          <div className="bg-white border border-slate-200 rounded-xl p-4">
            <FairnessComparisonChart data={fairnessData} />
          </div>
        </div>
      ) : (
        !historicalLoading && (
          <div className="bg-white border border-slate-200 rounded-xl p-8 text-center">
            <p className="text-sm text-slate-400">
              אין מספיק נתונים היסטוריים להצגת גרפים
            </p>
            <p className="text-xs text-slate-300 mt-1">
              פרסם לפחות שני לוחות זמנים כדי לראות מגמות
            </p>
          </div>
        )
      )}

      {/* Rotation Progress (only for army-template groups) */}
      <RotationProgressCard spaceId={spaceId} groupId={groupId} />

      {/* Leaderboards */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <StatsLeaderboard title={t("mostAssignments")} entries={stats.mostAssignments} />
        <StatsLeaderboard title={t("mostHardTasks")} entries={stats.mostHatedTasks} valueColor="#dc2626" />
        <StatsLeaderboard title={t("highestBurdenScore")} entries={stats.highestBurdenScore} valueColor="#d97706" />
        <StatsLeaderboard title={t("bestBurdenBalance")} entries={stats.bestBurdenBalance} valueColor="#16a34a" />
      </div>

      {/* People table */}
      <div>
        <h2 className="text-sm font-semibold text-slate-700 mb-3">{t("detailByPerson")}</h2>
        <StatsPeopleTable people={stats.people} />
      </div>
    </div>
  );
}
