"use client";

import { useState, useEffect, useRef, useCallback } from "react";
import { useTranslations } from "next-intl";
import FreezeDeactivationDialog from "./FreezeDeactivationDialog";
import { deactivateFreeze, DeactivateFreezeResponse } from "@/lib/api/homeLeave";

interface EmergencyFreezeBannerProps {
  /** Whether emergency freeze is currently active */
  isActive: boolean;
  /** Whether frozen personnel should be used for scheduling */
  useForScheduling: boolean;
  /** Timestamp when freeze was activated (ISO string) */
  freezeStartedAt?: string | null;
  /** Callback when freeze is activated (true only) */
  onToggleFreeze: (active: boolean) => void;
  /** Callback when "use for scheduling" option changes */
  onUseForSchedulingChange: (useForScheduling: boolean) => void;
  /** Whether the banner is disabled */
  disabled?: boolean;
  /** Space ID for the current group */
  spaceId: string;
  /** Group ID for the current group */
  groupId: string;
  /** Whether the user has schedule.rollback permission (shows discard option) */
  canRollback: boolean;
  /** Called after successful deactivation with the API response */
  onDeactivateSuccess: (response: DeactivateFreezeResponse) => void;
}

/**
 * Formats a duration in milliseconds to a human-readable string (HH:MM:SS).
 */
