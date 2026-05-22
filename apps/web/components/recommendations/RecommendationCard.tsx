"use client";

import { useTranslations } from "next-intl";
import { useRouter } from "next/navigation";
import { useRecommendations, useDismissRecommendation } from "@/lib/query/hooks/useRecommendations";

interface Props {
  spaceId: string;
  groupId: string;
}

/**
 * RecommendationCard — a passive informational card displayed above the emergency
 * freeze section. Shows which tasks have uncovered slots and links to the Tasks tab.
 *
 * Renders nothing when no active recommendations exist or while loading.
 *
 * Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 3.1
 */
export default function RecommendationCard({ spaceId, groupId }: Props) {
  const t = useTranslations("recommendations");
  const router = useRouter();
  const { data: recommendations, isLoading } = useRecommendations(spaceId, groupId);
  const dismissMutation = useDismissRecommendation(spaceId);

  // Render nothing while loading or if no active recommendations exist
  if (isLoading || !recommendations || recommendations.length === 0) {
    return null;
  }

  // Aggregate task names and total uncovered slot count
  const taskNames = recommendations.map((r) => r.taskName).join(", ");
  const totalUncoveredSlots = recommendations.reduce(
    (sum, r) => sum + r.totalUncoveredSlotsInRun,
    0
  );

  function handleGoToTasks() {
    router.push(`/groups/${groupId}?tab=tasks`);
  }

  function handleDismiss() {
    // Dismiss all active recommendations
    for (const rec of recommendations!) {
      dismissMutation.mutate(rec.id);
    }
  }

  return (
    <div
      className="flex flex-col gap-2 rounded-xl border border-sky-200 bg-sky-50 p-4 sm:flex-row sm:items-center sm:gap-4"
      role="status"
      aria-live="polite"
    >
      {/* Info icon */}
      <div className="flex-shrink-0">
        <svg
          width="24"
          height="24"
          fill="none"
          viewBox="0 0 24 24"
          className="text-sky-600"
        >
          <path
            stroke="currentColor"
            strokeWidth={2}
            strokeLinecap="round"
            strokeLinejoin="round"
            d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
          />
        </svg>
      </div>

      {/* Content */}
      <div className="flex-1 min-w-0">
        <p className="text-sm font-semibold text-sky-900">
          {t("cardTitle")}
        </p>
        <p className="mt-1 text-xs text-sky-800">
          {t("cardDescription", { taskNames, count: totalUncoveredSlots })}
        </p>
        <p className="mt-0.5 text-xs text-sky-700">
          {t("slotsCount", { count: totalUncoveredSlots })}
        </p>
      </div>

      {/* Action buttons */}
      <div className="flex items-center gap-2 flex-shrink-0">
        <button
          onClick={handleGoToTasks}
          className="rounded-lg bg-sky-600 px-4 py-2 text-xs font-semibold text-white shadow-sm hover:bg-sky-700 transition-colors focus:outline-none focus:ring-2 focus:ring-sky-500 focus:ring-offset-2"
        >
          {t("goToTasks")}
        </button>
        <button
          onClick={handleDismiss}
          disabled={dismissMutation.isPending}
          className="rounded-lg border border-sky-200 bg-white px-4 py-2 text-xs font-medium text-sky-700 hover:bg-sky-100 transition-colors focus:outline-none focus:ring-2 focus:ring-sky-500 focus:ring-offset-2 disabled:opacity-50"
        >
          {t("dismiss")}
        </button>
      </div>
    </div>
  );
}
