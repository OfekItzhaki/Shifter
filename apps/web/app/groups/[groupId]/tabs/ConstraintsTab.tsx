"use client";

import Modal from "@/components/Modal";
import ConstraintPayloadEditor from "@/components/ConstraintPayloadEditor";
import { SEVERITY_STYLES, SEVERITY_DOTS } from "../types";
import type { ConstraintDto, GroupTaskDto } from "../types";

interface Props {
  isAdmin: boolean;
  constraints: ConstraintDto[];
  constraintsLoading: boolean;
  constraintDeleteErrors: Record<string, string>;
  groupTasks: GroupTaskDto[];
  // Create form
  showConstraintForm: boolean;
  newConstraintRuleType: string;
  newConstraintSeverity: string;
  newConstraintPayload: string;
  constraintSaving: boolean;
  constraintError: string | null;
  // Edit form
  editingConstraintId: string | null;
  editConstraintPayload: string;
  editConstraintFrom: string;
  editConstraintUntil: string;
  editConstraintSaving: boolean;
  editConstraintError: string | null;
  // Handlers
  onOpenCreate: () => void;
  onCloseCreate: () => void;
  onRuleTypeChange: (rt: string) => void;
  onSeverityChange: (v: string) => void;
  onPayloadChange: (v: string) => void;
  onCreateSubmit: (e: React.FormEvent) => void;
  onDeleteConstraint: (id: string) => void;
  onStartEdit: (c: ConstraintDto) => void;
  onCloseEdit: () => void;
  onEditPayloadChange: (v: string) => void;
  onEditFromChange: (v: string) => void;
  onEditUntilChange: (v: string) => void;
  onUpdateConstraint: (id: string) => void;
}

const INP = "border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500";

const RULE_LABELS: Record<string, string> = {
  min_rest_hours: "מינימום מנוחה",
  max_kitchen_per_week: "מקסימום מטבח בשבוע",
  no_consecutive_burden: "ללא עומס רצוף",
  min_base_headcount: "מינימום כוח אדם",
  no_task_type_restriction: "הגבלת סוג משימה",
};

const RULE_DEFAULTS: Record<string, string> = {
  min_rest_hours: '{"hours": 8}',
  max_kitchen_per_week: '{"max": 2, "task_type_name": "kitchen"}',
  no_consecutive_burden: '{"burden_level": "disliked"}',
  min_base_headcount: '{"min": 3, "window_hours": 24}',
  no_task_type_restriction: '{"task_type_id": ""}',
};

function parsePayload(json: string): Record<string, unknown> {
  try { return JSON.parse(json); } catch { return {}; }
}

function formatPayload(ruleType: string, json: string): string {
  try {
    const p = JSON.parse(json);
    if (ruleType === "min_rest_hours") return `${p.hours} שעות`;
    if (ruleType === "max_kitchen_per_week") return `מקסימום ${p.max}`;
    if (ruleType === "no_consecutive_burden") return `עומס: ${p.burden_level}`;
    if (ruleType === "min_base_headcount") return `${p.min} אנשים / ${p.window_hours}ש'`;
    if (ruleType === "no_task_type_restriction") return `סוג: ${p.task_type_id || "—"}`;
    return json;
  } catch { return json; }
}

function LoadingSpinner() {
  return (
    <div className="flex items-center gap-3 text-slate-400 text-sm py-8">
      <svg className="animate-spin h-5 w-5 text-blue-400" fill="none" viewBox="0 0 24 24">
        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
      </svg>
      טוען...
    </div>
  );
}

