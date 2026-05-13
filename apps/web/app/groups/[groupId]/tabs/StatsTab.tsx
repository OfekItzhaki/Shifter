"use client";

import { useEffect, useState } from "react";
import { getBurdenStats, BurdenStats } from "@/lib/api/schedule";
import StatsLeaderboard from "@/app/admin/stats/_components/StatsLeaderboard";
import StatsPeopleTable from "@/app/admin/stats/_components/StatsPeopleTable";

interface Props {
  groupId: string;
  spaceId: string;
}

export default function StatsTab({ spaceId, groupId }: Props) {
  const [stats, setStats] = useState<BurdenStats | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    const safetyTimeout = setTimeout(() => {
      if (!cancelled) {
        setLoading(false);
        setError("Connection timeout — try again");
      }
    }, 15000);

    // Pass groupId so the backend only returns stats for this group's members
    getBurdenStats(spaceId, groupId)
      .then(data => { if (!cancelled) setStats(data); })
      .catch(() => { if (!cancelled) setError("Error loading statistics"); })
      .finally(() => { if (!cancelled) setLoading(false); clearTimeout(safetyTimeout); });

    return () => { cancelled = true; clearTimeout(safetyTimeout); };
  }, [spaceId, groupId]);

  if (loading) {
    return (
      <div className="flex items-center gap-3 py-12 text-slate-400 text-sm">
        <svg className="animate-spin h-5 w-5" fill="none" viewBox="0 0 24 24">
          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
        </svg>
        Loading statistics...
      </div>
    );
  }

  if (error) return <p className="text-sm text-red-600 py-8">{error}</p>;

  if (!stats || stats.people.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-16 bg-white rounded-xl border border-slate-200">
        <p className="text-slate-400 text-sm">No statistics for this group</p>
        <p className="text-slate-300 text-xs mt-1">Publish a schedule to see data</p>
      </div>
    );
  }

  // Compute group-scoped totals from the filtered people data
  const totalAssignments = stats.people.reduce((s, p) => s + p.totalAssignmentsAllTime, 0);
  const avgPerPerson = stats.people.length > 0 ? Math.round(totalAssignments / stats.people.length) : 0;
  const totalHard = stats.people.reduce((s, p) => s + (p.hardTasksAllTime ?? p.hatedTasksAllTime ?? 0), 0);

  return (
    <div className="space-y-5">
      {/* Summary — group-scoped totals */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {[
          { label: "Active Members", value: stats.people.length },
          { label: "Total Assignments", value: totalAssignments },
          { label: "Avg per Person", value: avgPerPerson },
          { label: "Hard Tasks", value: totalHard },
        ].map(c => (
          <div key={c.label} className="bg-white border border-slate-200 rounded-xl p-4">
            <p className="text-xs font-semibold text-slate-400 uppercase tracking-wide mb-1">{c.label}</p>
            <p className="text-2xl font-bold text-slate-900">{c.value}</p>
          </div>
        ))}
      </div>

      {/* Leaderboards */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <StatsLeaderboard title="Most Assignments" entries={stats.mostAssignments} />
        <StatsLeaderboard title="Most Hard Tasks" entries={stats.mostHatedTasks} valueColor="#dc2626" />
        <StatsLeaderboard title="Highest Burden Score" entries={stats.highestBurdenScore} valueColor="#d97706" />
        <StatsLeaderboard title="Best Burden Balance" entries={stats.bestBurdenBalance} valueColor="#16a34a" />
      </div>

      {/* People table */}
      <div>
        <h2 className="text-sm font-semibold text-slate-700 mb-3">Detail by Person</h2>
        <StatsPeopleTable people={stats.people} />
      </div>
    </div>
  );
}
