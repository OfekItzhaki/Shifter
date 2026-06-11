"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import {
  getSpaceSelfServiceDefaults,
  updateSpaceSelfServiceDefaults,
  type SpaceSelfServiceDefaultsDto,
  type UpdateSpaceSelfServiceDefaultsPayload,
} from "@/lib/api/spaces";

interface Props {
  spaceId: string;
  isOwner: boolean;
}

const numericFields: Array<{
  key: NumericField;
  min: number;
  max: number;
}> = [
  { key: "minShiftsPerCycle", min: 0, max: 100 },
  { key: "maxShiftsPerCycle", min: 1, max: 100 },
  { key: "requestWindowOpenOffsetHours", min: 1, max: 720 },
  { key: "requestWindowCloseOffsetHours", min: 1, max: 720 },
  { key: "cancellationCutoffHours", min: 1, max: 720 },
  { key: "maxAbsencesPerCycle", min: 0, max: 100 },
  { key: "maxLateCancellationsPerCycle", min: 0, max: 100 },
  { key: "lateCancellationWindowHours", min: 1, max: 720 },
  { key: "waitlistOfferMinutes", min: 15, max: 1440 },
  { key: "cycleDurationDays", min: 1, max: 30 },
];

const toggleFields: Array<keyof UpdateSpaceSelfServiceDefaultsPayload> = [
  "allowMemberShiftClaims",
  "allowWaitlist",
  "allowShiftChangeRequests",
  "allowAbsenceReports",
  "allowShiftSwaps",
];

type NumericField = {
  [K in keyof UpdateSpaceSelfServiceDefaultsPayload]:
    UpdateSpaceSelfServiceDefaultsPayload[K] extends number ? K : never;
}[keyof UpdateSpaceSelfServiceDefaultsPayload];

export default function SelfServiceDefaultsCard({ spaceId, isOwner }: Props) {
  const t = useTranslations("spaces.selfServiceDefaults");
  const common = useTranslations("spaces");
  const [defaults, setDefaults] = useState<SpaceSelfServiceDefaultsDto | null>(null);
  const [form, setForm] = useState<UpdateSpaceSelfServiceDefaultsPayload | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    let mounted = true;
    getSpaceSelfServiceDefaults(spaceId)
      .then((data) => {
        if (!mounted) return;
        setDefaults(data);
        setForm(toPayload(data));
      })
      .catch(() => {
        if (mounted) setError(t("loadError"));
      })
      .finally(() => {
        if (mounted) setLoading(false);
      });

    return () => {
      mounted = false;
    };
  }, [spaceId, t]);

  if (!isOwner) return null;

  const updateNumber = (key: NumericField, value: string) => {
    const parsed = Number(value);
    setSaved(false);
    setForm((prev) => prev ? { ...prev, [key]: Number.isNaN(parsed) ? 0 : parsed } : prev);
  };

  const updateToggle = (key: keyof UpdateSpaceSelfServiceDefaultsPayload, value: boolean) => {
    setSaved(false);
    setForm((prev) => prev ? { ...prev, [key]: value } : prev);
  };

  const validate = () => {
    if (!form) return t("loadError");
    for (const field of numericFields) {
      const value = Number(form[field.key]);
      if (!Number.isInteger(value) || value < field.min || value > field.max) {
        return t("validationError");
      }
    }
    if (form.minShiftsPerCycle > form.maxShiftsPerCycle) return t("minMaxError");
    if (form.requestWindowOpenOffsetHours <= form.requestWindowCloseOffsetHours) {
      return t("windowError");
    }
    return null;
  };

  const handleSave = async () => {
    const validation = validate();
    if (validation || !form) {
      setError(validation);
      return;
    }

    setSaving(true);
    setError(null);
    setSaved(false);
    try {
      const updated = await updateSpaceSelfServiceDefaults(spaceId, form);
      setDefaults(updated);
      setForm(toPayload(updated));
      setSaved(true);
    } catch {
      setError(t("saveError"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-2xl p-5 shadow-sm">
      <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-sm font-semibold text-slate-900 dark:text-white">
            {t("title")}
          </h2>
          <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
            {t("description")}
          </p>
        </div>
        {defaults && (
          <span className="rounded-full border border-slate-200 px-2.5 py-1 text-xs text-slate-500 dark:border-slate-600 dark:text-slate-300">
            {t(`source.${defaults.source}`)}
          </span>
        )}
      </div>

      {loading && (
        <p className="text-sm text-slate-500 dark:text-slate-400">{t("loading")}</p>
      )}

      {form && (
        <div className="space-y-5">
          <div className="grid gap-3 sm:grid-cols-2">
            {numericFields.map((field) => (
              <label key={field.key} className="block">
                <span className="mb-1 block text-xs font-medium text-slate-600 dark:text-slate-300">
                  {t(`fields.${field.key}`)}
                </span>
                <input
                  type="number"
                  min={field.min}
                  max={field.max}
                  step={1}
                  value={Number(form[field.key])}
                  onChange={(e) => updateNumber(field.key, e.target.value)}
                  className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 focus:border-sky-500 focus:outline-none dark:border-slate-600 dark:bg-slate-700 dark:text-white"
                />
              </label>
            ))}
          </div>

          <div className="grid gap-2 sm:grid-cols-2">
            {toggleFields.map((field) => (
              <label
                key={field}
                className="flex min-h-[44px] items-center justify-between gap-3 rounded-lg border border-slate-200 px-3 py-2 dark:border-slate-700"
              >
                <span className="text-sm text-slate-700 dark:text-slate-200">
                  {t(`fields.${field}`)}
                </span>
                <input
                  type="checkbox"
                  checked={Boolean(form[field])}
                  onChange={(e) => updateToggle(field, e.target.checked)}
                  className="h-4 w-4 accent-sky-500"
                />
              </label>
            ))}
          </div>

          <button
            type="button"
            onClick={handleSave}
            disabled={saving}
            className="rounded-lg bg-sky-500 px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-sky-600 disabled:bg-slate-300 dark:disabled:bg-slate-600"
          >
            {saving ? common("saving") : common("save")}
          </button>
        </div>
      )}

      {error && (
        <p className="mt-3 text-xs text-red-500 dark:text-red-400" role="alert">
          {error}
        </p>
      )}

      {saved && (
        <p className="mt-3 text-xs text-green-600 dark:text-green-400" role="status">
          {t("saved")}
        </p>
      )}
    </div>
  );
}

function toPayload(data: SpaceSelfServiceDefaultsDto): UpdateSpaceSelfServiceDefaultsPayload {
  return {
    minShiftsPerCycle: data.minShiftsPerCycle,
    maxShiftsPerCycle: data.maxShiftsPerCycle,
    requestWindowOpenOffsetHours: data.requestWindowOpenOffsetHours,
    requestWindowCloseOffsetHours: data.requestWindowCloseOffsetHours,
    cancellationCutoffHours: data.cancellationCutoffHours,
    maxAbsencesPerCycle: data.maxAbsencesPerCycle,
    maxLateCancellationsPerCycle: data.maxLateCancellationsPerCycle,
    lateCancellationWindowHours: data.lateCancellationWindowHours,
    waitlistOfferMinutes: data.waitlistOfferMinutes,
    cycleDurationDays: data.cycleDurationDays,
    allowMemberShiftClaims: data.allowMemberShiftClaims,
    allowWaitlist: data.allowWaitlist,
    allowShiftChangeRequests: data.allowShiftChangeRequests,
    allowAbsenceReports: data.allowAbsenceReports,
    allowShiftSwaps: data.allowShiftSwaps,
  };
}
