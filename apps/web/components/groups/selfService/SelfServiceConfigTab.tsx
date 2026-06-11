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
type NumericConfigKey = Exclude<
  keyof UpdateSelfServiceConfigPayload,
  | "allowMemberShiftClaims"
  | "allowWaitlist"
  | "allowShiftChangeRequests"
  | "allowAbsenceReports"
  | "allowShiftSwaps"
>;

interface ConfigField {
  key: NumericConfigKey;
  min: number;
  max: number;
  unit: "shifts" | "hours" | "minutes" | "days" | "reports";
  recommended: string;
}

interface ConfigSection {
  key: "shiftLimits" | "requestWindow" | "changesAbsence" | "waitlist";
  fields: ConfigField[];
}

type WorkflowToggleKey =
  | "allowMemberShiftClaims"
  | "allowWaitlist"
  | "allowShiftChangeRequests"
  | "allowAbsenceReports"
  | "allowShiftSwaps";

const WORKFLOW_TOGGLES: WorkflowToggleKey[] = [
  "allowMemberShiftClaims",
  "allowWaitlist",
  "allowShiftChangeRequests",
  "allowAbsenceReports",
  "allowShiftSwaps",
];

const CONFIG_SECTIONS: ConfigSection[] = [
  {
    key: "shiftLimits",
    fields: [
      { key: "minShiftsPerCycle", min: 0, max: 100, unit: "shifts", recommended: "1-2" },
      { key: "maxShiftsPerCycle", min: 1, max: 100, unit: "shifts", recommended: "team-based" },
      { key: "cycleDurationDays", min: 1, max: 30, unit: "days", recommended: "7" },
    ],
  },
  {
    key: "requestWindow",
    fields: [
      { key: "requestWindowOpenOffsetHours", min: 1, max: 720, unit: "hours", recommended: "72" },
      { key: "requestWindowCloseOffsetHours", min: 1, max: 720, unit: "hours", recommended: "12-24" },
    ],
  },
  {
    key: "changesAbsence",
    fields: [
      { key: "cancellationCutoffHours", min: 1, max: 720, unit: "hours", recommended: "24" },
      { key: "lateCancellationWindowHours", min: 1, max: 720, unit: "hours", recommended: "24" },
      { key: "maxAbsencesPerCycle", min: 0, max: 100, unit: "reports", recommended: "2-3" },
      { key: "maxLateCancellationsPerCycle", min: 0, max: 100, unit: "reports", recommended: "1-2" },
    ],
  },
  {
    key: "waitlist",
    fields: [
      { key: "waitlistOfferMinutes", min: 15, max: 1440, unit: "minutes", recommended: "30-60" },
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
    case "selfService.errors.maxAbsencesOutOfRange":
      return t("validation.maxLateRange");
    case "selfService.errors.waitlistOfferOutOfRange":
      return t("validation.waitlistOfferRange");
    case "selfService.errors.cycleDurationOutOfRange":
      return t("validation.cycleDurationRange");
    default:
      return errorKey;
  }
}

function getPolicyInsightKeys(values: UpdateSelfServiceConfigPayload): string[] {
  const insights: string[] = [];

  if (values.minShiftsPerCycle === 0) {
    insights.push("insights.noMinimum");
  }

  if (values.maxLateCancellationsPerCycle === 0) {
    insights.push("insights.noLateReports");
  } else if (values.maxLateCancellationsPerCycle <= 1) {
    insights.push("insights.strictLateReports");
  }

  if (values.requestWindowCloseOffsetHours > values.cancellationCutoffHours) {
    insights.push("insights.requestClosesBeforeCancellation");
  }

  if (values.lateCancellationWindowHours > values.cancellationCutoffHours) {
    insights.push("insights.lateWindowLongerThanCancel");
  }

  if (values.waitlistOfferMinutes < 30) {
    insights.push("insights.shortWaitlistOffer");
  }

  if (WORKFLOW_TOGGLES.some((key) => !values[key])) {
    insights.push("insights.workflowDisabled");
  }

  if (insights.length === 0) {
    insights.push("insights.balanced");
  }

  return insights;
}

function toFormValues(config: SelfServiceConfigDto): UpdateSelfServiceConfigPayload {
  return {
    minShiftsPerCycle: config.minShiftsPerCycle,
    maxShiftsPerCycle: config.maxShiftsPerCycle,
    requestWindowOpenOffsetHours: config.requestWindowOpenOffsetHours,
    requestWindowCloseOffsetHours: config.requestWindowCloseOffsetHours,
    cancellationCutoffHours: config.cancellationCutoffHours,
    maxAbsencesPerCycle: config.maxAbsencesPerCycle ?? 3,
    maxLateCancellationsPerCycle: config.maxLateCancellationsPerCycle,
    lateCancellationWindowHours: config.lateCancellationWindowHours,
    waitlistOfferMinutes: config.waitlistOfferMinutes,
    cycleDurationDays: config.cycleDurationDays,
    allowMemberShiftClaims: config.allowMemberShiftClaims ?? true,
    allowWaitlist: config.allowWaitlist ?? true,
    allowShiftChangeRequests: config.allowShiftChangeRequests ?? true,
    allowAbsenceReports: config.allowAbsenceReports ?? true,
    allowShiftSwaps: config.allowShiftSwaps ?? true,
  };
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
    maxAbsencesPerCycle: 3,
    maxLateCancellationsPerCycle: 2,
    lateCancellationWindowHours: 24,
    waitlistOfferMinutes: 60,
    cycleDurationDays: 7,
    allowMemberShiftClaims: true,
    allowWaitlist: true,
    allowShiftChangeRequests: true,
    allowAbsenceReports: true,
    allowShiftSwaps: true,
  });

  // Submission state
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);

  const fetchConfig = useCallback(async (showLoading = true) => {
    try {
      if (showLoading) {
        setLoading(true);
      }
      setError(null);
      const config = await getSelfServiceConfig(spaceId, groupId);
      setFormValues(toFormValues(config));
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setError(message);
    } finally {
      if (showLoading) {
        setLoading(false);
      }
    }
  }, [spaceId, groupId]);

  useEffect(() => {
    void Promise.resolve().then(() => fetchConfig());
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

  function handleToggleChange(key: WorkflowToggleKey) {
    setFormValues((prev) => ({ ...prev, [key]: !prev[key] }));
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
      const savedConfig = await updateSelfServiceConfig(spaceId, groupId, formValues);
      setFormValues(toFormValues(savedConfig));
      setSaveSuccess(true);
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setSaveError(message);
      await fetchConfig(false);
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
    return <ErrorRetry message={error} onRetry={() => fetchConfig()} />;
  }

  const policyInsightKeys = getPolicyInsightKeys(formValues);

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
              max: formValues.maxAbsencesPerCycle,
            })}
          />
          <PolicySummaryCard
            label={t("summary.waitlist")}
            value={t("summary.waitlistValue", { minutes: formValues.waitlistOfferMinutes })}
          />
        </div>

        <div className="mt-4 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3">
          <p className="text-xs font-semibold uppercase tracking-wide text-amber-800">
            {t("insights.title")}
          </p>
          <ul className="mt-2 space-y-1.5">
            {policyInsightKeys.map((key) => (
              <li key={key} className="text-sm leading-5 text-amber-900">
                {t(key)}
              </li>
            ))}
          </ul>
        </div>

        <div className="mt-6 space-y-4">
          <section className="rounded-xl border border-slate-200 bg-slate-50 p-4">
            <div className="mb-4">
              <h4 className="text-sm font-semibold text-slate-900">
                {t("sections.workflowAccess.title")}
              </h4>
              <p className="mt-1 text-xs leading-5 text-slate-500">
                {t("sections.workflowAccess.description")}
              </p>
            </div>

            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
              {WORKFLOW_TOGGLES.map((key) => (
                <button
                  key={key}
                  type="button"
                  onClick={() => handleToggleChange(key)}
                  className="flex min-h-24 items-start justify-between gap-4 rounded-lg border border-slate-200 bg-white p-3 text-start transition hover:border-sky-200 hover:bg-sky-50/60"
                  aria-pressed={formValues[key]}
                >
                  <span>
                    <span className="block text-sm font-semibold text-slate-800">
                      {t(`workflow.${key}.title`)}
                    </span>
                    <span className="mt-1 block text-xs leading-5 text-slate-500">
                      {t(`workflow.${key}.description`)}
                    </span>
                  </span>
                  <span
                    className={`mt-0.5 inline-flex h-6 w-11 shrink-0 items-center rounded-full border px-0.5 transition ${
                      formValues[key]
                        ? "border-sky-500 bg-sky-500"
                        : "border-slate-300 bg-slate-200"
                    }`}
                    aria-hidden="true"
                  >
                    <span
                      className={`h-5 w-5 rounded-full bg-white shadow-sm transition ${
                        formValues[key] ? "translate-x-5" : "translate-x-0"
                      }`}
                    />
                  </span>
                </button>
              ))}
            </div>
          </section>

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
                    <div className="mt-2 inline-flex rounded-full border border-sky-200 bg-sky-50 px-2.5 py-1 text-xs font-medium text-sky-700">
                      {t("recommended", {
                        value: t(`recommendations.${field.key}`, { value: field.recommended }),
                      })}
                    </div>
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
