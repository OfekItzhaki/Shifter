"use client";

import { useCallback } from "react";
import { useTranslations } from "next-intl";

interface LeaveDurationInputProps {
  /** Current value in hours (internally stored) */
  valueHours: number;
  /** Callback when value changes (emits hours) */
  onChange: (hours: number) => void;
  /** Whether the input is disabled */
  disabled?: boolean;
}

/**
 * Numeric input for leave duration displayed in days.
 * Internally stores as hours (value × 24).
 * Validation: 0.5 to 7 days (12 to 168 hours).
 * Shared between Automatic and Manual modes.
 */
export default function LeaveDurationInput({
  valueHours,
  onChange,
  disabled = false,
}: LeaveDurationInputProps) {
  const t = useTranslations("homeLeave.leaveDuration");

  // Convert hours to days for display
  const displayDays = valueHours / 24;

  // Validation
  const isValid = valueHours >= 12 && valueHours <= 168;
  const hasError = valueHours > 0 && !isValid;

  const handleChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const days = Number(e.target.value);
      if (isNaN(days)) return;
      // Convert days to hours and clamp
      const hours = days * 24;
      onChange(hours);
    },
    [onChange]
  );

  return (
    <div className="space-y-1">
      <label className="block text-sm text-slate-600 font-medium">
        {t("label")}
      </label>
      <p className="text-xs text-slate-400 -mt-0.5">{t("hint")}</p>
      <div className="relative">
        <input
          type="number"
          value={displayDays}
          onChange={handleChange}
          min={0.5}
          max={7}
          step={0.5}
          disabled={disabled}
          aria-label={t("label")}
          aria-invalid={hasError}
          aria-describedby={hasError ? "leave-duration-error" : undefined}
          className={`w-full border rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed ${
            hasError
              ? "border-red-300 focus:ring-red-500"
              : "border-slate-200"
          }`}
        />
        <span className="absolute end-3 top-1/2 -translate-y-1/2 text-xs text-slate-400 pointer-events-none">
          {t("unit")}
        </span>
      </div>
      {hasError && (
        <p id="leave-duration-error" className="text-xs text-red-600" role="alert">
          {t("validationError")}
        </p>
      )}
    </div>
  );
}
