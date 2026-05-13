"use client";

import { useState, useEffect, useCallback } from "react";
import { apiClient } from "@/lib/api/client";

export interface HomeLeaveConfigValues {
  minRestHours: number;
  eligibilityThresholdHours: number;
  leaveCapacity: number;
  leaveDurationHours: number;
}

const DEFAULTS: HomeLeaveConfigValues = {
  minRestHours: 8,
  eligibilityThresholdHours: 24,
  leaveCapacity: 1,
  leaveDurationHours: 48,
};

interface HomeLeaveConfigPanelProps {
  spaceId: string;
  groupId: string;
  isClosedBase: boolean;
}

/**
 * "הגדרות חופשות" (Leave Settings) panel.
 * Conditionally rendered when isClosedBase is true.
 * Fetches existing config on mount, allows editing, and saves via PUT.
 */
export default function HomeLeaveConfigPanel({
  spaceId,
  groupId,
  isClosedBase,
}: HomeLeaveConfigPanelProps) {
  const [values, setValues] = useState<HomeLeaveConfigValues>(DEFAULTS);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [permissionError, setPermissionError] = useState(false);
  const [loading, setLoading] = useState(true);

  // Fetch existing config on mount (or when group/space changes)
  const fetchConfig = useCallback(async () => {
    if (!spaceId || !groupId) return;
    setLoading(true);
    try {
      const { data } = await apiClient.get(
        `/spaces/${spaceId}/groups/${groupId}/home-leave-config`
      );
      setValues({
        minRestHours: data.minRestHours ?? DEFAULTS.minRestHours,
        eligibilityThresholdHours: data.eligibilityThresholdHours ?? DEFAULTS.eligibilityThresholdHours,
        leaveCapacity: data.leaveCapacity ?? DEFAULTS.leaveCapacity,
        leaveDurationHours: data.leaveDurationHours ?? DEFAULTS.leaveDurationHours,
      });
    } catch {
      // If fetch fails, use defaults
      setValues(DEFAULTS);
    } finally {
      setLoading(false);
    }
  }, [spaceId, groupId]);

  useEffect(() => {
    if (isClosedBase) {
      fetchConfig();
    }
  }, [isClosedBase, fetchConfig]);

  // Hide panel when not a closed base
  if (!isClosedBase) return null;

  function handleChange(field: keyof HomeLeaveConfigValues, raw: string) {
    const num = Number(raw);
    if (isNaN(num)) return;
    setValues((prev) => ({ ...prev, [field]: num }));
    // Clear field error on change
    setFieldErrors((prev) => {
      const next = { ...prev };
      delete next[field];
      return next;
    });
    setSaved(false);
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    setSaved(false);
    setFieldErrors({});
    setPermissionError(false);

    try {
      await apiClient.put(
        `/spaces/${spaceId}/groups/${groupId}/home-leave-config`,
        {
          minRestHours: values.minRestHours,
          eligibilityThresholdHours: values.eligibilityThresholdHours,
          leaveCapacity: values.leaveCapacity,
          leaveDurationHours: values.leaveDurationHours,
        }
      );
      setSaved(true);
    } catch (err: unknown) {
      const error = err as { response?: { status?: number; data?: Record<string, unknown> } };
      const status = error?.response?.status;

      if (status === 403) {
        setPermissionError(true);
      } else if (status === 400) {
        // Parse validation errors from response
        const data = error?.response?.data;
        if (data) {
          const errors: Record<string, string> = {};
          // Handle FluentValidation error format
          if (data.errors && typeof data.errors === "object") {
            const validationErrors = data.errors as Record<string, string[]>;
            for (const [key, messages] of Object.entries(validationErrors)) {
              const fieldKey = key.charAt(0).toLowerCase() + key.slice(1);
              errors[fieldKey] = Array.isArray(messages) ? messages[0] : String(messages);
            }
          } else if (data.error && typeof data.error === "string") {
            // Single error message
            errors["_general"] = data.error as string;
          }
          setFieldErrors(errors);
        }
      }
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <div className="bg-white border border-slate-200 rounded-2xl p-5 space-y-3">
        <h3 className="text-sm font-semibold text-slate-700">הגדרות חופשות</h3>
        <p className="text-sm text-slate-400">טוען...</p>
      </div>
    );
  }

  return (
    <div className="bg-white border border-slate-200 rounded-2xl p-5 space-y-3">
      <h3 className="text-sm font-semibold text-slate-700">הגדרות חופשות</h3>

      <form onSubmit={handleSubmit} className="space-y-4">
        {/* Min rest hours */}
        <FieldRow
          label="מנוחה מינימלית (שעות)"
          value={values.minRestHours}
          onChange={(v) => handleChange("minRestHours", v)}
          min={4}
          max={16}
          error={fieldErrors["minRestHours"]}
        />

        {/* Eligibility threshold hours */}
        <FieldRow
          label="סף זכאות לחופשה (שעות)"
          value={values.eligibilityThresholdHours}
          onChange={(v) => handleChange("eligibilityThresholdHours", v)}
          min={values.minRestHours}
          max={48}
          error={fieldErrors["eligibilityThresholdHours"]}
        />

        {/* Leave capacity */}
        <FieldRow
          label="כמות מקסימלית בחופשה"
          value={values.leaveCapacity}
          onChange={(v) => handleChange("leaveCapacity", v)}
          min={1}
          step={1}
          error={fieldErrors["leaveCapacity"]}
        />

        {/* Leave duration hours */}
        <FieldRow
          label="משך חופשה (שעות)"
          value={values.leaveDurationHours}
          onChange={(v) => handleChange("leaveDurationHours", v)}
          min={12}
          max={168}
          error={fieldErrors["leaveDurationHours"]}
        />

        {/* General error */}
        {fieldErrors["_general"] && (
          <p className="text-sm text-red-600">{fieldErrors["_general"]}</p>
        )}

        {/* Permission error toast */}
        {permissionError && (
          <div className="flex items-center gap-2 bg-red-50 border border-red-200 rounded-lg px-3 py-2">
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
            <span className="text-xs font-medium text-red-700">
              אין הרשאה לשנות הגדרות
            </span>
          </div>
        )}

        {/* Submit button */}
        <div className="flex items-center gap-3">
          <button
            type="submit"
            disabled={saving}
            className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
          >
            {saving ? "שומר..." : "שמור"}
          </button>
          {saved && (
            <span className="text-sm text-emerald-600">שמור ✓</span>
          )}
        </div>
      </form>
    </div>
  );
}

interface FieldRowProps {
  label: string;
  value: number;
  onChange: (value: string) => void;
  min?: number;
  max?: number;
  step?: number;
  error?: string;
}

function FieldRow({ label, value, onChange, min, max, step, error }: FieldRowProps) {
  return (
    <div className="space-y-1">
      <label className="block text-sm text-slate-600">{label}</label>
      <input
        type="number"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        min={min}
        max={max}
        step={step ?? 1}
        className={`w-full border rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 ${
          error ? "border-red-300 focus:ring-red-500" : "border-slate-200"
        }`}
      />
      {error && <p className="text-xs text-red-600">{error}</p>}
    </div>
  );
}
