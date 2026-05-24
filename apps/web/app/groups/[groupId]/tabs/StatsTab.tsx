"use client";

import { useEffect, useState, useCallback } from "react";
import { useTranslations } from "next-intl";
import { getBurdenStats, BurdenStats } from "@/lib/api/schedule";
import { apiClient } from "@/lib/api/client";
import { getCumulativeStats, CumulativePersonStats, CumulativeStatsResponse } from "@/lib/api/stats";
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

type TimeRange = "7d" | "14d" | "30d" | "90d" | "period";

const TIME_RANGE_OPTIONS: { value: TimeRange; label: string }[] = [
  { value: "7d", label: "7 ימים" },
  { value: "14d", label: "14 ימים" },
  { value: "30d", label: "30 ימים" },
  { value: "90d", label: "90 ימים" },
  { value: "period", label: "כל התקופה" },
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
  const [cumulativeStats, setCumulativeStats] = useState<CumulativeStatsResponse | null>(null);
  const [cumulativeLoading, setCumulativeLoading] = useState(false);

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

  // Fetch cumulative stats from the new endpoint
  useEffect(() => {
    let cancelled = false;
    setCumulativeLoading(true);
    getCumulativeStats(spaceId, groupId, timeRange)
      .then(data => { if (!cancelled) setCumulativeStats(data); })
      .catch(() => { if (!cancelled) setCumulativeStats(null); })
      .finally(() => { if (!cancelled) setCumulativeLoading(false); });
    return () => { cancelled = true; };
  }, [spaceId, groupId, timeRange]);

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
    return (
      <div className="flex flex-col items-center justify-center py-16 bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-700">
        <div className="flex items-center justify-center w-12 h-12 rounded-full bg-slate-100 dark:bg-slate-800 mb-3">
          <svg width={24} height={24} viewBox="0 0 24 24" fill="none" className="text-slate-400 dark:text-slate-500" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round">
            <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
            <line x1="12" y1="9" x2="12" y2="13" />
            <circle cx="12" cy="17" r="0.5" fill="currentColor" stroke="none" />
          </svg>
        </div>
        <p className="text-sm text-slate-600 dark:text-slate-400">{errorMessage}</p>
      </div>
    );
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
                ? "bg-sky-50 text-sky-700 border-sky-200"
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
        {cumulativeLoading && (
          <svg className="animate-spin h-4 w-4 text-slate-400 mr-2" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
        )}
      </div>

      {/* Cumulative Stats Table */}
      {cumulativeStats && cumulativeStats.people.length > 0 && (
        <div className="bg-white border border-slate-200 rounded-xl overflow-hidden">
          <div className="px-4 py-3 border-b border-slate-100 flex items-center justify-between">
            <h3 className="text-sm font-semibold text-slate-700">סטטיסטיקות מצטברות</h3>
            {cumulativeStats.periodStatus && (
              <span className="text-xs text-slate-400">
                תקופה: {cumulativeStats.periodStatus === "active" ? "פעילה" : "סגורה"}
              </span>
            )}
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-slate-50 text-slate-500 text-xs">
                <tr>
                  <th className="px-4 py-2 text-right font-medium">שם</th>
                  <th className="px-3 py-2 text-center font-medium">משימות</th>
                  <th className="px-3 py-2 text-center font-medium">קשות</th>
                  <th className="px-3 py-2 text-center font-medium">מטבח</th>
                  <th className="px-3 py-2 text-center font-medium">לילה</th>
                  <th className="px-3 py-2 text-center font-medium">ציון עומס</th>
                  <th className="px-3 py-2 text-center font-medium">שעות</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {cumulativeStats.people.map(person => (
                  <tr key={person.personId} className="hover:bg-slate-50">
                    <td className="px-4 py-2.5 text-right font-medium text-slate-700">
                      {person.displayName}
                    </td>
                    <td className="px-3 py-2.5 text-center text-slate-600">{person.totalAssignments}</td>
                    <td className="px-3 py-2.5 text-center text-red-600 font-medium">{person.hardTasks}</td>
                    <td className="px-3 py-2.5 text-center text-slate-600">{person.kitchenCount}</td>
                    <td className="px-3 py-2.5 text-center text-slate-600">{person.nightMissions}</td>
                    <td className="px-3 py-2.5 text-center text-amber-600 font-medium">{person.dislikedHatedScore}</td>
                    <td className="px-3 py-2.5 text-center text-slate-600">{Math.round(person.totalHoursAssigned)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

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
        <StatsLeaderboard title={t("mostAssignments")} entries={stats.mostAssignments ?? []} />
        <StatsLeaderboard title={t("mostHardTasks")} entries={stats.mostHatedTasks ?? []} valueColor="#dc2626" />
        <StatsLeaderboard title={t("highestBurdenScore")} entries={stats.highestBurdenScore ?? []} valueColor="#d97706" />
        <StatsLeaderboard title={t("bestBurdenBalance")} entries={stats.bestBurdenBalance ?? []} valueColor="#16a34a" />
      </div>

      {/* People table */}
      <div>
        <h2 className="text-sm font-semibold text-slate-700 mb-3">{t("detailByPerson")}</h2>
        <StatsPeopleTable people={stats.people ?? []} />
      </div>
    </div>
  );
}
