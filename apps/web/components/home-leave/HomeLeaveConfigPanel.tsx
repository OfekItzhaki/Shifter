"use client";

import { useState, useEffect, useCallback } from "react";
import { useTranslations } from "next-intl";
import {
  getHomeLeaveConfig,
  updateHomeLeaveConfig,
  toggleEmergencyFreeze,
  HomeLeaveMode,
  getHomeLeavePreview,
  DeactivateFreezeResponse,
} from "@/lib/api/homeLeave";
import ModeSelector from "./ModeSelector";
import RatioSlider from "./RatioSlider";
import ManualModeSection from "./ManualModeSection";
import EmergencyFreezeBanner from "./EmergencyFreezeBanner";
import FeasibilityIndicator, { FeasibilityResult } from "./FeasibilityIndicator";
import { useHomeLeavePreview } from "@/hooks/useHomeLeavePreview";
import RecommendationCard from "@/components/recommendations/RecommendationCard";

interface HomeLeaveConfigPanelProps {
  spaceId: string;
  groupId: string;
  isClosedBase: boolean;
  memberCount: number;
  /** Whether the user is in admin mode (used to determine schedule.rollback permission) */
  isAdmin?: boolean;
}

/**
 * "ניהול זמן בבית" (Home Time Management) panel.
 * Conditionally rendered when isClosedBase is true.
 * Integrates ModeSelector, RatioSlider, ManualModeSection,
 * EmergencyFreezeBanner, and FeasibilityIndicator.
 */