function ConstraintCreateFields({ ruleType, payload, groupTasks, onPayloadChange }: {
  ruleType: string;
  payload: string;
  groupTasks: GroupTaskDto[];
  onPayloadChange: (v: string) => void;
}) {
  const p = parsePayload(payload);

  if (ruleType === "min_rest_hours") {
    return (
      <div>
        <label className="block text-xs font-medium text-slate-500 mb-1.5">שעות מנוחה מינימליות</label>
        <input type="number" min={1} max={48}
          value={(p.hours as number) ?? 8}
          onChange={e => onPayloadChange(JSON.stringify({ hours: Number(e.target.value) }))}
          className={`w-32 ${INP}`} />
        <p className="text-xs text-slate-400 mt-1">לדוגמה: 8 שעות מנוחה בין סיום משמרת לתחילת הבאה</p>
      </div>
    );
  }
  if (ruleType === "max_kitchen_per_week") {
    return (
      <div>
        <label className="block text-xs font-medium text-slate-500 mb-1.5">מקסימום משמרות מטבח בשבוע</label>
        <input type="number" min={1} max={7}
          value={(p.max as number) ?? 2}
          onChange={e => onPayloadChange(JSON.stringify({ max: Number(e.target.value), task_type_name: "kitchen" }))}
          className={`w-32 ${INP}`} />
      </div>
    );
  }
  if (ruleType === "no_consecutive_burden") {
    return (
      <div>
        <label className="block text-xs font-medium text-slate-500 mb-1.5">רמת עומס שאסור לחזור ברצף</label>
        <select
          value={(p.burden_level as string) ?? "disliked"}
          onChange={e => onPayloadChange(JSON.stringify({ burden_level: e.target.value }))}
          className={`w-full max-w-xs ${INP}`}>
          <option value="disliked">לא אהוב (Disliked)</option>
          <option value="hated">שנוא (Hated)</option>
          <option value="neutral">ניטרלי (Neutral)</option>
        </select>
      </div>
    );
  }
  if (ruleType === "min_base_headcount") {
    return (
      <div className="grid grid-cols-2 gap-4">
        <div>
          <label className="block text-xs font-medium text-slate-500 mb-1.5">מינימום אנשים</label>
          <input type="number" min={1}
            value={(p.min as number) ?? 3}
            onChange={e => onPayloadChange(JSON.stringify({ ...p, min: Number(e.target.value) }))}
            className={`w-full ${INP}`} />
        </div>
        <div>
          <label className="block text-xs font-medium text-slate-500 mb-1.5">חלון זמן (שעות)</label>
          <input type="number" min={1}
            value={(p.window_hours as number) ?? 24}
            onChange={e => onPayloadChange(JSON.stringify({ ...p, window_hours: Number(e.target.value) }))}
            className={`w-full ${INP}`} />
        </div>
      </div>
    );
  }
  if (ruleType === "no_task_type_restriction") {
    return (
      <div>
        <label className="block text-xs font-medium text-slate-500 mb-1.5">סוג משימה מוגבל</label>
        <select
          value={(p.task_type_id as string) ?? ""}
          onChange={e => onPayloadChange(JSON.stringify({ task_type_id: e.target.value }))}
          className={`w-full ${INP}`}>
          <option value="">בחר סוג משימה...</option>
          {groupTasks.map(tt => <option key={tt.id} value={tt.id}>{tt.name}</option>)}
        </select>
        <p className="text-xs text-slate-400 mt-1">האדם לא יוכל לבצע את סוג המשימה הזה</p>
      </div>
    );
  }
  return null;
}

