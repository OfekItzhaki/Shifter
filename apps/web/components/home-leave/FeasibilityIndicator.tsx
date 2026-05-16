"use client";

import { useTranslations } from "next-intl";

export interface FeasibilityResult {
  /** Whether the configuration is feasible */
  isFeasible: boolean;
  /** Maximum feasible home days (suggestion when feasible) */
  maxFeasibleHomeDays?: number | null;
  /** Localized explanation when not feasible */
  reason?: string | null;
}

interface FeasibilityIndicatorProps {
  /** The feasibility result from the API */
  feasibilityResult: FeasibilityResult | null;
  /** Whether the result is currently loading */
  isLoading?: boolean;
}

/**
 * Displays feasibility feedback for the current home-leave configuration.
 * Green indicator when feasible, red when not feasible with explanation.
 */
export default function FeasibilityIndicator({
  feasibilityResult,
  isLoading = false,
}: FeasibilityIndicatorProps) {
  const t = useTranslations("homeLeave.feasibility");

  if (isLoading) {
    return (
      <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-slate-50 border border-slate-200">
        <div className="w-2 h-2 rounded-full bg-slate-300 animate-pulse" />
        <span className="text-xs text-slate-400">{t("checking")}</span>
      </div>
    );
  }

  if (!feasibilityResult) return null;

  if (feasibilityResult.isFeasible) {
    return (
      <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-emerald-50 border border-emerald-200">
        <div className="w-2 h-2 rounded-full bg-emerald-500" />
        <span className="text-xs text-emerald-700 font-medium">
          {t("feasible")}
        </span>
        {feasibilityResult.maxFeasibleHomeDays != null && (
          <span className="text-xs text-emerald-600">
            {t("maxHomeDays", { days: feasibilityResult.maxFeasibleHomeDays })}
          </span>
        )}
      </div>
    );
  }

  return (
    <div className="space-y-2">
      <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-red-50 border border-red-200">
        <div className="w-2 h-2 rounded-full bg-red-500 flex-shrink-0" />
        <span className="text-xs text-red-700 font-medium">
          {feasibilityResult.reason || t("notFeasible")}
        </span>
      </div>
      <p className="text-xs text-slate-500 px-1">
        {t("suggestions")}
      </p>
    </div>
  );
}
