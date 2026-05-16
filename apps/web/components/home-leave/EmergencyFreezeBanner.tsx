"use client";

import { useState, useEffect, useRef, useCallback } from "react";
import { useTranslations } from "next-intl";

interface EmergencyFreezeBannerProps {
  /** Whether emergency freeze is currently active */
  isActive: boolean;
  /** Whether frozen personnel should be used for scheduling */
  useForScheduling: boolean;
  /** Timestamp when freeze was activated (ISO string) */
  freezeStartedAt?: string | null;
  /** Callback when freeze is toggled */
  onToggleFreeze: (active: boolean) => void;
  /** Callback when "use for scheduling" option changes */
  onUseForSchedulingChange: (useForScheduling: boolean) => void;
  /** Whether the banner is disabled */
  disabled?: boolean;
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
 * and "use for scheduling" option. Includes confirmation dialog before activation.
 */
export default function EmergencyFreezeBanner({
  isActive,
  useForScheduling,
  freezeStartedAt,
  onToggleFreeze,
  onUseForSchedulingChange,
  disabled = false,
}: EmergencyFreezeBannerProps) {
  const t = useTranslations("homeLeave.emergencyFreeze");
  const [showConfirm, setShowConfirm] = useState(false);
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

  const handleToggleClick = useCallback(() => {
    if (isActive) {
      // Deactivate immediately
      onToggleFreeze(false);
    } else {
      // Show confirmation before activation
      setShowConfirm(true);
    }
  }, [isActive, onToggleFreeze]);

  const handleConfirmActivate = useCallback(() => {
    setShowConfirm(false);
    onToggleFreeze(true);
  }, [onToggleFreeze]);

  const handleCancelActivate = useCallback(() => {
    setShowConfirm(false);
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
        disabled={disabled}
        aria-pressed={isActive}
        className={`w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl text-sm font-medium transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 ${
          isActive
            ? "bg-red-600 hover:bg-red-700 text-white focus-visible:ring-red-500"
            : "bg-slate-100 hover:bg-red-50 text-red-700 border border-red-200 focus-visible:ring-red-500"
        } ${disabled ? "opacity-50 cursor-not-allowed" : "cursor-pointer"}`}
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
        {isActive ? t("deactivate") : t("activate")}
      </button>

      {/* Confirmation dialog */}
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
    </div>
  );
}