export default function HomeLeaveConfigPanel({
  spaceId,
  groupId,
  isClosedBase,
  memberCount,
  isAdmin = false,
}: HomeLeaveConfigPanelProps) {
  const t = useTranslations("homeLeave");

  // Config state
  const [mode, setMode] = useState<HomeLeaveMode>("automatic");
  const [baseDays, setBaseDays] = useState(7);
  const [homeDays, setHomeDays] = useState(2);
  const [sliderValue, setSliderValue] = useState(50);
  const [minPeopleAtBase, setMinPeopleAtBase] = useState(8);
  const [balanceValue, setBalanceValue] = useState(50);

  // Emergency freeze state
  const [emergencyFreezeActive, setEmergencyFreezeActive] = useState(false);
  const [emergencyUseForScheduling, setEmergencyUseForScheduling] = useState(false);
  const [freezeStartedAt, setFreezeStartedAt] = useState<string | null>(null);

  // Optimal ratio (from API)
  const [optimalBaseDays, setOptimalBaseDays] = useState(7);
  const [optimalHomeDays, setOptimalHomeDays] = useState(2);

  // UI state
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [loading, setLoading] = useState(true);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [permissionError, setPermissionError] = useState(false);

  // Feasibility for automatic mode (from preview hook)
  const previewRequest = !loading && mode === "automatic"
    ? { mode: "automatic" as const, sliderValue, leaveDurationHours: homeDays * 24 }
    : null;

  const preview = useHomeLeavePreview(
    isClosedBase && !loading ? spaceId : null,
    isClosedBase && !loading ? groupId : null,
    previewRequest
  );

  // Convert preview feasibility to FeasibilityResult for the indicator
  const automaticFeasibility: FeasibilityResult | null = preview.data?.feasibility
    ? {
        isFeasible: preview.data.feasibility.isFeasible,
        maxFeasibleHomeDays: preview.data.feasibility.maxFeasibleHomeDays,
        reason: preview.data.feasibility.reason,
      }
    : null;

  // Fetch existing config on mount
  const fetchConfig = useCallback(async () => {
    if (!spaceId || !groupId) return;
    setLoading(true);
    try {
      const config = await getHomeLeaveConfig(spaceId, groupId);
      setMode((config.mode as HomeLeaveMode) ?? "automatic");
      setBaseDays(config.baseDays ?? 7);
      setHomeDays(config.homeDays ?? 2);
      setMinPeopleAtBase(config.minPeopleAtBase ?? 8);
      setBalanceValue(config.balanceValue ?? 50);
      setSliderValue(config.balanceValue ?? 50);
      setEmergencyFreezeActive(config.emergencyFreezeActive ?? false);
      setEmergencyUseForScheduling(config.emergencyUseForScheduling ?? false);
      setFreezeStartedAt(config.freezeStartedAt ?? null);
      setOptimalBaseDays(config.optimalBaseDays ?? 7);
      setOptimalHomeDays(config.optimalHomeDays ?? 2);
    } catch {
      // Use defaults on error
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

  // Manual mode feasibility check callback
  async function handleManualFeasibilityCheck(
    base: number,
    home: number
  ): Promise<FeasibilityResult> {
    const result = await getHomeLeavePreview(spaceId, groupId, {
      mode: "manual",
      baseDays: base,
      homeDays: home,
      leaveDurationHours: home * 24,
    });
    if (result.feasibility) {
      return {
        isFeasible: result.feasibility.isFeasible,
        maxFeasibleHomeDays: result.feasibility.maxFeasibleHomeDays,
        reason: result.feasibility.reason,
      };
    }
    return { isFeasible: true };
  }

  // Emergency freeze toggle
  async function handleToggleFreeze(active: boolean) {
    try {
      const result = await toggleEmergencyFreeze(
        spaceId,
        groupId,
        active,
        emergencyUseForScheduling
      );
      setEmergencyFreezeActive(result.emergencyFreezeActive);
      setEmergencyUseForScheduling(result.emergencyUseForScheduling);
      setFreezeStartedAt(result.freezeStartedAt);
      // Restore mode if deactivating
      if (!active) {
        setMode(result.mode as HomeLeaveMode);
        setBaseDays(result.baseDays);
        setHomeDays(result.homeDays);
        setBalanceValue(result.balanceValue);
        setSliderValue(result.balanceValue);
      }
    } catch (err: unknown) {
      const error = err as { response?: { status?: number } };
      if (error?.response?.status === 403) {
        setPermissionError(true);
      }
    }
  }

  // Handle successful deactivation via the deactivation dialog
  function handleDeactivateSuccess(response: DeactivateFreezeResponse) {
    const config = response.config;
    setEmergencyFreezeActive(config.emergencyFreezeActive);
    setEmergencyUseForScheduling(config.emergencyUseForScheduling);
    setFreezeStartedAt(config.freezeStartedAt);
    setMode(config.mode as HomeLeaveMode);
    setBaseDays(config.baseDays);
    setHomeDays(config.homeDays);
    setBalanceValue(config.balanceValue);
    setSliderValue(config.balanceValue);
  }

  // Emergency use-for-scheduling change
  async function handleUseForSchedulingChange(useForScheduling: boolean) {
    setEmergencyUseForScheduling(useForScheduling);
    // Persist immediately
    try {
      await toggleEmergencyFreeze(spaceId, groupId, true, useForScheduling);
    } catch {
      // Revert on error
      setEmergencyUseForScheduling(!useForScheduling);
    }
  }

  // Mode change
  function handleModeChange(newMode: HomeLeaveMode) {
    setMode(newMode);
    setSaved(false);
  }

  // Slider change
  function handleSliderChange(value: number) {
    setSliderValue(value);
    setSaved(false);
  }

  // Save configuration
  async function handleSave(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    setSaved(false);
    setFieldErrors({});
    setPermissionError(false);

    try {
      const payload = {
        mode,
        baseDays: mode === "manual" ? baseDays : undefined,
        homeDays: mode === "manual" ? homeDays : undefined,
        sliderValue: mode === "automatic" ? sliderValue : undefined,
        leaveDurationHours: homeDays * 24,
        minPeopleAtBase,
        emergencyFreezeActive,
        emergencyUseForScheduling,
      };

      const result = await updateHomeLeaveConfig(spaceId, groupId, payload);

      // Update local state from response
      setOptimalBaseDays(result.optimalBaseDays);
      setOptimalHomeDays(result.optimalHomeDays);
      setBaseDays(result.baseDays);
      setHomeDays(result.homeDays);
      setBalanceValue(result.balanceValue);
      setSaved(true);
    } catch (err: unknown) {
      const error = err as { response?: { status?: number; data?: Record<string, unknown> } };
      const status = error?.response?.status;

      if (status === 403) {
        setPermissionError(true);
      } else if (status === 400) {
        const data = error?.response?.data;
        if (data) {
          const errors: Record<string, string> = {};
          if (data.errors && typeof data.errors === "object") {
            const validationErrors = data.errors as Record<string, string[]>;
            for (const [key, messages] of Object.entries(validationErrors)) {
              const fieldKey = key.charAt(0).toLowerCase() + key.slice(1);
              errors[fieldKey] = Array.isArray(messages) ? messages[0] : String(messages);
            }
          } else if (data.message && typeof data.message === "string") {
            errors["_general"] = data.message as string;
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
        <h3 className="text-sm font-semibold text-slate-700">{t("panel.title")}</h3>
        <p className="text-sm text-slate-400">{t("panel.loading")}</p>
      </div>
    );
  }

  return (
    <div className="bg-white border border-slate-200 rounded-2xl p-5 space-y-4">
      <div>
        <h3 className="text-sm font-semibold text-slate-700">{t("panel.title")}</h3>
        <p className="text-xs text-slate-400 mt-0.5">{t("panel.description")}</p>
      </div>

      {/* Recommendation Card — above emergency freeze */}
      <RecommendationCard spaceId={spaceId} groupId={groupId} />

      {/* Emergency Freeze Banner — always visible at top */}
      <EmergencyFreezeBanner
        isActive={emergencyFreezeActive}
        useForScheduling={emergencyUseForScheduling}
        freezeStartedAt={freezeStartedAt}
        onToggleFreeze={handleToggleFreeze}
        onUseForSchedulingChange={handleUseForSchedulingChange}
        disabled={saving}
        spaceId={spaceId}
        groupId={groupId}
        // TODO: Query actual schedule.rollback permission from backend instead of using isAdmin as proxy
        canRollback={isAdmin}
        onDeactivateSuccess={handleDeactivateSuccess}
      />

      {/* Mode Selector */}
      {!emergencyFreezeActive && (
        <div className="space-y-4">
          {/* Minimum people at base input */}
          <div className="space-y-1">
            <label htmlFor="minPeopleAtBase" className="text-sm font-medium text-slate-700">
              {t("minPeopleAtBase.label")}
            </label>
            <p className="text-xs text-slate-400">{t("minPeopleAtBase.hint")}</p>
            <input
              id="minPeopleAtBase"
              type="number"
              min={1}
              max={memberCount - 1}
              value={minPeopleAtBase}
              onChange={(e) => {
                const val = Math.max(1, Math.min(memberCount - 1, parseInt(e.target.value) || 1));
                setMinPeopleAtBase(val);
                setSaved(false);
              }}
              disabled={saving}
              className="w-24 rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-700 focus:border-blue-400 focus:ring-1 focus:ring-blue-400 disabled:opacity-50"
            />
            {minPeopleAtBase >= memberCount && (
              <p className="text-xs text-red-500">{t("minPeopleAtBase.maxError")}</p>
            )}
          </div>

          <ModeSelector
            value={mode}
            onChange={handleModeChange}
            disabled={saving}
          />

          {/* Automatic Mode: RatioSlider + Feasibility */}
          {mode === "automatic" && (
            <div className="space-y-3">
              <RatioSlider
                optimalBaseDays={optimalBaseDays}
                optimalHomeDays={optimalHomeDays}
                value={sliderValue}
                onChange={handleSliderChange}
                disabled={saving}
              />
              <FeasibilityIndicator
                feasibilityResult={automaticFeasibility}
                isLoading={preview.isLoading}
              />
            </div>
          )}

          {/* Manual Mode: ManualModeSection (includes its own FeasibilityIndicator) */}
          {mode === "manual" && (
            <ManualModeSection
              baseDays={baseDays}
              homeDays={homeDays}
              onBaseDaysChange={(days) => { setBaseDays(days); setSaved(false); }}
              onHomeDaysChange={(days) => { setHomeDays(days); setSaved(false); }}
              onCheckFeasibility={handleManualFeasibilityCheck}
              disabled={saving}
            />
          )}


        </div>
      )}

      {/* Save form */}
      <form onSubmit={handleSave} className="space-y-3 pt-2 border-t border-slate-100">
        {/* General error */}
        {fieldErrors["_general"] && (
          <p className="text-sm text-red-600">{fieldErrors["_general"]}</p>
        )}

        {/* Permission error */}
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
              {t("panel.permissionError")}
            </span>
          </div>
        )}

        {/* Submit button */}
        <div className="flex items-center gap-3">
          <button
            type="submit"
            disabled={saving || emergencyFreezeActive}
            className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
          >
            {saving ? t("panel.saving") : t("panel.save")}
          </button>
          {saved && (
            <span className="text-sm text-emerald-600">{t("panel.saved")}</span>
          )}
        </div>
      </form>
    </div>
  );
}
