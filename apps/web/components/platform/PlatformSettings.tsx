"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { apiClient } from "@/lib/api/client";

const MIN_TIMEOUT = 5;
const MAX_TIMEOUT = 120;

export default function PlatformSettings() {
  const t = useTranslations("platform");

  const [timeoutMinutes, setTimeoutMinutes] = useState<number>(15);
  const [inputValue, setInputValue] = useState<string>("15");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);

  useEffect(() => {
    apiClient
      .get<{ platformTimeoutMinutes: number }>("/platform/settings")
      .then(({ data }) => {
        const value = data.platformTimeoutMinutes ?? 15;
        setTimeoutMinutes(value);
        setInputValue(String(value));
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  function validateInput(value: string): string | null {
    const num = Number(value);
    if (value === "" || isNaN(num)) return t("timeoutValidationRequired");
    if (!Number.isInteger(num)) return t("timeoutValidationInteger");
    if (num < MIN_TIMEOUT || num > MAX_TIMEOUT) return t("timeoutValidationRange", { min: MIN_TIMEOUT, max: MAX_TIMEOUT });
    return null;
  }

  function handleInputChange(value: string) {
    setInputValue(value);
    setSaved(false);
    setValidationError(validateInput(value));
  }

  async function handleSave(e: React.FormEvent) {
    e.preventDefault();
    const err = validateInput(inputValue);
    if (err) { setValidationError(err); return; }

    const newValue = Number(inputValue);
    setSaving(true);
    setError(null);
    setSaved(false);

    try {
      await apiClient.patch("/platform/settings", { platformTimeoutMinutes: newValue });
      setTimeoutMinutes(newValue);
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch (err: unknown) {
      const axiosErr = err as { response?: { data?: { error?: string } } };
      setError(axiosErr?.response?.data?.error || t("settingsSaveError"));
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
        <h2 className="text-sm font-bold text-slate-900 dark:text-white mb-3">{t("platformSettings")}</h2>
        <p className="text-xs text-slate-400">{t("loading")}</p>
      </div>
    );
  }

  return (
    <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
      <h2 className="text-sm font-bold text-slate-900 dark:text-white mb-4">{t("platformSettings")}</h2>

      <form onSubmit={handleSave} className="flex flex-col gap-3 max-w-sm">
        <div>
          <label htmlFor="platformTimeoutMinutes" className="block text-xs font-semibold text-slate-600 dark:text-slate-300 mb-1">
            {t("sessionTimeoutLabel")}
          </label>
          <p className="text-xs text-slate-400 dark:text-slate-500 mb-2">
            {t("sessionTimeoutDescription")}
          </p>
          <div className="flex items-center gap-2">
            <input
              id="platformTimeoutMinutes"
              type="number"
              min={MIN_TIMEOUT}
              max={MAX_TIMEOUT}
              step={1}
              value={inputValue}
              onChange={(e) => handleInputChange(e.target.value)}
              aria-invalid={!!validationError}
              className={`w-20 px-3 py-2 rounded-lg border text-center text-sm bg-white dark:bg-slate-700 text-slate-900 dark:text-white focus:outline-none focus:border-sky-500 ${validationError ? "border-red-300 dark:border-red-700" : "border-slate-200 dark:border-slate-600"}`}
            />
            <span className="text-xs text-slate-500 dark:text-slate-400">{t("minutes")}</span>
          </div>
          <p className="text-[10px] text-slate-400 dark:text-slate-500 mt-1">
            {t("timeoutRangeHint", { min: MIN_TIMEOUT, max: MAX_TIMEOUT })}
          </p>
          {validationError && (
            <p className="text-xs text-red-600 dark:text-red-400 mt-1" role="alert">{validationError}</p>
          )}
        </div>

        <div className="flex items-center gap-3">
          <button
            type="submit"
            disabled={saving || !!validationError}
            className="bg-sky-500 hover:bg-sky-600 text-white rounded-lg px-4 py-2 text-xs font-semibold disabled:opacity-50 cursor-pointer border-none transition-colors"
          >
            {saving ? "..." : t("saveSettings")}
          </button>
          {saved && (
            <span className="text-xs text-emerald-500 font-semibold">✓ {t("saved")}</span>
          )}
        </div>

        {error && <p className="text-xs text-red-600 dark:text-red-400">{error}</p>}
      </form>
    </div>
  );
}
