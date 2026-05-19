"use client";

import { useState, useEffect, useCallback } from "react";
import { useTranslations } from "next-intl";
import {
  getFreezePeriodChangesCount,
  FreezePeriodChangesCountDto,
} from "@/lib/api/homeLeave";

interface FreezeDeactivationDialogProps {
  /** Whether the dialog is open */
  open: boolean;
  /** Space ID for the current group */
  spaceId: string;
  /** Group ID for the current group */
  groupId: string;
  /** Whether the user has schedule.rollback permission */
  canRollback: boolean;
  /** Called when the user confirms deactivation with their discard choice */
  onConfirm: (discardFreezeChanges: boolean) => void;
  /** Called when the user cancels */
  onCancel: () => void;
}

/**
 * Dialog shown when admin clicks "Deactivate Freeze".
 * Fetches freeze-period change counts and presents a discard toggle
 * if the user has the schedule.rollback permission and changes exist.
 *
 * Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 5.2, 7.2
 */
export default function FreezeDeactivationDialog({
  open,
  spaceId,
  groupId,
  canRollback,
  onConfirm,
  onCancel,
}: FreezeDeactivationDialogProps) {
  const t = useTranslations("homeLeave.deactivationDialog");

  const [counts, setCounts] = useState<FreezePeriodChangesCountDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [fetchError, setFetchError] = useState(false);
  const [discardChecked, setDiscardChecked] = useState(false);

  // Fetch change counts when dialog opens
  const fetchCounts = useCallback(async () => {
    if (!spaceId || !groupId) return;
    setLoading(true);
    setFetchError(false);
    setCounts(null);
    setDiscardChecked(false);

    try {
      const result = await getFreezePeriodChangesCount(spaceId, groupId);
      setCounts(result);
    } catch {
      setFetchError(true);
    } finally {
      setLoading(false);
    }
  }, [spaceId, groupId]);

  useEffect(() => {
    if (open) {
      fetchCounts();
    }
  }, [open, fetchCounts]);

  if (!open) return null;

  const hasChanges = counts !== null && counts.totalCount > 0;
  const noChanges = counts !== null && counts.totalCount === 0;

  // Determine whether to show the discard toggle:
  // - Hidden if user lacks schedule.rollback permission (Req 5.2)
  // - Hidden if no changes exist (Req 1.4)
  // - Disabled if fetch failed (Req 1.5)
  const showDiscardToggle = canRollback && hasChanges && !fetchError;
  const disableDiscardToggle = fetchError;

  function handleConfirm() {
    onConfirm(discardChecked);
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40"
      role="dialog"
      aria-modal="true"
      aria-labelledby="freeze-deactivation-title"
    >
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-md mx-4 p-6 space-y-4">
        {/* Title */}
        <h2
          id="freeze-deactivation-title"
          className="text-lg font-semibold text-slate-800"
        >
          {t("title")}
        </h2>

        {/* Description */}
        <p className="text-sm text-slate-600">{t("description")}</p>

        {/* Loading state */}
        {loading && (
          <div className="flex items-center gap-2 py-3 text-slate-400 text-sm">
            <svg className="animate-spin h-4 w-4" fill="none" viewBox="0 0 24 24">
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
            {t("loadingCounts")}
          </div>
        )}

        {/* Change counts display */}
        {!loading && hasChanges && (
          <div className="bg-slate-50 border border-slate-200 rounded-xl p-4 space-y-2">
            <p className="text-sm font-medium text-slate-700">
              {t("changesTitle")}
            </p>
            <div className="grid grid-cols-3 gap-3 text-center">
              <div>
                <p className="text-xl font-bold text-slate-800">
                  {counts.overrideCount}
                </p>
                <p className="text-xs text-slate-500">{t("overrides")}</p>
              </div>
              <div>
                <p className="text-xl font-bold text-slate-800">
                  {counts.manualAssignmentCount}
                </p>
                <p className="text-xs text-slate-500">{t("manualAssignments")}</p>
              </div>
              <div>
                <p className="text-xl font-bold text-slate-800">
                  {counts.swapCount}
                </p>
                <p className="text-xs text-slate-500">{t("swaps")}</p>
              </div>
            </div>
          </div>
        )}

        {/* No changes message (Req 1.4) */}
        {!loading && noChanges && (
          <div className="bg-emerald-50 border border-emerald-200 rounded-xl px-4 py-3">
            <p className="text-sm text-emerald-700">{t("noChanges")}</p>
          </div>
        )}

        {/* Fetch error (Req 1.5) */}
        {!loading && fetchError && (
          <div className="bg-red-50 border border-red-200 rounded-xl px-4 py-3">
            <p className="text-sm text-red-700">{t("fetchError")}</p>
          </div>
        )}

        {/* Discard toggle (Req 1.1, 1.2, 5.2) */}
        {showDiscardToggle && (
          <label className="flex items-start gap-3 cursor-pointer p-3 rounded-xl border border-slate-200 hover:bg-slate-50 transition-colors">
            <input
              type="checkbox"
              checked={discardChecked}
              onChange={(e) => setDiscardChecked(e.target.checked)}
              disabled={disableDiscardToggle}
              className="mt-0.5 w-4 h-4 rounded border-slate-300 text-red-600 focus:ring-red-500 disabled:opacity-50"
            />
            <div className="space-y-0.5">
              <span className="text-sm font-medium text-slate-700">
                {t("discardLabel")}
              </span>
              <p className="text-xs text-slate-500">{t("discardHint")}</p>
            </div>
          </label>
        )}

        {/* Disabled discard toggle with error when fetch fails and user has permission */}
        {canRollback && fetchError && (
          <label className="flex items-start gap-3 p-3 rounded-xl border border-slate-200 opacity-50 cursor-not-allowed">
            <input
              type="checkbox"
              checked={false}
              disabled
              className="mt-0.5 w-4 h-4 rounded border-slate-300 text-red-600"
            />
            <div className="space-y-0.5">
              <span className="text-sm font-medium text-slate-700">
                {t("discardLabel")}
              </span>
              <p className="text-xs text-red-500">{t("discardUnavailable")}</p>
            </div>
          </label>
        )}

        {/* Action buttons */}
        <div className="flex gap-3 pt-2">
          <button
            type="button"
            onClick={handleConfirm}
            disabled={loading}
            className="flex-1 px-4 py-2.5 text-sm font-medium bg-red-600 text-white rounded-xl hover:bg-red-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {t("confirm")}
          </button>
          <button
            type="button"
            onClick={onCancel}
            className="flex-1 px-4 py-2.5 text-sm font-medium bg-white text-slate-600 border border-slate-200 rounded-xl hover:bg-slate-50 transition-colors"
          >
            {t("cancel")}
          </button>
        </div>
      </div>
    </div>
  );
}
