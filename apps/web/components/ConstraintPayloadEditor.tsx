"use client";

/**
 * Replaces the raw JSON textarea for constraint payloads with
 * friendly form fields based on the rule type.
 */

interface Props {
  ruleType: string;
  value: string; // JSON string
  onChange: (json: string) => void;
}

function parsePayload(json: string): Record<string, unknown> {
  try { return JSON.parse(json) ?? {}; } catch { return {}; }
}

function field(label: string, key: string, payload: Record<string, unknown>, onChange: (p: Record<string, unknown>) => void, type: "number" | "text" = "number") {
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

export default function ConstraintPayloadEditor({ ruleType, value, onChange }: Props) {
  const payload = parsePayload(value);
  const update = (p: Record<string, unknown>) => onChange(JSON.stringify(p));

  switch (ruleType) {
    case "min_rest_hours":
      return (
        <div>
          <label className="block text-xs font-medium text-slate-500 mb-1">מינימום שעות מנוחה בין משמרות</label>
          <div className="flex items-center gap-2">
            <input
              type="number" min={1} max={72}
              value={Number(payload.hours ?? 8)}
              onChange={e => update({ hours: Number(e.target.value) })}
              className="w-24 border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            <span className="text-sm text-slate-500">שעות</span>
          </div>
        </div>
      );

    case "max_kitchen_per_week":
      return (
        <div className="space-y-3">
          {field("מקסימום משמרות מטבח בשבוע", "max", payload, update)}
          {field("שם סוג המשימה (אופציונלי)", "task_type_name", payload, update, "text")}
        </div>
      );

    case "no_consecutive_burden":
      return (
        <div>
          <label className="block text-xs font-medium text-slate-500 mb-1">רמת עומס לא רצופה</label>
          <select
            value={String(payload.burden_level ?? "hated")}
            onChange={e => update({ burden_level: e.target.value })}
            className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="hated">שנוא</option>
            <option value="disliked">לא אהוב</option>
            <option value="neutral">ניטרלי</option>
          </select>
        </div>
      );

    case "min_base_headcount":
      return (
        <div className="space-y-3">
          {field("מינימום אנשים", "min", payload, update)}
          {field("חלון זמן (שעות)", "window_hours", payload, update)}
        </div>
      );

    case "no_task_type_restriction":
      return (
        <div>
          {field("מזהה סוג משימה", "task_type_id", payload, update, "text")}
        </div>
      );

    default:
      // Fallback: pretty JSON editor for unknown rule types
      return (
        <div>
          <label className="block text-xs font-medium text-slate-500 mb-1">
            פרמטרים (JSON) — סוג כלל: <code className="bg-slate-100 px-1 rounded">{ruleType}</code>
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
