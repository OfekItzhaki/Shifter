"use client";

import { useEffect, useState, useCallback } from "react";
import { useTranslations } from "next-intl";
import {
  getSelfServiceConfig,
  updateSelfServiceConfig,
  SelfServiceConfigDto,
  UpdateSelfServiceConfigPayload,
} from "@/lib/api/selfService";
import { validateSelfServiceConfig } from "@/lib/utils/selfServiceValidation";
import { getSelfServiceErrorMessage } from "@/lib/utils/selfServiceErrors";
import LoadingCard from "./LoadingCard";
import ErrorRetry from "./ErrorRetry";
import MutationButton from "./MutationButton";

interface SelfServiceConfigTabProps {
  spaceId: string;
  groupId: string;
}

/** Field configuration for rendering config inputs */
interface ConfigField {
  key: keyof UpdateSelfServiceConfigPayload;
  min: number;
  max: number;
}

const CONFIG_FIELDS: ConfigField[] = [
  { key: "minShiftsPerCycle", min: 0, max: 100 },
  { key: "maxShiftsPerCycle", min: 1, max: 100 },
  { key: "requestWindowOpenOffsetHours", min: 1, max: 720 },
  { key: "requestWindowCloseOffsetHours", min: 1, max: 720 },
  { key: "cancellationCutoffHours", min: 1, max: 720 },
  { key: "maxLateCancellationsPerCycle", min: 0, max: 100 },
  { key: "lateCancellationWindowHours", min: 1, max: 720 },
  { key: "waitlistOfferMinutes", min: 1, max: 1440 },
  { key: "cycleDurationDays", min: 1, max: 365 },
];

export default function SelfServiceConfigTab({ spaceId, groupId }: SelfServiceConfigTabProps) {
  const t = useTranslations("selfService.config");

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Form state
  const [formValues, setFormValues] = useState<UpdateSelfServiceConfigPayload>({
    minShiftsPerCycle: 0,
    maxShiftsPerCycle: 5,
    requestWindowOpenOffsetHours: 168,
    requestWindowCloseOffsetHours: 24,
    cancellationCutoffHours: 24,
    maxLateCancellationsPerCycle: 2,
    lateCancellationWindowHours: 24,
    waitlistOfferMinutes: 60,
    cycleDurationDays: 7,
  });

  // Submission state
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);

  const fetchConfig = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const config = await getSelfServiceConfig(spaceId, groupId);
      setFormValues({
        minShiftsPerCycle: config.minShiftsPerCycle,
        maxShiftsPerCycle: config.maxShiftsPerCycle,
        requestWindowOpenOffsetHours: config.requestWindowOpenOffsetHours,
        requestWindowCloseOffsetHours: config.requestWindowCloseOffsetHours,
        cancellationCutoffHours: config.cancellationCutoffHours,
        maxLateCancellationsPerCycle: config.maxLateCancellationsPerCycle,
        lateCancellationWindowHours: config.lateCancellationWindowHours,
        waitlistOfferMinutes: config.waitlistOfferMinutes,
        cycleDurationDays: config.cycleDurationDays,
      });
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setError(message);
    } finally {
      setLoading(false);
    }
  }, [spaceId, groupId]);

  useEffect(() => {
    void Promise.resolve().then(fetchConfig);
  }, [fetchConfig]);

  // ── Form handlers ────────────────────────────────────────────────────────

  function handleFieldChange(key: keyof UpdateSelfServiceConfigPayload, value: string) {
    const numValue = value === "" ? 0 : parseInt(value, 10);
    if (isNaN(numValue)) return;

    setFormValues((prev) => ({ ...prev, [key]: numValue }));
    setValidationError(null);
    setSaveError(null);
    setSaveSuccess(false);
  }

  async function handleSave() {
    // Client-side validation
    const validation = validateSelfServiceConfig(formValues);
    if (!validation.valid) {
      setValidationError(validation.errorKey ?? null);
      return;
    }

    setSaving(true);
    setSaveError(null);
    setSaveSuccess(false);
    setValidationError(null);

    try {
      await updateSelfServiceConfig(spaceId, groupId, formValues);
      setSaveSuccess(true);
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setSaveError(message);
    } finally {
      setSaving(false);
    }
  }

  // ── Loading state ────────────────────────────────────────────────────────

  if (loading) {
    return <LoadingCard rows={7} variant="form" />;
  }

  // ── Error state ──────────────────────────────────────────────────────────

  if (error) {
    return <ErrorRetry message={error} onRetry={fetchConfig} />;
  }

  // ── Render ───────────────────────────────────────────────────────────────

  return (
    <div className="space-y-5">
      <div className="bg-white border border-slate-200 rounded-xl p-6">
        <h3 className="text-base font-semibold text-slate-900 mb-6">{t("title")}</h3>

        <div className="space-y-5">
          {CONFIG_FIELDS.map((field) => (
            <div key={field.key}>
              <label
                htmlFor={`config-${field.key}`}
                className="block text-sm font-medium text-slate-700 mb-1.5"
              >
                {t(field.key)}
              </label>
              <input
                id={`config-${field.key}`}
                type="number"
                min={field.min}
                max={field.max}
                value={formValues[field.key]}
                onChange={(e) => handleFieldChange(field.key, e.target.value)}
                className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-sky-400 focus:border-transparent"
                dir="ltr"
              />
            </div>
          ))}
        </div>

        {/* Validation error */}
        {validationError && (
          <div className="mt-4 bg-red-50 border border-red-200 rounded-lg px-4 py-2.5">
            <p className="text-sm text-red-600">{validationError}</p>
          </div>
        )}

        {/* API error */}
        {saveError && (
          <div className="mt-4 bg-red-50 border border-red-200 rounded-lg px-4 py-2.5">
            <p className="text-sm text-red-600">{saveError}</p>
          </div>
        )}

        {/* Success message */}
        {saveSuccess && (
          <div className="mt-4 bg-emerald-50 border border-emerald-200 rounded-lg px-4 py-2.5">
            <p className="text-sm text-emerald-700">{t("saved")}</p>
          </div>
        )}

        {/* Save button */}
        <div className="mt-6">
          <MutationButton
            onClick={handleSave}
            loading={saving}
            label={t("save")}
            loadingLabel={t("saving")}
            variant="primary"
          />
        </div>
      </div>
    </div>
  );
}
