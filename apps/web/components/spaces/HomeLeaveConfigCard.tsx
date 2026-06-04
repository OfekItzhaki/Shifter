"use client";

import { useState, useEffect, useCallback } from "react";
import { useTranslations, useLocale } from "next-intl";
import { isRtl as isRtlLocale } from "@/lib/i18n/locales";
import {
  getHomeLeaveConfig,
  updateHomeLeaveConfig,
  SpaceHomeLeaveConfigDto,
  SpaceHomeLeaveMode,
  UpdateSpaceHomeLeaveConfigPayload,
} from "@/lib/api/spaces";

interface Props {
  spaceId: string;
  /** Only render the card if the user is the space owner */
  isOwner: boolean;
}

type LoadingState = "loading" | "loaded" | "error";

/**
 * Space-level home-leave configuration card for the space settings page.
 * Permission-gated: only visible to the space owner.
 * Provides mode selection (Automatic/Manual/Disabled), ratio slider,
 * manual mode fields, and emergency freeze toggle.
 */
export default function HomeLeaveConfigCard({ spaceId, isOwner }: Props) {
  const t = useTranslations("spaceHomeLeave");
  const locale = useLocale();
  const isRtl = isRtlLocale(locale);

  const [loadingState, setLoadingState] = useState<LoadingState>("loading");
  const [config, setConfig] = useState<SpaceHomeLeaveConfigDto | null>(null);

  // Form state
  const [mode, setMode] = useState<SpaceHomeLeaveMode>("automatic");
  const [balanceValue, setBalanceValue] = useState(50);
  const [baseDays, setBaseDays] = useState(7);
  const [homeDays, setHomeDays] = useState(2);
  const [minPeopleAtBase, setMinPeopleAtBase] = useState(8);
  const [emergencyFreezeActive, setEmergencyFreezeActive] = useState(false);
  const [emergencyUseForScheduling, setEmergencyUseForScheduling] = useState(false);

  // UI state
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchConfig = useCallback(async () => {
    if (!spaceId) return;
    setLoadingState("loading");
    try {
      const data = await getHomeLeaveConfig(spaceId);
      setConfig(data);
      if (data) {
        setMode(data.mode);
        setBalanceValue(data.balanceValue);
        setBaseDays(data.baseDays);
        setHomeDays(data.homeDays);
        setMinPeopleAtBase(data.minPeopleAtBase);
        setEmergencyFreezeActive(data.emergencyFreezeActive);
        setEmergencyUseForScheduling(data.emergencyUseForScheduling);
      }
      setLoadingState("loaded");
    } catch {
      setLoadingState("error");
    }
  }, [spaceId]);

  useEffect(() => {
    if (isOwner) {
      fetchConfig();
    }
  }, [isOwner, fetchConfig]);

  const handleSave = useCallback(async () => {
    if (!spaceId) return;
    setSaving(true);
    setSaved(false);
    setError(null);

    try {
      const payload: UpdateSpaceHomeLeaveConfigPayload = {
        mode,
        balanceValue,
        baseDays,
        homeDays,
        minPeopleAtBase,
        minRestHours: config?.minRestHours ?? 0,
        eligibilityThresholdHours: config?.eligibilityThresholdHours ?? 168,
        leaveCapacity: config?.leaveCapacity ?? 1,
        leaveDurationHours: config?.leaveDurationHours ?? 48,
        emergencyFreezeActive,
        emergencyUseForScheduling,
      };
      await updateHomeLeaveConfig(spaceId, payload);
      setSaved(true);
    } catch {
      setError(t("saveError"));
    } finally {
      setSaving(false);
    }
  }, [
    spaceId, mode, balanceValue, baseDays, homeDays, minPeopleAtBase,
    emergencyFreezeActive, emergencyUseForScheduling, config, t,
  ]);

  // Permission gate: hide entirely for non-owners
  if (!isOwner) return null;

  if (loadingState === "loading") {
    return (
      <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-900 dark:text-white mb-3">
          {t("title")}
        </h2>
        <div className="flex items-center justify-center py-4 text-slate-500 dark:text-slate-400 text-sm">
          {t("loading")}
        </div>
      </div>
    );
  }

  if (loadingState === "error") {
    return (
      <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-900 dark:text-white mb-3">
          {t("title")}
        </h2>
        <div className="flex flex-col items-center gap-3 py-4">
          <p className="text-sm text-slate-500 dark:text-slate-400">
            {t("loadError")}
          </p>
          <button
            onClick={fetchConfig}
            className="px-4 py-2 rounded-lg bg-sky-500 hover:bg-sky-600 text-white font-semibold text-sm transition-colors"
          >
            {t("retry")}
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
      <div className="mb-4">
        <h2 className="text-sm font-semibold text-slate-900 dark:text-white">
          {t("title")}
        </h2>
        <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
          {t("description")}
        </p>
      </div>

      <div className="space-y-4">
        {/* Mode Selector */}
        <div>
          <label className="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-2">
            {t("modeLabel")}
          </label>
          <div
            role="radiogroup"
            aria-label={t("modeLabel")}
            className="inline-flex rounded-xl bg-slate-100 dark:bg-slate-700 p-1 gap-1"
          >
            <ModeButton
              label={t("modeAutomatic")}
              active={mode === "automatic"}
              disabled={saving}
              onClick={() => { setMode("automatic"); setSaved(false); }}
            />
            <ModeButton
              label={t("modeManual")}
              active={mode === "manual"}
              disabled={saving}
              onClick={() => { setMode("manual"); setSaved(false); }}
            />
            <ModeButton
              label={t("modeDisabled")}
              active={mode === "disabled"}
              disabled={saving}
              onClick={() => { setMode("disabled"); setSaved(false); }}
            />
          </div>
        </div>

        {/* Automatic Mode: Ratio Slider */}
        {mode === "automatic" && (
          <div className="space-y-2">
            <label className="block text-xs font-medium text-slate-600 dark:text-slate-300">
              {t("balanceLabel")}
            </label>
            <div className="flex items-center gap-3">
              <input
                type="range"
                min={0}
                max={100}
                step={5}
                value={balanceValue}
                onChange={(e) => { setBalanceValue(Number(e.target.value)); setSaved(false); }}
                disabled={saving}
                dir={isRtl ? "rtl" : "ltr"}
                aria-label={t("balanceLabel")}
                aria-valuemin={0}
                aria-valuemax={100}
                aria-valuenow={balanceValue}
                className="flex-1 h-2 appearance-none bg-gradient-to-r from-sky-400 via-slate-300 to-emerald-400 rounded-full cursor-pointer disabled:cursor-not-allowed disabled:opacity-50
                  [&::-webkit-slider-thumb]:appearance-none
                  [&::-webkit-slider-thumb]:w-5
                  [&::-webkit-slider-thumb]:h-5
                  [&::-webkit-slider-thumb]:rounded-full
                  [&::-webkit-slider-thumb]:bg-white
                  [&::-webkit-slider-thumb]:border-2
                  [&::-webkit-slider-thumb]:border-sky-500
                  [&::-webkit-slider-thumb]:shadow-md
                  [&::-moz-range-thumb]:w-5
                  [&::-moz-range-thumb]:h-5
                  [&::-moz-range-thumb]:rounded-full
                  [&::-moz-range-thumb]:bg-white
                  [&::-moz-range-thumb]:border-2
                  [&::-moz-range-thumb]:border-sky-500
                  [&::-moz-range-thumb]:shadow-md"
              />
              <span className="text-sm font-medium text-slate-700 dark:text-slate-200 w-10 text-center">
                {balanceValue}
              </span>
            </div>
            <div className="flex justify-between px-1">
              <span className="text-xs text-sky-600 dark:text-sky-400">{t("moreBase")}</span>
              <span className="text-xs text-emerald-600 dark:text-emerald-400">{t("moreHome")}</span>
            </div>
          </div>
        )}

        {/* Manual Mode: Base days, Home days, Min people at base */}
        {mode === "manual" && (
          <div className="grid grid-cols-3 gap-3">
            <div className="space-y-1">
              <label className="block text-xs font-medium text-slate-600 dark:text-slate-300">
                {t("baseDaysLabel")}
              </label>
              <input
                type="number"
                min={1}
                max={14}
                value={baseDays}
                onChange={(e) => { setBaseDays(Math.max(1, Number(e.target.value))); setSaved(false); }}
                disabled={saving}
                aria-label={t("baseDaysLabel")}
                className="w-full px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white text-sm disabled:opacity-50 focus:outline-none focus:border-sky-500"
              />
            </div>
            <div className="space-y-1">
              <label className="block text-xs font-medium text-slate-600 dark:text-slate-300">
                {t("homeDaysLabel")}
              </label>
              <input
                type="number"
                min={1}
                max={7}
                value={homeDays}
                onChange={(e) => { setHomeDays(Math.max(1, Number(e.target.value))); setSaved(false); }}
                disabled={saving}
                aria-label={t("homeDaysLabel")}
                className="w-full px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white text-sm disabled:opacity-50 focus:outline-none focus:border-sky-500"
              />
            </div>
            <div className="space-y-1">
              <label className="block text-xs font-medium text-slate-600 dark:text-slate-300">
                {t("minPeopleLabel")}
              </label>
              <input
                type="number"
                min={1}
                value={minPeopleAtBase}
                onChange={(e) => { setMinPeopleAtBase(Math.max(1, Number(e.target.value))); setSaved(false); }}
                disabled={saving}
                aria-label={t("minPeopleLabel")}
                className="w-full px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white text-sm disabled:opacity-50 focus:outline-none focus:border-sky-500"
              />
            </div>
          </div>
        )}

        {/* Emergency Freeze Toggle */}
        <div className="border-t border-slate-100 dark:border-slate-700 pt-4 space-y-3">
          <div className="flex items-center justify-between">
            <div>
              <span className="text-sm font-medium text-slate-700 dark:text-slate-200">
                {t("emergencyFreezeLabel")}
              </span>
              <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
                {t("emergencyFreezeHint")}
              </p>
            </div>
            <button
              type="button"
              role="switch"
              aria-checked={emergencyFreezeActive}
              aria-label={t("emergencyFreezeLabel")}
              onClick={() => { setEmergencyFreezeActive(!emergencyFreezeActive); setSaved(false); }}
              disabled={saving}
              className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-500 disabled:opacity-50 ${
                emergencyFreezeActive
                  ? "bg-red-500"
                  : "bg-slate-300 dark:bg-slate-600"
              }`}
            >
              <span
                className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${
                  emergencyFreezeActive ? "translate-x-6" : "translate-x-1"
                }`}
              />
            </button>
          </div>

          {/* Use for scheduling sub-option */}
          {emergencyFreezeActive && (
            <div className="flex items-center gap-3 ps-4">
              <input
                type="checkbox"
                id="useForScheduling"
                checked={emergencyUseForScheduling}
                onChange={(e) => { setEmergencyUseForScheduling(e.target.checked); setSaved(false); }}
                disabled={saving}
                className="h-4 w-4 rounded border-slate-300 dark:border-slate-600 text-sky-500 focus:ring-sky-500"
              />
              <label
                htmlFor="useForScheduling"
                className="text-sm text-slate-600 dark:text-slate-300"
              >
                {t("useForSchedulingLabel")}
              </label>
            </div>
          )}
        </div>

        {/* Save Button */}
        <div className="flex items-center gap-3 pt-2">
          <button
            onClick={handleSave}
            disabled={saving}
            className="px-4 py-2 rounded-lg bg-sky-500 hover:bg-sky-600 disabled:bg-slate-300 dark:disabled:bg-slate-600 text-white font-semibold text-sm transition-colors"
          >
            {saving ? t("saving") : t("save")}
          </button>
          {saved && (
            <span className="text-sm text-emerald-600 dark:text-emerald-400">
              {t("saved")}
            </span>
          )}
          {error && (
            <span className="text-sm text-red-600 dark:text-red-400">
              {error}
            </span>
          )}
        </div>
      </div>
    </div>
  );
}

// ── Mode Button ───────────────────────────────────────────────────────────────

function ModeButton({
  label,
  active,
  disabled,
  onClick,
}: {
  label: string;
  active: boolean;
  disabled: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      role="radio"
      aria-checked={active}
      tabIndex={active ? 0 : -1}
      disabled={disabled}
      onClick={onClick}
      className={`px-3 py-1.5 text-sm font-medium rounded-lg transition-all focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-500 ${
        active
          ? "bg-white dark:bg-slate-600 text-sky-700 dark:text-sky-300 shadow-sm"
          : "text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-200"
      } ${disabled ? "opacity-50 cursor-not-allowed" : "cursor-pointer"}`}
    >
      {label}
    </button>
  );
}
