"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";

export interface RegenerateButtonProps {
  /** The space ID for the current space */
  spaceId: string;
  /** The group ID for the selected group */
  groupId: string;
  /** Whether a published version exists for the selected group */
  hasPublishedVersion: boolean;
  /** Whether the user has ScheduleRecalculate permission */
  hasPermission: boolean;
  /** Whether a regeneration run is currently in progress */
  isRegenerationInProgress: boolean;
  /** Called when the user clicks the button — should open the confirm dialog */
  onRegenerate: () => void;
}

/**
 * Displays a "Regenerate Schedule" button in the admin schedule management panel.
 *
 * Visibility rules:
 * - Hidden when user lacks ScheduleRecalculate permission (Req 7.3)
 * - Hidden when no published version exists for the group (Req 1.4)
 * - Disabled with status indicator when a regeneration run is in progress (Req 1.5)
 * - On click, triggers the onRegenerate callback to open the confirmation dialog (Req 1.1)
 */
export default function RegenerateButton({
  hasPublishedVersion,
  hasPermission,
  isRegenerationInProgress,
  onRegenerate,
}: RegenerateButtonProps) {
  const t = useTranslations("admin.regeneration");

  // Hide button when user lacks permission
  if (!hasPermission) return null;

  // Hide button when no published version exists
  if (!hasPublishedVersion) return null;

  return (
    <button
      onClick={onRegenerate}
      disabled={isRegenerationInProgress}
      aria-busy={isRegenerationInProgress}
      className="flex items-center gap-2 bg-violet-500 hover:bg-violet-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl shadow-sm shadow-violet-500/20 disabled:opacity-50 disabled:cursor-not-allowed transition-all"
    >
      {isRegenerationInProgress ? (
        <>
          <svg
            className="animate-spin h-4 w-4"
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
          <span>{t("inProgress")}</span>
        </>
      ) : (
        <>
          <svg
            className="w-4 h-4"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            strokeWidth={2}
            aria-hidden="true"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"
            />
          </svg>
          <span>{t("button")}</span>
        </>
      )}
    </button>
  );
}
