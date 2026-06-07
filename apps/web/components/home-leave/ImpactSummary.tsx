"use client";

import type { HomeLeavePreviewResponse } from "@/lib/api/homeLeave";

interface ImpactSummaryProps {
  data: HomeLeavePreviewResponse["preview"] | null;
  isLoading: boolean;
  error: string | null;
}

export default function ImpactSummary({ data, isLoading, error }: ImpactSummaryProps) {
  // Loading state
  if (isLoading) {
    return (
      <div className="flex items-center gap-2 py-4 text-slate-400 text-sm">
        <svg className="animate-spin h-4 w-4" fill="none" viewBox="0 0 24 24">
          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
        </svg>
        מחשב תצוגה מקדימה...
      </div>
    );
  }

  // Error state
  if (error) {
    return (
      <div className="py-3 px-4 bg-red-50 border border-red-200 rounded-xl text-sm text-red-600">
        {error}
      </div>
    );
  }

  // No data yet
  if (!data) return null;

  // No solution found
  if (data.status === "no_solution") {
    return (
      <div className="py-3 px-4 bg-slate-50 border border-slate-200 rounded-xl text-center">
        <p className="text-xs text-slate-500">
          התצוגה המקדימה לא זמינה — הפעל את הסולבר לקבלת תוצאות מלאות
        </p>
      </div>
    );
  }

  const total = data.peopleHomeCount + data.peopleAtBaseCount;
  const homePercent = total > 0 ? Math.round((data.peopleHomeCount / total) * 100) : 0;
  const basePercent = 100 - homePercent;

  const totalGapHours = data.coverageGaps.reduce((sum, gap) => {
    const start = new Date(gap.startsAt).getTime();
    const end = new Date(gap.endsAt).getTime();
    return sum + (end - start) / (1000 * 60 * 60);
  }, 0);

  const minAvailable = data.coverageGaps.length > 0
    ? Math.min(...data.coverageGaps.map(g => g.availableCount))
    : total;

  const fairnessSpread = data.fairnessSpread;
  const fairnessPercent = Math.round(fairnessSpread * 100);
  const fairnessWarning = fairnessSpread - 0.15 > Number.EPSILON;

  return (
    <div className="space-y-3 bg-white border border-slate-200 rounded-xl p-4">
      {/* Status indicator */}
      {data.status === "feasible" && (
        <div className="flex items-center gap-1.5 text-xs text-slate-400">
          <span className="w-1.5 h-1.5 rounded-full bg-amber-400" />
          תוצאה משוערת
        </div>
      )}

      {/* Metrics grid */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <div className="text-center">
          <p className="text-2xl font-bold text-emerald-600">{data.peopleHomeCount}</p>
          <p className="text-xs text-slate-500">אנשים בבית</p>
        </div>
        <div className="text-center">
          <p className="text-2xl font-bold text-sky-600">{data.peopleAtBaseCount}</p>
          <p className="text-xs text-slate-500">אנשים בבסיס</p>
        </div>
        <div className="text-center">
          <p className="text-2xl font-bold text-slate-700">{data.totalHomeLeaveSlots}</p>
          <p className="text-xs text-slate-500">סה״כ חופשות</p>
        </div>
        <div className="text-center">
          <p className={`text-2xl font-bold ${fairnessWarning ? "text-amber-600" : "text-slate-700"}`}>
            {fairnessPercent}%
          </p>
          <p className={`text-xs ${fairnessWarning ? "text-amber-500" : "text-slate-500"}`}>
            פער הוגנות
            {fairnessWarning && " ⚠️"}
          </p>
        </div>
      </div>

      {/* Bar visualization */}
      <div className="space-y-1">
        <div className="flex h-3 rounded-full overflow-hidden">
          <div
            className="bg-emerald-400 transition-all duration-300"
            style={{ width: `${homePercent}%` }}
            title={`בבית: ${homePercent}%`}
          />
          <div
            className="bg-sky-400 transition-all duration-300"
            style={{ width: `${basePercent}%` }}
            title={`בבסיס: ${basePercent}%`}
          />
        </div>
        <div className="flex justify-between text-xs text-slate-400">
          <span>🏠 {homePercent}%</span>
          <span>🏢 {basePercent}%</span>
        </div>
      </div>

      {/* Coverage status */}
      {data.coverageGaps.length === 0 ? (
        <div className="flex items-center gap-1.5 text-xs text-emerald-600 font-medium">
          <span className="w-2 h-2 rounded-full bg-emerald-500" />
          כיסוי מלא
        </div>
      ) : (
        <div className="flex items-center gap-1.5 text-xs text-amber-600 font-medium">
          <span className="w-2 h-2 rounded-full bg-amber-500" />
          {Math.round(totalGapHours)} שעות ללא כיסוי מלא (מינימום {minAvailable} אנשים זמינים)
        </div>
      )}

      {/* Solver time */}
      {data.solverTimeMs > 0 && (
        <p className="text-xs text-slate-300 text-left">
          {(data.solverTimeMs / 1000).toFixed(1)}s
        </p>
      )}
    </div>
  );
}
