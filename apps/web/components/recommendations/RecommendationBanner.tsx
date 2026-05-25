"use client";

import { useTranslations } from "next-intl";
import { useRouter } from "next/navigation";
import { useRecommendationsForRun } from "@/lib/query/hooks/useRecommendations";
import { useDateFormat } from "@/lib/hooks/useDateFormat";

interface Props {
  spaceId: string;
  runId: string;
  groupId: string;
  subscriptionActive?: boolean;
}

/**
 * RecommendationBanner — displays an inline info banner when the recommendation
 * engine has detected staffing shortfalls for a solver run and suggests enabling
 * double shifts on specific tasks.
 *
 * Renders nothing if no recommendations exist for the run.
 *
 * Validates: Requirements 3.2
 */
export default function RecommendationBanner({ spaceId, runId, groupId, subscriptionActive = true }: Props) {
  const t = useTranslations("recommendations");
  const router = useRouter();
  const { fDateShort } = useDateFormat();
  const { data, isLoading } = useRecommendationsForRun(spaceId, runId);

  // Don't render if subscription is expired
  if (!subscriptionActive) return null;

  // Render nothing while loading or if no recommendations exist
  if (isLoading || !data || !data.recommendations || data.recommendations.length === 0) {
    return null;
  }

  const { totalUncoveredSlots, recommendations, remainingCount, affectedDateRange } = data;

  // Show up to 5 task names
  const displayedTasks = recommendations.slice(0, 5);
  const moreCount = remainingCount > 0 ? remainingCount : 0;

  // Format the affected date range for display
  const dateRangeDisplay = affectedDateRange || (() => {
    if (recommendations.length === 0) return "";
    const starts = recommendations.map(r => r.affectedDateStart);
    const ends = recommendations.map(r => r.affectedDateEnd);
    const earliest = starts.sort()[0];
    const latest = ends.sort().reverse()[0];
    if (earliest && latest) {
      return `${fDateShort(earliest)} – ${fDateShort(latest)}`;
    }
    return "";
  })();

  function handleNavigateToTasks() {
    // Navigate to the group page — the tasks tab will be activated by the group page
    // The integration task (13.2) will handle passing tab context if needed
    router.push(`/groups/${groupId}?tab=tasks`);
  }

  return (
    <div
      className="flex flex-col gap-2 rounded-xl border border-amber-200 bg-amber-50 p-4 sm:flex-row sm:items-center sm:gap-4"
      role="alert"
      aria-live="polite"
    >
      {/* Icon */}
      <div className="flex-shrink-0">
        <svg
          width="24"
          height="24"
          fill="none"
          viewBox="0 0 24 24"
          className="text-amber-600"
        >
          <path
            stroke="currentColor"
            strokeWidth={2}
            strokeLinecap="round"
            strokeLinejoin="round"
            d="M12 9v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
          />
        </svg>
      </div>

      {/* Content */}
      <div className="flex-1 min-w-0">
        <p className="text-sm font-semibold text-amber-900">
          {t("bannerTitle", { count: totalUncoveredSlots })}
        </p>
        <p className="mt-1 text-xs text-amber-800">
          {displayedTasks.map(r => r.taskName).join(", ")}
          {moreCount > 0 && (
            <span className="text-amber-600 font-medium">
              {" "}
              {t("andMore", { count: moreCount })}
            </span>
          )}
        </p>
        {dateRangeDisplay && (
          <p className="mt-0.5 text-xs text-amber-700">
            {t("affectedRange", { range: dateRangeDisplay })}
          </p>
        )}
      </div>

      {/* CTA Button */}
      <button
        onClick={handleNavigateToTasks}
        className="flex-shrink-0 rounded-lg bg-amber-600 px-4 py-2 text-xs font-semibold text-white shadow-sm hover:bg-amber-700 transition-colors focus:outline-none focus:ring-2 focus:ring-amber-500 focus:ring-offset-2"
      >
        {t("viewTasks")}
      </button>
    </div>
  );
}
