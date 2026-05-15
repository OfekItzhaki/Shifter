"use client";

import { useCallback } from "react";

interface BalanceSliderProps {
  /** Current slider value (0–100) */
  value: number;
  /** Callback when the value changes (emits 0-100 for backend compat) */
  onChange: (value: number) => void;
  /** Whether the slider is disabled */
  disabled?: boolean;
  /** Eligibility threshold in hours (used to calculate ratio) */
  eligibilityHours?: number;
  /** Leave duration in hours (used to calculate ratio) */
  leaveDurationHours?: number;
}

/**
 * Calculates the estimated base:home ratio based on slider position and settings.
 * At value=0: all base, no home. At value=100: maximum home time.
 * The ratio is: baseDays : homeDays per cycle.
 */
function calculateRatio(value: number, eligibilityHours: number, leaveDurationHours: number): string {
  if (value === 0) return "∞:0";
  
  const eligibilityDays = Math.round(eligibilityHours / 24);
  const leaveDays = Math.round(leaveDurationHours / 24) || 1;
  
  // At value=100, the cycle is eligibility + leave (maximum home time)
  // At value=50, double the base time
  // Scale: lower value = more base time relative to home
  const scaleFactor = Math.max(0.1, value / 50); // 0.1 at value=5, 1.0 at value=50, 2.0 at value=100
  const effectiveBaseDays = Math.max(1, Math.round(eligibilityDays / scaleFactor));
  
  return `${effectiveBaseDays}:${leaveDays}`;
}

export default function BalanceSlider({
  value,
  onChange,
  disabled = false,
  eligibilityHours = 168,
  leaveDurationHours = 48,
}: BalanceSliderProps) {
  const handleChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      onChange(Number(e.target.value));
    },
    [onChange]
  );

  const ratio = calculateRatio(value, eligibilityHours, leaveDurationHours);

  return (
    <div className="space-y-3">
      {/* Current ratio display */}
      <div className="flex justify-center">
        <span className="inline-flex items-center gap-1.5 px-4 py-1.5 text-sm font-semibold text-slate-700 bg-slate-100 rounded-lg">
          <span className="text-blue-600">{ratio.split(":")[0]}</span>
          <span className="text-slate-400">:</span>
          <span className="text-emerald-600">{ratio.split(":")[1]}</span>
          <span className="text-xs text-slate-400 font-normal mr-1">ימים בסיס / בית</span>
        </span>
      </div>

      {/* Slider — continuous 0-100 */}
      <div className="relative">
        <input
          type="range"
          min={0}
          max={100}
          step={5}
          value={value}
          onChange={handleChange}
          disabled={disabled}
          aria-label="יחס זמן בסיס/בית"
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={value}
          className="w-full h-2 appearance-none bg-gradient-to-l from-blue-400 via-slate-300 to-emerald-400 rounded-full cursor-pointer disabled:cursor-not-allowed disabled:opacity-50
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
            [&::-moz-range-track]:bg-transparent"
        />
      </div>

      {/* Labels: in RTL, first element is on the right */}
      <div className="flex justify-between px-1">
        <span className="text-xs text-blue-600 font-medium">יותר אנשים בבסיס</span>
        <span className="text-xs text-emerald-600 font-medium">יותר אנשים בבית</span>
      </div>
    </div>
  );
}
