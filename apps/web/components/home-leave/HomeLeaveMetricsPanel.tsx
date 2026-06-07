"use client";

export interface HomeLeaveMetric {
  personId: string;
  personName: string;
  totalBaseHours: number;
  totalHomeHours: number;
  baseTimeRatio: number;
  leaveSlotCount: number;
}

interface HomeLeaveMetricsPanelProps {
  metrics: HomeLeaveMetric[] | null | undefined;
}

/**
 * Displays per-person base-time vs. home-time statistics for a closed-base group.
 * Includes a horizontal stacked bar chart per person and a fairness warning
 * when the max-min base_time_ratio spread exceeds 15 percentage points.
 *
 * Hides entirely when no metrics data is available.
 */
export default function HomeLeaveMetricsPanel({ metrics }: HomeLeaveMetricsPanelProps) {
  if (!metrics || metrics.length === 0) {
    return null;
  }

  // Sort people alphabetically by name
  const sorted = [...metrics].sort((a, b) => a.personName.localeCompare(b.personName, "he"));

  // Calculate fairness warning: show when max - min base_time_ratio > 15pp
  const ratios = sorted.map((m) => m.baseTimeRatio);
  const maxRatio = Math.max(...ratios);
  const minRatio = Math.min(...ratios);
  const fairnessSpread = maxRatio - minRatio;
  const showFairnessWarning = metrics.length >= 2 && fairnessSpread - 0.15 > Number.EPSILON;

  return (
    <div className="bg-white border border-slate-200 rounded-xl p-5 space-y-4">
      {/* Panel header */}
      <div className="flex items-center gap-2">
        <svg
          className="w-5 h-5 text-sky-500"
          fill="none"
          viewBox="0 0 24 24"
          strokeWidth={1.5}
          stroke="currentColor"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            d="M3 13.125C3 12.504 3.504 12 4.125 12h2.25c.621 0 1.125.504 1.125 1.125v6.75C7.5 20.496 6.996 21 6.375 21h-2.25A1.125 1.125 0 013 19.875v-6.75zM9.75 8.625c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125v11.25c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 01-1.125-1.125V8.625zM16.5 4.125c0-.621.504-1.125 1.125-1.125h2.25C20.496 3 21 3.504 21 4.125v15.75c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 01-1.125-1.125V4.125z"
          />
        </svg>
        <h3 className="text-sm font-semibold text-slate-800">זמן בבסיס / בבית</h3>
      </div>

      {/* Fairness warning */}
      {showFairnessWarning && (
        <div className="flex items-center gap-2 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
          <svg
            className="w-4 h-4 text-amber-600 flex-shrink-0"
            fill="none"
            viewBox="0 0 24 24"
            strokeWidth={1.5}
            stroke="currentColor"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z"
            />
          </svg>
          <span className="text-xs font-medium text-amber-700">
            פער הוגנות גבוה — הפרש של{" "}
            {(fairnessSpread * 100).toFixed(1)}% בין האדם עם הכי הרבה זמן בבסיס לבין הכי פחות
          </span>
        </div>
      )}

      {/* Per-person stats table */}
      <div className="space-y-3">
        {sorted.map((metric) => {
          const basePercent = metric.baseTimeRatio * 100;
          const homePercent = 100 - basePercent;

          return (
            <div key={metric.personId} className="space-y-1.5">
              {/* Person info row */}
              <div className="flex items-center justify-between text-sm">
                <span className="font-medium text-slate-800">{metric.personName}</span>
                <div className="flex items-center gap-3 text-xs text-slate-500">
                  <span title="שעות בבסיס">
                    <span className="inline-block w-2 h-2 rounded-full bg-sky-500 mr-1" />
                    {Math.round(metric.totalBaseHours)} ש׳
                  </span>
                  <span title="שעות בבית">
                    <span className="inline-block w-2 h-2 rounded-full bg-emerald-500 mr-1" />
                    {Math.round(metric.totalHomeHours)} ש׳
                  </span>
                  <span title="אחוז זמן בבסיס" className="font-medium text-slate-700">
                    {(metric.baseTimeRatio * 100).toFixed(1)}%
                  </span>
                  <span title="מספר חופשות" className="text-slate-400">
                    {metric.leaveSlotCount} חופשות
                  </span>
                </div>
              </div>

              {/* Stacked bar chart */}
              <div
                className="flex h-3 w-full rounded-full overflow-hidden bg-slate-100"
                role="img"
                aria-label={`${metric.personName}: ${basePercent.toFixed(1)}% בבסיס, ${homePercent.toFixed(1)}% בבית`}
              >
                <div
                  className="bg-sky-500 transition-all"
                  style={{ width: `${basePercent}%` }}
                  title={`בבסיס: ${basePercent.toFixed(1)}%`}
                />
                <div
                  className="bg-emerald-500 transition-all"
                  style={{ width: `${homePercent}%` }}
                  title={`בבית: ${homePercent.toFixed(1)}%`}
                />
              </div>
            </div>
          );
        })}
      </div>

      {/* Legend */}
      <div className="flex items-center gap-4 pt-2 border-t border-slate-100">
        <div className="flex items-center gap-1.5 text-xs text-slate-500">
          <span className="w-3 h-3 rounded bg-sky-500" />
          <span>זמן בבסיס</span>
        </div>
        <div className="flex items-center gap-1.5 text-xs text-slate-500">
          <span className="w-3 h-3 rounded bg-emerald-500" />
          <span>זמן בבית</span>
        </div>
      </div>
    </div>
  );
}
