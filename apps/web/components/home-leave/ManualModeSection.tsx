"use client";

import { useState, useEffect, useRef, useCallback } from "react";
import { useTranslations } from "next-intl";
import FeasibilityIndicator, { FeasibilityResult } from "./FeasibilityIndicator";

interface ManualModeSectionProps {
  /** Current base days value */
  baseDays: number;
  /** Current home days value */
  homeDays: number;
  /** Callback when base days changes */
  onBaseDaysChange: (days: number) => void;
  /** Callback when home days changes */
  onHomeDaysChange: (days: number) => void;
  /** Function to check feasibility — called with debounce */
  onCheckFeasibility?: (baseDays: number, homeDays: number) => Promise<FeasibilityResult>;
  /** Whether the section is disabled */
  disabled?: boolean;
}

/**
 * Manual mode section with two numeric inputs for base days and home days.
 * Validates minimum 1 for both fields.
 * Debounces feasibility API calls by 500ms.
 */
export default function ManualModeSection({
  baseDays,
  homeDays,
  onBaseDaysChange,
  onHomeDaysChange,
  onCheckFeasibility,
  disabled = false,
}: ManualModeSectionProps) {
  const t = useTranslations("homeLeave.manual");
  const [feasibility, setFeasibility] = useState<FeasibilityResult | null>(null);
  const [isFeasibilityLoading, setIsFeasibilityLoading] = useState(false);
  const [baseDaysError, setBaseDaysError] = useState<string | null>(null);
  const [homeDaysError, setHomeDaysError] = useState<string | null>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const checkFeasibility = useCallback(
    async (base: number, home: number) => {
      if (!onCheckFeasibility) return;
      if (base < 1 || home < 1) return;

      setIsFeasibilityLoading(true);
      try {
        const result = await onCheckFeasibility(base, home);
        setFeasibility(result);
      } catch {
        setFeasibility(null);
      } finally {
        setIsFeasibilityLoading(false);
      }
    },
    [onCheckFeasibility]
  );

  // Debounced feasibility check
  useEffect(() => {
    if (debounceRef.current) {
      clearTimeout(debounceRef.current);
    }

    debounceRef.current = setTimeout(() => {
      checkFeasibility(baseDays, homeDays);
    }, 500);

    return () => {
      if (debounceRef.current) {
        clearTimeout(debounceRef.current);
      }
    };
  }, [baseDays, homeDays, checkFeasibility]);

  function handleBaseDaysChange(e: React.ChangeEvent<HTMLInputElement>) {
    const val = Number(e.target.value);
    if (isNaN(val)) return;

    if (val < 1) {
      setBaseDaysError(t("minError"));
    } else {
      setBaseDaysError(null);
    }
    onBaseDaysChange(Math.max(1, val));
  }

  function handleHomeDaysChange(e: React.ChangeEvent<HTMLInputElement>) {
    const val = Number(e.target.value);
    if (isNaN(val)) return;

    if (val < 1) {
      setHomeDaysError(t("minError"));
    } else {
      setHomeDaysError(null);
    }
    onHomeDaysChange(Math.max(1, val));
  }

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-3">
        {/* Base days input */}
        <div className="space-y-1">
          <label className="block text-sm text-slate-600 font-medium">
            {t("baseDaysLabel")}
          </label>
          <input
            type="number"
            value={baseDays}
            onChange={handleBaseDaysChange}
            min={1}
            max={14}
            step={1}
            disabled={disabled}
            aria-label={t("baseDaysLabel")}
            aria-invalid={!!baseDaysError}
            className={`w-full border rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed ${
              baseDaysError
                ? "border-red-300 focus:ring-red-500"
                : "border-slate-200"
            }`}
          />
          {baseDaysError && (
            <p className="text-xs text-red-600" role="alert">
              {baseDaysError}
            </p>
          )}
        </div>

        {/* Home days input */}
        <div className="space-y-1">
          <label className="block text-sm text-slate-600 font-medium">
            {t("homeDaysLabel")}
          </label>
          <input
            type="number"
            value={homeDays}
            onChange={handleHomeDaysChange}
            min={1}
            max={7}
            step={1}
            disabled={disabled}
            aria-label={t("homeDaysLabel")}
            aria-invalid={!!homeDaysError}
            className={`w-full border rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed ${
              homeDaysError
                ? "border-red-300 focus:ring-red-500"
                : "border-slate-200"
            }`}
          />
          {homeDaysError && (
            <p className="text-xs text-red-600" role="alert">
              {homeDaysError}
            </p>
          )}
        </div>
      </div>

      {/* Feasibility feedback */}
      <FeasibilityIndicator
        feasibilityResult={feasibility}
        isLoading={isFeasibilityLoading}
      />
    </div>
  );
}
