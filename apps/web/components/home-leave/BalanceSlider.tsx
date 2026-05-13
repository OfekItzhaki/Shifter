"use client";

import { useCallback } from "react";

interface BalanceSliderProps {
  /** Current slider value (0–100) */
  value: number;
  /** Callback when the value changes */
  onChange: (value: number) => void;
  /** Whether the slider is disabled */
  disabled?: boolean;
}

/**
 * BalanceSlider — a horizontal range input (0–100) for adjusting the
 * home-leave vs. base-coverage balance.
 *
 * RTL layout: left = "יותר אנשים בבסיס" (more at base, value 0),
 *             right = "יותר אנשים בבית" (more at home, value 100).
 *
 * Keyboard: Arrow keys ±1, Page Up/Down ±10.
 */
export default function BalanceSlider({
  value,
  onChange,
  disabled = false,
}: BalanceSliderProps) {
  const clamp = (v: number) => Math.max(0, Math.min(100, v));

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLInputElement>) => {
      let newValue: number | null = null;

      switch (e.key) {
        case "PageUp":
          newValue = clamp(value + 10);
          e.preventDefault();
          break;
        case "PageDown":
          newValue = clamp(value - 10);
          e.preventDefault();
          break;
        // Arrow keys are handled natively by the range input
        default:
          return;
      }

      if (newValue !== null && newValue !== value) {
        onChange(newValue);
      }
    },
    [value, onChange]
  );

  const handleChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const newValue = Number(e.target.value);
      onChange(clamp(newValue));
    },
    [onChange]
  );

  // Calculate the fill percentage for the gradient track visual
  const fillPercent = value;

  return (
    <div className="space-y-2">
      {/* Labels row */}
      <div className="flex items-center justify-between">
        <span className="text-xs text-slate-500 select-none">
          יותר אנשים בבסיס
        </span>
        <span className="text-xs text-slate-500 select-none">
          יותר אנשים בבית
        </span>
      </div>

      {/* Slider track + input */}
      <div className="relative flex items-center">
        {/* Background gradient track */}
        <div
          className="absolute inset-0 h-2 top-1/2 -translate-y-1/2 rounded-full pointer-events-none"
          style={{
            background: `linear-gradient(to right, #3b82f6, #3b82f6 ${fillPercent}%, #e2e8f0 ${fillPercent}%, #e2e8f0)`,
          }}
        />
        {/* Colored overlay: blue (base) on left, green (home) on right */}
        <div
          className="absolute inset-0 h-2 top-1/2 -translate-y-1/2 rounded-full pointer-events-none"
          style={{
            background: `linear-gradient(to right, #3b82f6 0%, #6366f1 50%, #10b981 100%)`,
            opacity: 0.85,
          }}
        />

        {/* Native range input */}
        <input
          type="range"
          min={0}
          max={100}
          step={1}
          value={value}
          onChange={handleChange}
          onKeyDown={handleKeyDown}
          disabled={disabled}
          aria-label="איזון חופשות בית-בסיס"
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={value}
          className="relative z-10 w-full h-2 appearance-none bg-transparent cursor-pointer disabled:cursor-not-allowed disabled:opacity-50
            [&::-webkit-slider-thumb]:appearance-none
            [&::-webkit-slider-thumb]:w-5
            [&::-webkit-slider-thumb]:h-5
            [&::-webkit-slider-thumb]:rounded-full
            [&::-webkit-slider-thumb]:bg-white
            [&::-webkit-slider-thumb]:border-2
            [&::-webkit-slider-thumb]:border-blue-500
            [&::-webkit-slider-thumb]:shadow-md
            [&::-webkit-slider-thumb]:transition-transform
            [&::-webkit-slider-thumb]:hover:scale-110
            [&::-moz-range-thumb]:w-5
            [&::-moz-range-thumb]:h-5
            [&::-moz-range-thumb]:rounded-full
            [&::-moz-range-thumb]:bg-white
            [&::-moz-range-thumb]:border-2
            [&::-moz-range-thumb]:border-blue-500
            [&::-moz-range-thumb]:shadow-md
            [&::-webkit-slider-runnable-track]:bg-transparent
            [&::-moz-range-track]:bg-transparent"
        />
      </div>

      {/* Current value display */}
      <div className="flex justify-center">
        <span
          className="inline-flex items-center justify-center min-w-[2.5rem] px-2 py-0.5 text-sm font-medium text-slate-700 bg-slate-100 rounded-lg"
          aria-live="polite"
        >
          {value}
        </span>
      </div>
    </div>
  );
}