export default function ConstraintsTab({
  isAdmin, constraints, constraintsLoading, constraintDeleteErrors, groupTasks,
  showConstraintForm, newConstraintRuleType, newConstraintSeverity, newConstraintPayload,
  constraintSaving, constraintError,
  editingConstraintId, editConstraintPayload, editConstraintFrom, editConstraintUntil,
  editConstraintSaving, editConstraintError,
  onOpenCreate, onCloseCreate, onRuleTypeChange, onSeverityChange, onPayloadChange,
  onCreateSubmit, onDeleteConstraint, onStartEdit, onCloseEdit,
  onEditPayloadChange, onEditFromChange, onEditUntilChange, onUpdateConstraint,
}: Props) {
  if (constraintsLoading) return <LoadingSpinner />;

  return (
    <div className="space-y-4">
      {isAdmin && (
        <div className="flex justify-end">
          <button onClick={onOpenCreate}
            className="flex items-center gap-2 bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-3.5 py-2 rounded-xl transition-colors">
            <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
            </svg>
            אילוץ
          </button>
        </div>
      )}

      <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-slate-100 bg-slate-50/80">
              <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">כלל</th>
              <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">פרטים</th>
              <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">חומרה</th>
              <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">פעיל</th>
              {isAdmin && <th className="px-4 py-3" />}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {constraints.map(c => (
              <tr key={c.id} className="hover:bg-slate-50/60">
                <td className="px-4 py-3.5 font-medium text-slate-900">{RULE_LABELS[c.ruleType] ?? c.ruleType}</td>
                <td className="px-4 py-3.5 text-slate-500 text-xs">{formatPayload(c.ruleType, c.rulePayloadJson)}</td>
                <td className="px-4 py-3.5">
                  <span className={`inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-xs font-medium border ${SEVERITY_STYLES[c.severity] ?? "bg-slate-100 text-slate-600 border-slate-200"}`}>
                    <span className={`w-1.5 h-1.5 rounded-full ${SEVERITY_DOTS[c.severity] ?? "bg-slate-400"}`} />
                    {c.severity === "hard" ? "קשיח" : c.severity === "soft" ? "רך" : c.severity}
                  </span>
                </td>
                <td className="px-4 py-3.5">
                  <span className={`text-xs font-medium ${c.isActive ? "text-emerald-600" : "text-slate-400"}`}>
                    {c.isActive ? "כן" : "לא"}
                  </span>
                </td>
                {isAdmin && (
                  <td className="px-4 py-3.5">
                    <div className="flex items-center gap-2">
                      <button
                        onClick={() => onStartEdit(c)}
                        className="text-xs text-blue-500 hover:text-blue-700 border border-blue-200 hover:border-blue-400 px-2.5 py-1 rounded-lg transition-colors"
                      >
                        ערוך
                      </button>
                      <button
                        onClick={() => onDeleteConstraint(c.id)}
                        className="text-xs text-red-500 hover:text-red-700 border border-red-200 hover:border-red-400 px-2.5 py-1 rounded-lg transition-colors"
                      >
                        מחק
                      </button>
                    </div>
                    {constraintDeleteErrors[c.id] && (
                      <p className="text-xs text-red-600 mt-1">{constraintDeleteErrors[c.id]}</p>
                    )}
                  </td>
                )}
              </tr>
            ))}
            {constraints.length === 0 && (
              <tr>
                <td colSpan={isAdmin ? 5 : 4} className="px-4 py-12 text-center text-slate-400 text-sm">אין אילוצים</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Create constraint modal */}
      <Modal title="אילוץ חדש" open={showConstraintForm} onClose={onCloseCreate} maxWidth={560}>
        <form onSubmit={onCreateSubmit} className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-medium text-slate-500 mb-1.5">סוג כלל</label>
              <select value={newConstraintRuleType} onChange={e => onRuleTypeChange(e.target.value)} className={`w-full ${INP}`}>
                <option value="min_rest_hours">מינימום מנוחה בין משמרות</option>
                <option value="max_kitchen_per_week">מקסימום משמרות מטבח בשבוע</option>
                <option value="no_consecutive_burden">ללא עומס רצוף</option>
                <option value="min_base_headcount">מינימום כוח אדם בסיסי</option>
                <option value="no_task_type_restriction">הגבלת סוג משימה לאדם</option>
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-500 mb-1.5">חומרה</label>
              <select value={newConstraintSeverity} onChange={e => onSeverityChange(e.target.value)} className={`w-full ${INP}`}>
                <option value="hard">קשיח — חייב להתקיים</option>
                <option value="soft">רך — עדיפות בלבד</option>
              </select>
            </div>
          </div>
          <ConstraintCreateFields
            ruleType={newConstraintRuleType}
            payload={newConstraintPayload}
            groupTasks={groupTasks}
            onPayloadChange={onPayloadChange}
          />
          {constraintError && <p className="text-sm text-red-600">{constraintError}</p>}
          <div className="flex gap-2">
            <button type="submit" disabled={constraintSaving}
              className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
              {constraintSaving ? "שומר..." : "שמור"}
            </button>
            <button type="button" onClick={onCloseCreate}
              className="text-sm text-slate-500 hover:text-slate-700 px-3">ביטול</button>
          </div>
        </form>
      </Modal>

      {/* Edit constraint modal */}
      <Modal title="עריכת אילוץ" open={!!editingConstraintId} onClose={onCloseEdit}>
        <div className="space-y-3">
          <ConstraintPayloadEditor
            ruleType={constraints.find(c => c.id === editingConstraintId)?.ruleType ?? ""}
            value={editConstraintPayload}
            onChange={onEditPayloadChange}
          />
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-slate-500 mb-1">בתוקף מ</label>
              <input type="date" value={editConstraintFrom} onChange={e => onEditFromChange(e.target.value)}
                className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-500 mb-1">בתוקף עד</label>
              <input type="date" value={editConstraintUntil} onChange={e => onEditUntilChange(e.target.value)}
                className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
          </div>
          {editConstraintError && <p className="text-xs text-red-600">{editConstraintError}</p>}
          <div className="flex gap-2">
            <button
              onClick={() => editingConstraintId && onUpdateConstraint(editingConstraintId)}
              disabled={editConstraintSaving}
              className="bg-blue-500 hover:bg-blue-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors"
            >
              {editConstraintSaving ? "שומר..." : "שמור"}
            </button>
            <button onClick={onCloseEdit} className="text-xs text-slate-500 hover:text-slate-700 px-2">ביטול</button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
