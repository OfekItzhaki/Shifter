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
  unit: "shifts" | "hours" | "minutes" | "days" | "reports";
}

interface ConfigSection {
  key: "shiftLimits" | "requestWindow" | "changesAbsence" | "waitlist";
  fields: ConfigField[];
}

const CONFIG_SECTIONS: ConfigSection[] = [
  {
    key: "shiftLimits",
    fields: [
      { key: "minShiftsPerCycle", min: 0, max: 100, unit: "shifts" },
      { key: "maxShiftsPerCycle", min: 1, max: 100, unit: "shifts" },
      { key: "cycleDurationDays", min: 1, max: 30, unit: "days" },
    ],
  },
  {
    key: "requestWindow",
    fields: [
      { key: "requestWindowOpenOffsetHours", min: 1, max: 720, unit: "hours" },
      { key: "requestWindowCloseOffsetHours", min: 1, max: 720, unit: "hours" },
    ],
  },
  {
    key: "changesAbsence",
    fields: [
      { key: "cancellationCutoffHours", min: 1, max: 720, unit: "hours" },
      { key: "lateCancellationWindowHours", min: 1, max: 720, unit: "hours" },
      { key: "maxLateCancellationsPerCycle", min: 0, max: 100, unit: "reports" },
    ],
  },
  {
    key: "waitlist",
    fields: [
      { key: "waitlistOfferMinutes", min: 15, max: 1440, unit: "minutes" },
    ],
  },
];

function getConfigValidationMessage(
  errorKey: string,
  t: (key: string) => string
): string {
  switch (errorKey) {
    case "selfService.errors.minShiftsOutOfRange":
      return t("validation.minShiftsRange");
    case "selfService.errors.maxShiftsOutOfRange":
      return t("validation.maxShiftsRange");
    case "selfService.errors.minExceedsMax":
      return t("validation.minGreaterThanMax");
    case "selfService.errors.openOffsetOutOfRange":
    case "selfService.errors.closeOffsetOutOfRange":
    case "selfService.errors.cutoffOutOfRange":
    case "selfService.errors.lateCancellationWindowOutOfRange":
      return t("validation.offsetRange");
    case "selfService.errors.openOffsetMustBeGreaterThanClose":
      return t("validation.openCloseOrder");
    case "selfService.errors.maxLateCancellationsOutOfRange":
      return t("validation.maxLateRange");
    case "selfService.errors.waitlistOfferOutOfRange":
      return t("validation.waitlistOfferRange");
    case "selfService.errors.cycleDurationOutOfRange":
      return t("validation.cycleDurationRange");
    default:
      return errorKey;
  }
}

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
      setValidationError(validation.errorKey ? getConfigValidationMessage(validation.errorKey, t) : null);
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
        <div className="flex flex-col gap-1">
          <h3 className="text-base font-semibold text-slate-900">{t("title")}</h3>
          <p className="text-sm text-slate-500">{t("description")}</p>
        </div>

        <div className="mt-5 grid gap-3 lg:grid-cols-4">
          <PolicySummaryCard
            label={t("summary.shiftLimits")}
            value={t("summary.shiftLimitsValue", {
              min: formValues.minShiftsPerCycle,
              max: formValues.maxShiftsPerCycle,
              days: formValues.cycleDurationDays,
            })}
          />
          <PolicySummaryCard
            label={t("summary.requestWindow")}
            value={t("summary.requestWindowValue", {
              open: formValues.requestWindowOpenOffsetHours,
              close: formValues.requestWindowCloseOffsetHours,
            })}
          />
          <PolicySummaryCard
            label={t("summary.absence")}
            value={t("summary.absenceValue", {
              cutoff: formValues.cancellationCutoffHours,
              lateWindow: formValues.lateCancellationWindowHours,
              max: formValues.maxLateCancellationsPerCycle,
            })}
          />
          <PolicySummaryCard
            label={t("summary.waitlist")}
            value={t("summary.waitlistValue", { minutes: formValues.waitlistOfferMinutes })}
          />
        </div>

        <div className="mt-6 space-y-4">
          {CONFIG_SECTIONS.map((section) => (
            <section key={section.key} className="rounded-xl border border-slate-200 bg-slate-50 p-4">
              <div className="mb-4">
                <h4 className="text-sm font-semibold text-slate-900">
                  {t(`sections.${section.key}.title`)}
                </h4>
                <p className="mt-1 text-xs leading-5 text-slate-500">
                  {t(`sections.${section.key}.description`)}
                </p>
              </div>

              <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                {section.fields.map((field) => (
                  <div key={field.key} className="rounded-lg border border-slate-200 bg-white p-3">
                    <div className="flex items-start justify-between gap-3">
                      <label
                        htmlFor={`config-${field.key}`}
                        className="text-sm font-medium text-slate-700"
                      >
                        {t(field.key)}
                      </label>
                      <span className="shrink-0 rounded-full border border-slate-200 bg-slate-50 px-2 py-0.5 text-xs font-medium text-slate-500">
                        {t(`units.${field.unit}`)}
                      </span>
                    </div>
                    <p className="mt-1 min-h-10 text-xs leading-5 text-slate-500">
                      {t(`descriptions.${field.key}`)}
                    </p>
                    <input
                      id={`config-${field.key}`}
                      type="number"
                      min={field.min}
                      max={field.max}
                      value={formValues[field.key]}
                      onChange={(e) => handleFieldChange(field.key, e.target.value)}
                      className="mt-3 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-transparent focus:outline-none focus:ring-2 focus:ring-sky-400"
                      dir="ltr"
                    />
                    <p className="mt-1 text-xs text-slate-400">
                      {t("range", { min: field.min, max: field.max })}
                    </p>
                  </div>
                ))}
              </div>
            </section>
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

function PolicySummaryCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-slate-200 bg-slate-50 px-3 py-2">
      <p className="text-xs font-medium text-slate-500">{label}</p>
      <p className="mt-1 text-sm font-semibold text-slate-900">{value}</p>
    </div>
  );
}
