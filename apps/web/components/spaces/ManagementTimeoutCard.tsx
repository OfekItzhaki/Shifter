"use client";

import { useState, useEffect } from "react";
import { useTranslations } from "next-intl";
import { updateManagementTimeout } from "@/lib/api/spaces";

interface ManagementTimeoutCardProps {
  spaceId: string;
  currentTimeout: number;
  isOwner: boolean;
}

/**
 * Card component for managing the space-level management timeout setting.
 * Displays the current timeout value and allows the Space Owner to update it.
 * Validates input is an integer in [5, 120] on the client side.
 * Only visible to the Space Owner.
 *
 * Validates: Requirements 5.1, 5.2, 5.3
 */
export default function ManagementTimeoutCard({
  spaceId,
  currentTimeout,
  isOwner,
}: ManagementTimeoutCardProps) {
  const t = useTranslations("spaces");

  const [minutes, setMinutes] = useState<string>(String(currentTimeout));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  // Sync if parent re-fetches
  useEffect(() => {
    setMinutes(String(currentTimeout));
  }, [currentTimeout]);

  // Permission gate: hide entirely for non-owners
  if (!isOwner) return null;

  const validate = (value: string): string | null => {
    const parsed = Number(value);
    if (!Number.isInteger(parsed) || parsed < 5 || parsed > 120) {
      return t("managementTimeout.validationError");
    }
    return null;
  };

  const handleSave = async () => {
    const validationError = validate(minutes);
    if (validationError) {
      setError(validationError);
      return;
    }

    setSaving(true);
    setError(null);
    setSuccess(false);

    try {
      await updateManagementTimeout(spaceId, Number(minutes));
      setSuccess(true);
    } catch {
      setError(t("managementTimeout.saveError"));
    } finally {
      setSaving(false);
    }
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setMinutes(e.target.value);
    setError(null);
    setSuccess(false);
  };

  return (
    <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
      <h2 className="text-sm font-semibold text-slate-900 dark:text-white mb-1">
        {t("managementTimeout.title")}
      </h2>
      <p className="text-xs text-slate-500 dark:text-slate-400 mb-3">
        {t("managementTimeout.description")}
      </p>

      <div className="flex items-center gap-3">
        <input
          type="number"
          min={5}
          max={120}
          step={1}
          value={minutes}
          onChange={handleChange}
          aria-label={t("managementTimeout.inputLabel")}
          className="w-24 px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white text-sm focus:outline-none focus:border-sky-500"
        />
        <span className="text-sm text-slate-500 dark:text-slate-400">
          {t("managementTimeout.minutes")}
        </span>
        <button
          onClick={handleSave}
          disabled={saving}
          className="px-4 py-2 rounded-lg bg-sky-500 hover:bg-sky-600 disabled:bg-slate-300 dark:disabled:bg-slate-600 text-white font-semibold text-sm transition-colors"
        >
          {saving ? t("saving") : t("save")}
        </button>
      </div>

      {/* Validation / error message */}
      {error && (
        <p className="mt-2 text-xs text-red-500 dark:text-red-400" role="alert">
          {error}
        </p>
      )}

      {/* Success message */}
      {success && (
        <p className="mt-2 text-xs text-green-600 dark:text-green-400" role="status">
          {t("managementTimeout.saved")}
        </p>
      )}
    </div>
  );
}
