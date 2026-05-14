"use client";

import { useCallback } from "react";

interface BalanceSliderProps {
  /** Current slider value (0–100) — mapped from 5 steps internally */
  value: number;
  /** Callback when the value changes (emits 0-100 for backend compat) */
  onChange: (value: number) => void;
  /** Whether the slider is disabled */
  disabled?: boolean;
}

const STEPS = [
  { value: 0, label: "יותר אנשים בבסיס" },
  { value: 25, label: "פחות אנשים בבית" },
  { value: 50, label: "מאוזן" },
  { value: 75, label: "פחות אנשים בבסיס" },
  { value: 100, label: "יותר אנשים בבית" },
];

function valueToStep(value: number): number {
  // Find closest step
  let closest = 0;
  let minDist = Math.abs(value - STEPS[0].value);
  for (let i = 1; i < STEPS.length; i++) {
    const dist = Math.abs(value - STEPS[i].value);
    if (dist < minDist) {
      minDist = dist;
      closest = i;
    }
  }
  return closest;
}

export default function BalanceSlider({
  value,
  onChange,
  disabled = false,
}: BalanceSliderProps) {
  const currentStep = valueToStep(value);
  const currentLabel = STEPS[currentStep].label;

  const handleChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const stepIndex = Number(e.target.value);
      onChange(STEPS[stepIndex].value);
    },
    [onChange]
  );

  return (
    <div className="space-y-3">
      {/* Current selection label */}
      <div className="flex justify-center">
        <span className="inline-flex items-center gap-1.5 px-3 py-1 text-sm font-medium text-slate-700 bg-slate-100 rounded-lg">
          {currentLabel}
        </span>
      </div>

      {/* Slider */}
      <div className="relative">
        <input
          type="range"
          min={0}
          max={4}
          step={1}
          value={currentStep}
          onChange={handleChange}
          disabled={disabled}
          aria-label="עדיפות זמן בבית"
          aria-valuemin={0}
          aria-valuemax={4}
          aria-valuenow={currentStep}
          aria-valuetext={currentLabel}
          className="w-full h-2 appearance-none bg-gradient-to-r from-blue-400 via-slate-300 to-emerald-400 rounded-full cursor-pointer disabled:cursor-not-allowed disabled:opacity-50
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

      {/* Step labels */}
      <div className="flex justify-between px-1">
        {STEPS.map((step, i) => (
          <button
            key={i}
            type="button"
            onClick={() => !disabled && onChange(step.value)}
            disabled={disabled}
            className={`text-[10px] leading-tight text-center max-w-[60px] transition-colors ${
              currentStep === i
                ? "text-blue-600 font-semibold"
                : "text-slate-400 hover:text-slate-600"
            } disabled:cursor-not-allowed`}
          >
            {step.label}
          </button>
        ))}
      </div>
    </div>
  );
}
