"use client";

import { useCallback } from "react";
import { useTranslations, useLocale } from "next-intl";

interface RatioSliderProps {
  /** Optimal base days calculated by the system */
  optimalBaseDays: number;
  /** Optimal home days calculated by the system */
  optimalHomeDays: number;
  /** Current slider value (0-100). 50 = optimal ratio */
  value: number;
  /** Callback when slider value changes */
  onChange: (value: number) => void;
  /** Whether the slider is disabled */
  disabled?: boolean;
}

/**
 * Calculates the displayed base:home ratio from the slider position.
 * At value=50: optimal ratio. Below 50: more conservative (more base days).
 * Above 50: more generous (more home days).
 */
function calculateDisplayRatio(
  value: number,
  optimalBaseDays: number,
  optimalHomeDays: number
): { baseDays: number; homeDays: number } {
  if (value === 50) {
    return { baseDays: optimalBaseDays, homeDays: optimalHomeDays };
  }

  if (value < 50) {
    // More conservative: increase base days, keep home days
    const factor = 1 + ((50 - value) / 50) * 1.5; // 1.0 at 50, up to 2.5 at 0
    const baseDays = Math.max(1, Math.round(optimalBaseDays * factor));
    const homeDays = Math.max(1, optimalHomeDays);
    return { baseDays, homeDays };
  }

  // More generous: decrease base days (min 1), keep home days
  const factor = 1 - ((value - 50) / 50) * 0.6; // 1.0 at 50, down to 0.4 at 100
  const baseDays = Math.max(1, Math.round(optimalBaseDays * factor));
  const homeDays = Math.max(1, optimalHomeDays);
  return { baseDays, homeDays };
}

/**
 * Smart ratio slider that replaces the old BalanceSlider.
 * Centered on the optimal ratio, with gradient from conservative to generous.
 * Step of 5, keyboard accessible (arrow keys ±5, Page Up/Down ±10).
 */
export default function RatioSlider({
  optimalBaseDays,
  optimalHomeDays,
  value,
  onChange,
  disabled = false,
}: RatioSliderProps) {
  const t = useTranslations("homeLeave.ratioSlider");
  const locale = useLocale();
  const isRTL = locale === "he";

  const { baseDays, homeDays } = calculateDisplayRatio(
    value,
    optimalBaseDays,
    optimalHomeDays
  );

  const handleChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      onChange(Number(e.target.value));
    },
    [onChange]
  );

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLInputElement>) => {
      let newValue = value;
      if (e.key === "PageUp") {
        e.preventDefault();
        newValue = Math.min(100, value + 10);
      } else if (e.key === "PageDown") {
        e.preventDefault();
        newValue = Math.max(0, value - 10);
      }
      if (newValue !== value) {
        onChange(newValue);
      }
    },
    [value, onChange]
  );

  // Gradient direction: in RTL, conservative is on the right, generous on the left
  const gradientClass = isRTL
    ? "bg-gradient-to-r from-emerald-400 via-slate-300 to-blue-400"
    : "bg-gradient-to-l from-emerald-400 via-slate-300 to-blue-400";

  return (
    <div className="space-y-3" dir={isRTL ? "rtl" : "ltr"}>
      {/* Current ratio display */}
      <div className="flex justify-center">
        <span className="inline-flex items-center gap-1.5 px-4 py-1.5 text-sm font-semibold text-slate-700 bg-slate-100 rounded-lg">
          <span className="text-blue-600">{baseDays}</span>
          <span className="text-slate-400">:</span>
          <span className="text-emerald-600">{homeDays}</span>
          <span className="text-xs text-slate-400 font-normal ms-1">
            {t("ratioLabel")}
          </span>
        </span>
      </div>

      {/* Optimal badge */}
      {value === 50 && (
        <div className="flex justify-center">
          <span className="text-xs text-emerald-600 bg-emerald-50 px-2 py-0.5 rounded-full">
            {t("optimal")}
          </span>
        </div>
      )}

      {/* Slider */}
      <div className="relative">
        <input
          type="range"
          min={0}
          max={100}
          step={5}
          value={value}
          onChange={handleChange}
          onKeyDown={handleKeyDown}
          disabled={disabled}
          dir={isRTL ? "rtl" : "ltr"}
          aria-label={t("ariaLabel")}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={value}
          aria-valuetext={t("ariaValueText", { baseDays, homeDays })}
          className={`w-full h-2 appearance-none ${gradientClass} rounded-full cursor-pointer disabled:cursor-not-allowed disabled:opacity-50
            [&::-webkit-slider-thumb]:appearance-none
            [&::-webkit-slider-thumb]:w-6
            [&::-webkit-slider-thumb]:h-6
            [&::-webkit-slider-thumb]:rounded-full
            [&::-webkit-slider-thumb]:bg-white
            [&::-webkit-slider-thumb]:border-2
            [&::-webkit-slider-thumb]:border-blue-500
            [&::-webkit-slider-thumb]:shadow-md
            [&::-webkit-slider-thumb]:transition-transform
            [&::-webkit-slider-thumb]:hover:scale-110
            [&::-moz-range-thumb]:w-6
            [&::-moz-range-thumb]:h-6
            [&::-moz-range-thumb]:rounded-full
            [&::-moz-range-thumb]:bg-white
            [&::-moz-range-thumb]:border-2
            [&::-moz-range-thumb]:border-blue-500
            [&::-moz-range-thumb]:shadow-md
            [&::-webkit-slider-runnable-track]:bg-transparent
            [&::-moz-range-track]:bg-transparent`}
        />
        {/* Center marker for optimal position */}
        <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-0.5 h-4 bg-slate-400 rounded-full pointer-events-none opacity-50" />
      </div>

      {/* Labels */}
      <div className="flex justify-between px-1">
        <span className="text-xs text-blue-600 font-medium">
          {t("moreBase")}
        </span>
        <span className="text-xs text-emerald-600 font-medium">
          {t("moreHome")}
        </span>
      </div>
    </div>
  );
}
