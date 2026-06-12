"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations, useLocale } from "next-intl";
import {
  createSpaceSpecialDay,
  deleteSpaceSpecialDay,
  HolidayCalendarDayDto,
  importHolidayCalendar,
  listSpaceSpecialDays,
  previewHolidayCalendar,
  SpaceSpecialDayDto,
  SpaceSpecialDayKind,
  updateSpaceSpecialDay,
} from "@/lib/api/spaceSpecialDays";

interface SpecialDaysCardProps {
  spaceId: string;
  isOwner: boolean;
}

interface FormState {
  date: string;
  name: string;
  kind: SpaceSpecialDayKind;
  homeLeaveWeightMultiplier: string;
  requiresCoverage: boolean;
}

function createDefaultForm(): FormState {
  return {
    date: new Date().toISOString().split("T")[0],
    name: "",
    kind: "Holiday",
    homeLeaveWeightMultiplier: "1.5",
    requiresCoverage: true,
  };
}

const KIND_OPTIONS: SpaceSpecialDayKind[] = ["Holiday", "Weekend", "Custom"];

export default function SpecialDaysCard({ spaceId, isOwner }: SpecialDaysCardProps) {
  const t = useTranslations("spaces.specialDays");
  const locale = useLocale();

  const [days, setDays] = useState<SpaceSpecialDayDto[]>([]);
  const [form, setForm] = useState<FormState>(() => createDefaultForm());
  const [editingId, setEditingId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [calendarYear, setCalendarYear] = useState(() => String(new Date().getFullYear()));
  const [calendarPreview, setCalendarPreview] = useState<HolidayCalendarDayDto[]>([]);
  const [calendarLoading, setCalendarLoading] = useState(false);
  const [calendarImporting, setCalendarImporting] = useState(false);
  const [calendarMessage, setCalendarMessage] = useState<string | null>(null);

  const loadDays = useCallback(async () => {
    if (!spaceId) return;
    setLoading(true);
    setError(null);
    try {
      const from = new Date();
      from.setMonth(from.getMonth() - 1);
      const to = new Date();
      to.setFullYear(to.getFullYear() + 1);
      const data = await listSpaceSpecialDays(
        spaceId,
        from.toISOString().split("T")[0],
        to.toISOString().split("T")[0]
      );
      setDays(data);
    } catch {
      setError(t("loadError"));
    } finally {
      setLoading(false);
    }
  }, [spaceId, t]);

  useEffect(() => {
    if (isOwner) {
      queueMicrotask(() => void loadDays());
    }
  }, [isOwner, loadDays]);

  if (!isOwner) return null;

  function resetForm() {
    setForm(createDefaultForm());
    setEditingId(null);
    setSaved(false);
    setError(null);
  }

  function startEdit(day: SpaceSpecialDayDto) {
    setForm({
      date: day.date,
      name: day.name,
      kind: day.kind,
      homeLeaveWeightMultiplier: String(day.homeLeaveWeightMultiplier),
      requiresCoverage: day.requiresCoverage,
    });
    setEditingId(day.id);
    setSaved(false);
    setError(null);
  }

  async function handleSave() {
    const multiplier = Number(form.homeLeaveWeightMultiplier);
    if (!form.date || form.name.trim().length < 2 || Number.isNaN(multiplier) || multiplier < 1 || multiplier > 5) {
      setError(t("validationError"));
      return;
    }

    setSaving(true);
    setError(null);
    setSaved(false);
    try {
      const payload = {
        date: form.date,
        name: form.name.trim(),
        kind: form.kind,
        homeLeaveWeightMultiplier: multiplier,
        requiresCoverage: form.requiresCoverage,
      };

      if (editingId) {
        await updateSpaceSpecialDay(spaceId, editingId, payload);
      } else {
        await createSpaceSpecialDay(spaceId, payload);
      }

      setSaved(true);
      resetForm();
      await loadDays();
    } catch {
      setError(t("saveError"));
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(dayId: string) {
    setDeletingId(dayId);
    setError(null);
    try {
      await deleteSpaceSpecialDay(spaceId, dayId);
      if (editingId === dayId) resetForm();
      await loadDays();
    } catch {
      setError(t("deleteError"));
    } finally {
      setDeletingId(null);
    }
  }

  async function handlePreviewCalendar() {
    const year = Number(calendarYear);
    if (!Number.isInteger(year) || year < 2000 || year > 2100) {
      setError(t("calendar.yearError"));
      return;
    }

    setCalendarLoading(true);
    setError(null);
    setCalendarMessage(null);
    try {
      const preview = await previewHolidayCalendar(spaceId, "IL", year);
      setCalendarPreview(preview);
      setCalendarMessage(t("calendar.previewLoaded", {
        count: preview.length,
        newCount: preview.filter(day => !day.alreadyExists).length,
      }));
    } catch {
      setError(t("calendar.previewError"));
    } finally {
      setCalendarLoading(false);
    }
  }

  async function handleImportCalendar() {
    const year = Number(calendarYear);
    if (!Number.isInteger(year) || year < 2000 || year > 2100) {
      setError(t("calendar.yearError"));
      return;
    }

    setCalendarImporting(true);
    setError(null);
    setCalendarMessage(null);
    try {
      const result = await importHolidayCalendar(spaceId, "IL", year);
      setCalendarPreview([]);
      setCalendarMessage(t("calendar.imported", {
        imported: result.imported.length,
        skipped: result.skipped.length,
      }));
      await loadDays();
    } catch {
      setError(t("calendar.importError"));
    } finally {
      setCalendarImporting(false);
    }
  }

  function formatDate(value: string) {
    const date = new Date(`${value}T12:00:00Z`);
    return new Intl.DateTimeFormat(locale, {
      year: "numeric",
      month: "short",
      day: "numeric",
    }).format(date);
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
        <div className="rounded-xl border border-sky-100 bg-sky-50/70 p-3 dark:border-sky-900/60 dark:bg-sky-950/20">
          <div className="mb-3">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-sky-800 dark:text-sky-200">
              {t("calendar.title")}
            </h3>
            <p className="mt-1 text-xs text-sky-700 dark:text-sky-300">
              {t("calendar.description")}
            </p>
          </div>
          <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
            <div className="sm:w-36">
              <label className="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-1">
                {t("calendar.yearLabel")}
              </label>
              <input
                type="number"
                min={2000}
                max={2100}
                value={calendarYear}
                onChange={(e) => { setCalendarYear(e.target.value); setCalendarPreview([]); setCalendarMessage(null); }}
                disabled={calendarLoading || calendarImporting}
                className="w-full px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white text-sm disabled:opacity-50 focus:outline-none focus:border-sky-500"
              />
            </div>
            <button
              type="button"
              onClick={handlePreviewCalendar}
              disabled={calendarLoading || calendarImporting}
              className="px-4 py-2 rounded-lg bg-white hover:bg-slate-50 dark:bg-slate-800 dark:hover:bg-slate-700 border border-sky-200 dark:border-sky-800 text-sky-700 dark:text-sky-200 font-semibold text-sm transition-colors disabled:opacity-50"
            >
              {calendarLoading ? t("calendar.previewing") : t("calendar.preview")}
            </button>
            <button
              type="button"
              onClick={handleImportCalendar}
              disabled={calendarLoading || calendarImporting}
              className="px-4 py-2 rounded-lg bg-sky-500 hover:bg-sky-600 text-white font-semibold text-sm transition-colors disabled:opacity-50"
            >
              {calendarImporting ? t("calendar.importing") : t("calendar.import")}
            </button>
          </div>
          {calendarMessage && (
            <p className="mt-2 text-xs text-sky-700 dark:text-sky-300">{calendarMessage}</p>
          )}
          {calendarPreview.length > 0 && (
            <div className="mt-3 flex flex-wrap gap-2">
              {calendarPreview.slice(0, 6).map(day => (
                <span
                  key={`${day.date}-${day.name}`}
                  className="rounded-full bg-white px-2.5 py-1 text-xs font-medium text-slate-700 ring-1 ring-slate-200 dark:bg-slate-900 dark:text-slate-200 dark:ring-slate-700"
                >
                  {formatDate(day.date)} · {day.name}
                  {day.alreadyExists ? ` · ${t("calendar.exists")}` : ""}
                </span>
              ))}
              {calendarPreview.length > 6 && (
                <span className="rounded-full px-2.5 py-1 text-xs font-medium text-slate-500 dark:text-slate-400">
                  {t("calendar.more", { count: calendarPreview.length - 6 })}
                </span>
              )}
            </div>
          )}
        </div>

        <div className="grid gap-3 sm:grid-cols-[1fr_1fr]">
          <div>
            <label className="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-1">
              {t("dateLabel")}
            </label>
            <input
              type="date"
              value={form.date}
              onChange={(e) => { setForm(prev => ({ ...prev, date: e.target.value })); setSaved(false); }}
              disabled={saving}
              className="w-full px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white text-sm disabled:opacity-50 focus:outline-none focus:border-sky-500"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-1">
              {t("nameLabel")}
            </label>
            <input
              type="text"
              value={form.name}
              maxLength={120}
              placeholder={t("namePlaceholder")}
              onChange={(e) => { setForm(prev => ({ ...prev, name: e.target.value })); setSaved(false); }}
              disabled={saving}
              className="w-full px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white text-sm disabled:opacity-50 focus:outline-none focus:border-sky-500"
            />
          </div>
        </div>

        <div className="grid gap-3 sm:grid-cols-[1fr_1fr]">
          <div>
            <label className="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-1">
              {t("kindLabel")}
            </label>
            <select
              value={form.kind}
              onChange={(e) => { setForm(prev => ({ ...prev, kind: e.target.value as SpaceSpecialDayKind })); setSaved(false); }}
              disabled={saving}
              className="w-full px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white text-sm disabled:opacity-50 focus:outline-none focus:border-sky-500"
            >
              {KIND_OPTIONS.map(kind => (
                <option key={kind} value={kind}>
                  {t(`kind.${kind}`)}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-600 dark:text-slate-300 mb-1">
              {t("weightLabel")}
            </label>
            <input
              type="number"
              min={1}
              max={5}
              step={0.25}
              value={form.homeLeaveWeightMultiplier}
              onChange={(e) => { setForm(prev => ({ ...prev, homeLeaveWeightMultiplier: e.target.value })); setSaved(false); }}
              disabled={saving}
              className="w-full px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 text-slate-900 dark:text-white text-sm disabled:opacity-50 focus:outline-none focus:border-sky-500"
            />
          </div>
        </div>

        <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-200">
          <input
            type="checkbox"
            checked={form.requiresCoverage}
            onChange={(e) => { setForm(prev => ({ ...prev, requiresCoverage: e.target.checked })); setSaved(false); }}
            disabled={saving}
            className="h-4 w-4 rounded border-slate-300 dark:border-slate-600 text-sky-500 focus:ring-sky-500"
          />
          {t("requiresCoverage")}
        </label>

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={handleSave}
            disabled={saving}
            className="px-4 py-2 rounded-lg bg-sky-500 hover:bg-sky-600 disabled:bg-slate-300 dark:disabled:bg-slate-600 text-white font-semibold text-sm transition-colors"
          >
            {saving ? t("saving") : editingId ? t("update") : t("add")}
          </button>
          {editingId && (
            <button
              type="button"
              onClick={resetForm}
              disabled={saving}
              className="px-4 py-2 rounded-lg bg-slate-100 hover:bg-slate-200 dark:bg-slate-700 dark:hover:bg-slate-600 text-slate-700 dark:text-slate-200 font-semibold text-sm transition-colors disabled:opacity-50"
            >
              {t("cancel")}
            </button>
          )}
          {saved && <span className="text-sm text-emerald-600 dark:text-emerald-400">{t("saved")}</span>}
          {error && <span className="text-sm text-red-600 dark:text-red-400">{error}</span>}
        </div>

        <div className="border-t border-slate-100 dark:border-slate-700 pt-4">
          {loading ? (
            <p className="text-sm text-slate-500 dark:text-slate-400">{t("loading")}</p>
          ) : days.length === 0 ? (
            <p className="text-sm text-slate-500 dark:text-slate-400">{t("empty")}</p>
          ) : (
            <div className="space-y-2">
              {days.map(day => (
                <div
                  key={day.id}
                  className="flex flex-col gap-3 rounded-xl border border-slate-100 dark:border-slate-700 bg-slate-50/70 dark:bg-slate-900/30 p-3 sm:flex-row sm:items-center"
                >
                  <div className="flex-1 min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-sm font-semibold text-slate-900 dark:text-white">{day.name}</span>
                      <span className="rounded-full bg-sky-50 dark:bg-sky-950/40 px-2 py-0.5 text-xs font-medium text-sky-700 dark:text-sky-300">
                        {t(`kind.${day.kind}`)}
                      </span>
                    </div>
                    <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
                      {formatDate(day.date)} · {t("weightValue", { value: day.homeLeaveWeightMultiplier })}
                      {day.requiresCoverage ? ` · ${t("coverageOn")}` : ""}
                    </p>
                  </div>
                  <div className="flex gap-2">
                    <button
                      type="button"
                      onClick={() => startEdit(day)}
                      className="px-3 py-1.5 rounded-lg bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-600 text-xs font-semibold text-slate-700 dark:text-slate-200 hover:border-sky-300 transition-colors"
                    >
                      {t("edit")}
                    </button>
                    <button
                      type="button"
                      onClick={() => handleDelete(day.id)}
                      disabled={deletingId === day.id}
                      className="px-3 py-1.5 rounded-lg bg-red-50 dark:bg-red-950/30 text-xs font-semibold text-red-600 dark:text-red-300 hover:bg-red-100 dark:hover:bg-red-950/50 transition-colors disabled:opacity-50"
                    >
                      {deletingId === day.id ? t("deleting") : t("delete")}
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
