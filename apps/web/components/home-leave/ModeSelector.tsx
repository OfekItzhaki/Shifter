"use client";

import { useCallback } from "react";
import { useTranslations } from "next-intl";

export type HomeLeaveMode = "automatic" | "manual";

interface ModeSelectorProps {
  /** Currently selected mode */
  value: HomeLeaveMode;
  /** Callback when mode changes */
  onChange: (mode: HomeLeaveMode) => void;
  /** Whether the selector is disabled */
  disabled?: boolean;
}

/**
 * Segmented control for switching between Automatic and Manual home-leave modes.
 * Accessible with keyboard navigation and ARIA attributes.
 * Localized labels using next-intl (Hebrew, English, Russian).
 */
export default function ModeSelector({
  value,
  onChange,
  disabled = false,
}: ModeSelectorProps) {
  const t = useTranslations("homeLeave.modeSelector");

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (disabled) return;
      if (e.key === "ArrowLeft" || e.key === "ArrowRight") {
        e.preventDefault();
        onChange(value === "automatic" ? "manual" : "automatic");
      }
    },
    [disabled, onChange, value]
  );

  return (
    <div
      role="radiogroup"
      aria-label={t("groupLabel")}
      className="inline-flex rounded-xl bg-slate-100 p-1 gap-1"
      onKeyDown={handleKeyDown}
    >
      <button
        type="button"
        role="radio"
        aria-checked={value === "automatic"}
        tabIndex={value === "automatic" ? 0 : -1}
        disabled={disabled}
        onClick={() => onChange("automatic")}
        className={`px-4 py-2 text-sm font-medium rounded-lg transition-all focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-500 ${
          value === "automatic"
            ? "bg-white text-sky-700 shadow-sm"
            : "text-slate-500 hover:text-slate-700"
        } ${disabled ? "opacity-50 cursor-not-allowed" : "cursor-pointer"}`}
      >
        {t("automatic")}
      </button>
      <button
        type="button"
        role="radio"
        aria-checked={value === "manual"}
        tabIndex={value === "manual" ? 0 : -1}
        disabled={disabled}
        onClick={() => onChange("manual")}
        className={`px-4 py-2 text-sm font-medium rounded-lg transition-all focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-500 ${
          value === "manual"
            ? "bg-white text-sky-700 shadow-sm"
            : "text-slate-500 hover:text-slate-700"
        } ${disabled ? "opacity-50 cursor-not-allowed" : "cursor-pointer"}`}
      >
        {t("manual")}
      </button>
    </div>
  );
}
