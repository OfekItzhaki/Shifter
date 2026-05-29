"use client";

import { useTranslations } from "next-intl";

export interface PickerHeaderProps {
  /** The currently selected group name, or null if no group is selected */
  groupName: string | null;
  /** Callback to navigate back to the group selector */
  onBack: () => void;
  /** Callback to trigger a data refresh */
  onRefresh: () => void;
  /** Whether a refresh request is currently in progress */
  refreshing: boolean;
}

/**
 * Minimal mobile header for the /pick route.
 * Displays the group name, a back-navigation button, and a refresh button.
 * No sidebar navigation or desktop shell elements.
 */
export default function PickerHeader({
  groupName,
  onBack,
  onRefresh,
  refreshing,
}: PickerHeaderProps) {
  const t = useTranslations("pick");

  return (
    <header className="sticky top-0 z-20 flex items-center gap-2 px-3 py-2 bg-white dark:bg-slate-900 border-b border-slate-200 dark:border-slate-700">
      {/* Back button — min 44x44 tap target */}
      <button
        onClick={onBack}
        aria-label={t("back")}
        className="flex items-center justify-center min-w-[44px] min-h-[44px] rounded-lg text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
      >
        <svg
          width={20}
          height={20}
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth={2}
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
        >
          {/* Arrow pointing right for RTL (back = forward arrow visually in RTL) */}
          <path d="M5 12h14M12 5l7 7-7 7" />
        </svg>
      </button>

      {/* Group name — centered, truncated */}
      <h1 className="flex-1 text-base font-semibold text-slate-900 dark:text-white truncate text-center min-w-0">
        {groupName ?? t("title")}
      </h1>

      {/* Refresh button — min 44x44 tap target */}
      <button
        onClick={onRefresh}
        disabled={refreshing}
        aria-label={t("refresh")}
        className="flex items-center justify-center min-w-[44px] min-h-[44px] rounded-lg text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
      >
        {refreshing ? (
          <svg
            className="animate-spin h-5 w-5"
            fill="none"
            viewBox="0 0 24 24"
            aria-hidden="true"
          >
            <circle
              className="opacity-25"
              cx="12"
              cy="12"
              r="10"
              stroke="currentColor"
              strokeWidth="4"
            />
            <path
              className="opacity-75"
              fill="currentColor"
              d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
            />
          </svg>
        ) : (
          <svg
            width={20}
            height={20}
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth={2}
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
          >
            <path d="M1 4v6h6M23 20v-6h-6" />
            <path d="M20.49 9A9 9 0 0 0 5.64 5.64L1 10m22 4l-4.64 4.36A9 9 0 0 1 3.51 15" />
          </svg>
        )}
      </button>
    </header>
  );
}
