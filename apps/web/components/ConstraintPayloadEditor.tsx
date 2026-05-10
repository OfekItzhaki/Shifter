"use client";

import { useTranslations } from "next-intl";

/**
 * Replaces the raw JSON textarea for constraint payloads with
 * friendly form fields based on the rule type.
 */

export interface TaskOption {
  id: string;
  name: string;
}

interface Props {
  ruleType: string;
  value: string; // JSON string
  onChange: (json: string) => void;
  /** Optional list of tasks for the no_task_type_restriction dropdown */
  taskOptions?: TaskOption[];
}

function parsePayload(json: string): Record<string, unknown> {
  try { return JSON.parse(json) ?? {}; } catch { return {}; }
}

function field(
  label: string, key: string,
  payload: Record<string, unknown>,
  onChange: (p: Record<string, unknown>) => void,
  type: "number" | "text" = "number"
) {
  return (
    <div key={key}>
      <label className="block text-xs font-medium text-slate-500 mb-1">{label}</label>
      <input
        type={type}
        value={String(payload[key] ?? "")}
        onChange={e => onChange({ ...payload, [key]: type === "number" ? Number(e.target.value) : e.target.value })}
        className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
      />
    </div>
  );
}

export default function ConstraintPayloadEditor({ ruleType, value, onChange, taskOptions }: Props) {
  const t = useTranslations("constraintEditor");
  const payload = parsePayload(value);
  const update = (p: Record<string, unknown>) => onChange(JSON.stringify(p));

  switch (ruleType) {
    case "min_rest_hours":
      return (
        <div>
          <label className="block text-xs font-medium text-slate-500 mb-1">{t("minRestHours")}</label>
          <div className="flex items-center gap-2">
            <input
              type="number" min={1} max={72}
              value={Number(payload.hours ?? 8)}
              onChange={e => update({ hours: Number(e.target.value) })}
              className="w-24 border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            <span className="text-sm text-slate-500">{t("hours")}</span>
          </div>
        </div>
      );

    case "max_kitchen_per_week":
      return (
        <div className="space-y-3">
          {field(t("maxKitchenPerWeek"), "max", payload, update)}
          {field(t("taskTypeName"), "task_type_name", payload, update, "text")}
        </div>
      );

    case "no_consecutive_burden":
      return (
        <div>
          <label className="block text-xs font-medium text-slate-500 mb-1">{t("noConsecutiveBurden")}</label>
          <select
            value={String(payload.burden_level ?? "hated")}
            onChange={e => update({ burden_level: e.target.value })}
            className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="hated">{t("hated")}</option>
            <option value="disliked">{t("disliked")}</option>
            <option value="neutral">{t("neutral")}</option>
          </select>
        </div>
      );

    case "min_base_headcount":
      return (
        <div className="space-y-3">
          {field(t("minPeople"), "min", payload, update)}
          {field(t("windowHours"), "window_hours", payload, update)}
        </div>
      );

    case "no_task_type_restriction":
      if (taskOptions && taskOptions.length > 0) {
        return (
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1">{t("restrictedTaskType")}</label>
            <select
              value={String(payload.task_type_id ?? "")}
              onChange={e => update({ task_type_id: e.target.value })}
              className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="">{t("selectTask")}</option>
              {taskOptions.map(opt => (
                <option key={opt.id} value={opt.id}>{opt.name}</option>
              ))}
            </select>
          </div>
        );
      }
      return (
        <div>
          {field(t("taskTypeId"), "task_type_id", payload, update, "text")}
        </div>
      );

    case "required_qualification_per_shift":
    case "preferred_qualification_per_shift":
      return (
        <div className="space-y-3">
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1">שם כישור</label>
            <input
              type="text"
              value={String(payload.qualification_name ?? "")}
              onChange={e => update({ ...payload, qualification_name: e.target.value })}
              placeholder="לדוגמה: מפקד כיתה, חובש"
              className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1">שם משימה (אופציונלי)</label>
            {taskOptions && taskOptions.length > 0 ? (
              <select
                value={String(payload.task_name ?? "")}
                onChange={e => update({ ...payload, task_name: e.target.value || undefined })}
                className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <option value="">כל המשימות</option>
                {taskOptions.map(opt => (
                  <option key={opt.id} value={opt.name}>{opt.name}</option>
                ))}
              </select>
            ) : (
              <input
                type="text"
                value={String(payload.task_name ?? "")}
                onChange={e => update({ ...payload, task_name: e.target.value || undefined })}
                placeholder="ריק = כל המשימות"
                className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            )}
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1">מספר מינימלי</label>
            <input
              type="number" min={1} max={10}
              value={Number(payload.min_count ?? 1)}
              onChange={e => update({ ...payload, min_count: Number(e.target.value) })}
              className="w-24 border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
        </div>
      );

    default:
      return (
        <div>
          <label className="block text-xs font-medium text-slate-500 mb-1">
            {t("parameters")} — {t("ruleType")}: <code className="bg-slate-100 px-1 rounded">{ruleType}</code>
          </label>
          <textarea
            value={value}
            onChange={e => onChange(e.target.value)}
            rows={3}
            className="w-full border border-slate-200 rounded-xl px-3 py-2 text-xs font-mono focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
          />
        </div>
      );
  }
}
