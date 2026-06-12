"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import {
  getOrganizationSelfServiceDefaults,
  searchPlatformOrganizations,
  updateOrganizationSelfServiceDefaults,
  type OrganizationCandidateDto,
} from "@/lib/api/platform";
import type {
  SpaceSelfServiceDefaultsDto,
  UpdateSpaceSelfServiceDefaultsPayload,
} from "@/lib/api/spaces";

const numericFields: Array<{ key: NumericField; min: number; max: number }> = [
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

export default function OrganizationSelfServiceDefaultsPanel() {
  const t = useTranslations("platform.organizationDefaults");
  const defaultsT = useTranslations("spaces.selfServiceDefaults");
  const platformT = useTranslations("platform");

  const [search, setSearch] = useState("");
  const [organizations, setOrganizations] = useState<OrganizationCandidateDto[]>([]);
  const [selectedOrganizationId, setSelectedOrganizationId] = useState("");
  const [defaults, setDefaults] = useState<SpaceSelfServiceDefaultsDto | null>(null);
  const [form, setForm] = useState<UpdateSpaceSelfServiceDefaultsPayload | null>(null);
  const [loadingOrganizations, setLoadingOrganizations] = useState(true);
  const [loadingDefaults, setLoadingDefaults] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const loadOrganizations = useCallback(async (nextSearch: string) => {
    setLoadingOrganizations(true);
    setError(null);
    try {
      const results = await searchPlatformOrganizations(nextSearch);
      setOrganizations(results);
      setSelectedOrganizationId((current) => {
        if (results.some((organization) => organization.id === current)) {
          return current;
        }
        return results[0]?.id || "";
      });
      if (results.length === 0) {
        setDefaults(null);
        setForm(null);
      }
    } catch {
      setError(t("loadOrganizationsError"));
    } finally {
      setLoadingOrganizations(false);
    }
  }, [t]);

  useEffect(() => {
    void Promise.resolve().then(() => loadOrganizations(""));
  }, [loadOrganizations]);

  useEffect(() => {
    if (!selectedOrganizationId) return;
    let mounted = true;
    void Promise.resolve()
      .then(() => {
        if (!mounted) return null;
        setLoadingDefaults(true);
        setError(null);
        return getOrganizationSelfServiceDefaults(selectedOrganizationId);
      })
      .then((data) => {
        if (!mounted || !data) return;
        setDefaults(data);
        setForm(toPayload(data));
      })
      .catch(() => {
        if (mounted) setError(defaultsT("loadError"));
      })
      .finally(() => {
        if (mounted) setLoadingDefaults(false);
      });

    return () => {
      mounted = false;
    };
  }, [selectedOrganizationId, defaultsT]);

  function updateNumber(key: NumericField, value: string) {
    const parsed = Number(value);
    setSaved(false);
    setForm((prev) => prev ? { ...prev, [key]: Number.isNaN(parsed) ? 0 : parsed } : prev);
  }

  function updateToggle(key: keyof UpdateSpaceSelfServiceDefaultsPayload, value: boolean) {
    setSaved(false);
    setForm((prev) => prev ? { ...prev, [key]: value } : prev);
  }

  function validate(): string | null {
    if (!form) return defaultsT("loadError");
    for (const field of numericFields) {
      const value = Number(form[field.key]);
      if (!Number.isInteger(value) || value < field.min || value > field.max) {
        return defaultsT("validationError");
      }
    }
    if (form.minShiftsPerCycle > form.maxShiftsPerCycle) return defaultsT("minMaxError");
    if (form.requestWindowOpenOffsetHours <= form.requestWindowCloseOffsetHours) {
      return defaultsT("windowError");
    }
    return null;
  }

  async function handleSave() {
    const validation = validate();
    if (validation || !form || !selectedOrganizationId) {
      setError(validation);
      return;
    }

    setSaving(true);
    setSaved(false);
    setError(null);
    try {
      const updated = await updateOrganizationSelfServiceDefaults(selectedOrganizationId, form);
      setDefaults(updated);
      setForm(toPayload(updated));
      setSaved(true);
    } catch {
      setError(defaultsT("saveError"));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-700 dark:bg-slate-800">
      <div className="mb-4 flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <h2 className="text-sm font-bold text-slate-900 dark:text-white">{t("title")}</h2>
          <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">{t("description")}</p>
        </div>
        {defaults && (
          <span className="w-fit rounded-full border border-slate-200 px-2.5 py-1 text-xs text-slate-500 dark:border-slate-600 dark:text-slate-300">
            {defaultsT(`source.${defaults.source}`)}
          </span>
        )}
      </div>

      <div className="mb-4 grid gap-2 lg:grid-cols-[minmax(0,1fr)_auto]">
        <label className="block">
          <span className="mb-1 block text-xs font-semibold text-slate-600 dark:text-slate-300">
            {t("searchLabel")}
          </span>
          <input
            type="search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === "Enter") {
                event.preventDefault();
                void loadOrganizations(search);
              }
            }}
            className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 focus:border-sky-500 focus:outline-none dark:border-slate-600 dark:bg-slate-700 dark:text-white"
            placeholder={t("searchPlaceholder")}
          />
        </label>
        <button
          type="button"
          onClick={() => loadOrganizations(search)}
          disabled={loadingOrganizations}
          className="self-end rounded-lg border border-slate-200 bg-white px-4 py-2 text-sm font-semibold text-sky-700 transition-colors hover:border-sky-200 hover:bg-sky-50 disabled:opacity-50 dark:border-slate-600 dark:bg-slate-700 dark:text-sky-300"
        >
          {loadingOrganizations ? platformT("loading") : t("search")}
        </button>
      </div>

      <label className="mb-4 block">
        <span className="mb-1 block text-xs font-semibold text-slate-600 dark:text-slate-300">
          {t("organizationLabel")}
        </span>
        <select
          value={selectedOrganizationId}
          onChange={(event) => {
            setSelectedOrganizationId(event.target.value);
            setSaved(false);
          }}
          disabled={loadingOrganizations || organizations.length === 0}
          className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 focus:border-sky-500 focus:outline-none disabled:opacity-60 dark:border-slate-600 dark:bg-slate-700 dark:text-white"
        >
          {organizations.length === 0 ? (
            <option value="">{loadingOrganizations ? platformT("loading") : t("empty")}</option>
          ) : organizations.map((organization) => (
            <option key={organization.id} value={organization.id}>
              {organization.displayName} ({organization.spaceCount} / {organization.groupCount} / {organization.memberCount})
            </option>
          ))}
        </select>
      </label>

      {loadingDefaults && (
        <p className="text-sm text-slate-500 dark:text-slate-400">{defaultsT("loading")}</p>
      )}

      {form && !loadingDefaults && (
        <div className="space-y-5">
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            {numericFields.map((field) => (
              <label key={field.key} className="block">
                <span className="mb-1 block text-xs font-medium text-slate-600 dark:text-slate-300">
                  {defaultsT(`fields.${field.key}`)}
                </span>
                <input
                  type="number"
                  min={field.min}
                  max={field.max}
                  step={1}
                  value={Number(form[field.key])}
                  onChange={(event) => updateNumber(field.key, event.target.value)}
                  className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 focus:border-sky-500 focus:outline-none dark:border-slate-600 dark:bg-slate-700 dark:text-white"
                />
              </label>
            ))}
          </div>

          <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-5">
            {toggleFields.map((field) => (
              <label
                key={field}
                className="flex min-h-[44px] items-center justify-between gap-3 rounded-lg border border-slate-200 px-3 py-2 dark:border-slate-700"
              >
                <span className="text-sm text-slate-700 dark:text-slate-200">
                  {defaultsT(`fields.${field}`)}
                </span>
                <input
                  type="checkbox"
                  checked={Boolean(form[field])}
                  onChange={(event) => updateToggle(field, event.target.checked)}
                  className="h-4 w-4 accent-sky-500"
                />
              </label>
            ))}
          </div>

          <button
            type="button"
            onClick={handleSave}
            disabled={saving || !selectedOrganizationId}
            className="rounded-lg bg-sky-500 px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-sky-600 disabled:bg-slate-300 dark:disabled:bg-slate-600"
          >
            {saving ? platformT("loading") : platformT("saveSettings")}
          </button>
        </div>
      )}

      {error && <p className="mt-3 text-xs text-red-500 dark:text-red-400" role="alert">{error}</p>}
      {saved && <p className="mt-3 text-xs text-green-600 dark:text-green-400" role="status">{defaultsT("saved")}</p>}
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