function formatDuration(ms: number): string {
  const totalSeconds = Math.floor(ms / 1000);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  return `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
}

/**
 * Emergency freeze banner with prominent toggle, duration timer,
 * and "use for scheduling" option. Includes confirmation dialog before activation
 * and deactivation dialog with optional discard of freeze-period changes.
 *
 * Requirements: 1.1, 4.1, 4.2, 6.1
 */
export default function EmergencyFreezeBanner({
  isActive,
  useForScheduling,
  freezeStartedAt,
  onToggleFreeze,
  onUseForSchedulingChange,
  disabled = false,
  spaceId,
  groupId,
  canRollback,
  onDeactivateSuccess,
}: EmergencyFreezeBannerProps) {
  const t = useTranslations("homeLeave.emergencyFreeze");
  const [showConfirm, setShowConfirm] = useState(false);
  const [showDeactivateDialog, setShowDeactivateDialog] = useState(false);
  const [deactivating, setDeactivating] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [duration, setDuration] = useState<string>("00:00:00");
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // Update duration timer every second when freeze is active
  useEffect(() => {
    if (isActive && freezeStartedAt) {
      const updateDuration = () => {
        const elapsed = Date.now() - new Date(freezeStartedAt).getTime();
        setDuration(formatDuration(Math.max(0, elapsed)));
      };

      updateDuration();
      intervalRef.current = setInterval(updateDuration, 1000);

      return () => {
        if (intervalRef.current) {
          clearInterval(intervalRef.current);
        }
      };
    } else {
      setDuration("00:00:00");
    }
  }, [isActive, freezeStartedAt]);

  // Auto-dismiss success toast after 5 seconds
  useEffect(() => {
    if (successMessage) {
      const timer = setTimeout(() => setSuccessMessage(null), 5000);
      return () => clearTimeout(timer);
    }
  }, [successMessage]);

  // Auto-dismiss error message after 8 seconds
  useEffect(() => {
    if (errorMessage) {
      const timer = setTimeout(() => setErrorMessage(null), 8000);
      return () => clearTimeout(timer);
    }
  }, [errorMessage]);

  const handleToggleClick = useCallback(() => {
    if (isActive) {
      // Open deactivation dialog instead of deactivating immediately
      setShowDeactivateDialog(true);
    } else {
      // Show confirmation before activation
      setShowConfirm(true);
    }
  }, [isActive]);

  const handleConfirmActivate = useCallback(() => {
    setShowConfirm(false);
    onToggleFreeze(true);
  }, [onToggleFreeze]);

  const handleCancelActivate = useCallback(() => {
    setShowConfirm(false);
  }, []);

  const handleDeactivateConfirm = useCallback(
    async (discardFreezeChanges: boolean) => {
      setDeactivating(true);
      setErrorMessage(null);

      try {
        const response = await deactivateFreeze(spaceId, groupId, discardFreezeChanges);

        // Close dialog on success
        setShowDeactivateDialog(false);

        // Show success toast
        if (response.discardPerformed) {
          setSuccessMessage(
            t("deactivateSuccessDiscard", { count: response.discardedChangeCount })
          );
        } else {
          setSuccessMessage(t("deactivateSuccess"));
        }

        // Notify parent to refresh config state
        onDeactivateSuccess(response);
      } catch (err: unknown) {
        const error = err as { response?: { status?: number; data?: { message?: string } } };
        const status = error?.response?.status;

        if (status === 403) {
          setErrorMessage(t("errorPermission"));
        } else if (status === 400) {
          setErrorMessage(
            error?.response?.data?.message || t("errorBadRequest")
          );
        } else {
          setErrorMessage(t("errorServer"));
        }
      } finally {
        setDeactivating(false);
      }
    },
    [spaceId, groupId, t, onDeactivateSuccess]
  );

  const handleDeactivateCancel = useCallback(() => {
    setShowDeactivateDialog(false);
  }, []);

  return (
    <div className="space-y-2">
      {/* Active freeze banner */}
      {isActive && (
        <div className="bg-red-50 border-2 border-red-300 rounded-xl p-4 space-y-3">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <div className="w-3 h-3 rounded-full bg-red-500 animate-pulse" />
              <span className="text-sm font-semibold text-red-800">
                {t("activeTitle")}
              </span>
            </div>
            <span className="text-sm font-mono text-red-700 bg-red-100 px-2 py-0.5 rounded">
              {duration}
            </span>
          </div>

          {/* Use for scheduling toggle */}
          <label className="flex items-center gap-2 cursor-pointer">
            <input
              type="checkbox"
              checked={useForScheduling}
              onChange={(e) => onUseForSchedulingChange(e.target.checked)}
              disabled={disabled}
              className="w-4 h-4 rounded border-red-300 text-red-600 focus:ring-red-500"
            />
            <span className="text-xs text-red-700">
              {t("useForScheduling")}
            </span>
          </label>
        </div>
      )}

      {/* Toggle button */}
      <button
        type="button"
        onClick={handleToggleClick}
        disabled={disabled || deactivating}
        aria-pressed={isActive}
        className={`w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl text-sm font-medium transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 ${
          isActive
            ? "bg-red-600 hover:bg-red-700 text-white focus-visible:ring-red-500"
            : "bg-slate-100 hover:bg-red-50 text-red-700 border border-red-200 focus-visible:ring-red-500"
        } ${disabled || deactivating ? "opacity-50 cursor-not-allowed" : "cursor-pointer"}`}
      >
        <svg
          className="w-4 h-4"
          fill="none"
          viewBox="0 0 24 24"
          strokeWidth={2}
          stroke="currentColor"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z"
          />
        </svg>
        {deactivating ? t("deactivating") : isActive ? t("deactivate") : t("activate")}
      </button>

      {/* Activation confirmation dialog */}
      {showConfirm && (
        <div className="bg-amber-50 border border-amber-200 rounded-xl p-4 space-y-3">
          <p className="text-sm text-amber-800 font-medium">
            {t("confirmTitle")}
          </p>
          <p className="text-xs text-amber-700">
            {t("confirmDescription")}
          </p>
          <div className="flex gap-2">
            <button
              type="button"
              onClick={handleConfirmActivate}
              className="px-3 py-1.5 text-xs font-medium bg-red-600 text-white rounded-lg hover:bg-red-700 transition-colors"
            >
              {t("confirmYes")}
            </button>
            <button
              type="button"
              onClick={handleCancelActivate}
              className="px-3 py-1.5 text-xs font-medium bg-white text-slate-600 border border-slate-200 rounded-lg hover:bg-slate-50 transition-colors"
            >
              {t("confirmNo")}
            </button>
          </div>
        </div>
      )}

      {/* Error message */}
      {errorMessage && (
        <div className="flex items-center gap-2 bg-red-50 border border-red-200 rounded-xl px-4 py-3">
          <svg
            className="w-4 h-4 text-red-500 flex-shrink-0"
            fill="none"
            viewBox="0 0 24 24"
            strokeWidth={1.5}
            stroke="currentColor"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z"
            />
          </svg>
          <span className="text-sm text-red-700">{errorMessage}</span>
          <button
            type="button"
            onClick={() => setErrorMessage(null)}
            className="ml-auto text-red-400 hover:text-red-600"
            aria-label="Dismiss"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
      )}

      {/* Success toast */}
      {successMessage && (
        <div
          role="status"
          aria-live="polite"
          className="flex items-center gap-2 bg-emerald-50 border border-emerald-200 rounded-xl px-4 py-3"
        >
          <svg
            className="w-4 h-4 text-emerald-500 flex-shrink-0"
            fill="none"
            viewBox="0 0 24 24"
            strokeWidth={2}
            stroke="currentColor"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"
            />
          </svg>
          <span className="text-sm text-emerald-700">{successMessage}</span>
          <button
            type="button"
            onClick={() => setSuccessMessage(null)}
            className="ml-auto text-emerald-400 hover:text-emerald-600"
            aria-label="Dismiss"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
      )}

      {/* Deactivation dialog with discard option */}
      <FreezeDeactivationDialog
        open={showDeactivateDialog}
        spaceId={spaceId}
        groupId={groupId}
        canRollback={canRollback}
        onConfirm={handleDeactivateConfirm}
        onCancel={handleDeactivateCancel}
      />
    </div>
  );
}
