"use client";

import ConstraintPayloadEditor from "@/components/ConstraintPayloadEditor";
import type { ConstraintDto } from "@/lib/api/constraints";
import { SEVERITY_STYLES, SEVERITY_DOTS } from "../types";

const RULE_TYPES = [
  { value: "min_rest_hours", label: "מינימום שעות מנוחה" },
  { value: "max_kitchen_per_week", label: "מקסימום מטבח בשבוע" },
  { value: "no_consecutive_burden", label: "ללא עומס רצוף" },
  { value: "min_base_headcount", label: "מינימום כוח אדם בבסיס" },
  { value: "no_task_type_restriction", label: "הגבלת סוג משימה" },
];

interface Props {
  isAdmin: boolean;
  constraints: ConstraintDto[];
  constraintsLoading: boolean;
  constraintDeleteErrors: Record<string, string>;
  showConstraintForm: boolean;
  newConstraintRuleType: string;
  newConstraintSeverity: string;
  newConstraintPayload: string;
  constraintSaving: boolean;
  constraintError: string | null;
  editingConstraintId: string | null;
  editConstraintPayload: string;
  editConstraintFrom: string;
  editConstraintUntil: string;
  editConstraintSaving: boolean;
  editConstraintError: string | null;
  onOpenCreate: () => void;
  onCloseCreate: () => void;
  onRuleTypeChange: (v: string) => void;
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

export default function ConstraintsTab({
  isAdmin, constraints, constraintsLoading, constraintDeleteErrors,
  showConstraintForm, newConstraintRuleType, newConstraintSeverity, newConstraintPayload, constraintSaving, constraintError,
  editingConstraintId, editConstraintPayload, editConstraintFrom, editConstraintUntil, editConstraintSaving, editConstraintError,
  onOpenCreate, onCloseCreate, onRuleTypeChange, onSeverityChange, onPayloadChange, onCreateSubmit,
  onDeleteConstraint, onStartEdit, onCloseEdit, onEditPayloadChange, onEditFromChange, onEditUntilChange, onUpdateConstraint,
}: Props) {
  return (
    <div className="space-y-4">
      {isAdmin && !showConstraintForm && (
        <button onClick={onOpenCreate} className="flex items-center gap-2 text-sm font-medium text-blue-600 border border-blue-200 bg-blue-50 hover:bg-blue-100 px-4 py-2.5 rounded-xl transition-colors">
          + אילוץ חדש
        </button>
      )}

      {showConstraintForm && (
        <form onSubmit={onCreateSubmit} className="bg-white border border-slate-200 rounded-2xl p-4 space-y-3">
          <h3 className="text-sm font-semibold text-slate-700">אילוץ חדש</h3>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs text-slate-500 mb-1">סוג אילוץ</label>
              <select value={newConstraintRuleType} onChange={e => onRuleTypeChange(e.target.value)} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
                {RULE_TYPES.map(r => <option key={r.value} value={r.value}>{r.label}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-xs text-slate-500 mb-1">חומרה</label>
              <select value={newConstraintSeverity} onChange={e => onSeverityChange(e.target.value)} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
                <option value="hard">קשה</option>
                <option value="soft">רך</option>
              </select>
            </div>
          </div>
          <ConstraintPayloadEditor ruleType={newConstraintRuleType} value={newConstraintPayload} onChange={onPayloadChange} />
          {constraintError && <p className="text-sm text-red-600">{constraintError}</p>}
          <div className="flex gap-2">
            <button type="submit" disabled={constraintSaving} className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
              {constraintSaving ? "שומר..." : "צור"}
            </button>
            <button type="button" onClick={onCloseCreate} className="text-sm text-slate-500 border border-slate-200 px-4 py-2.5 rounded-xl hover:bg-slate-50 transition-colors">ביטול</button>
          </div>
        </form>
      )}

      {constraintsLoading && <p className="text-sm text-slate-400 py-8">טוען אילוצים...</p>}

      {!constraintsLoading && constraints.length === 0 && (
        <div className="flex flex-col items-center justify-center py-16 text-center bg-white rounded-xl border border-slate-200">
          <p className="text-slate-400 text-sm">אין אילוצים מוגדרים</p>
        </div>
      )}

      <div className="space-y-2">
        {constraints.map(c => (
          <div key={c.id} className="bg-white border border-slate-200 rounded-xl p-4 space-y-2">
            {editingConstraintId === c.id ? (
              <div className="space-y-3">
                <ConstraintPayloadEditor ruleType={c.ruleType} value={editConstraintPayload} onChange={onEditPayloadChange} />
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block text-xs text-slate-500 mb-1">בתוקף מ</label>
                    <input type="date" value={editConstraintFrom} onChange={e => onEditFromChange(e.target.value)} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
                  </div>
                  <div>
                    <label className="block text-xs text-slate-500 mb-1">בתוקף עד</label>
                    <input type="date" value={editConstraintUntil} onChange={e => onEditUntilChange(e.target.value)} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
                  </div>
                </div>
                {editConstraintError && <p className="text-sm text-red-600">{editConstraintError}</p>}
                <div className="flex gap-2">
                  <button onClick={() => onUpdateConstraint(c.id)} disabled={editConstraintSaving} className="bg-blue-500 hover:bg-blue-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors">{editConstraintSaving ? "שומר..." : "שמור"}</button>
                  <button onClick={onCloseEdit} className="text-xs text-slate-500 border border-slate-200 px-3 py-1.5 rounded-lg hover:bg-slate-50 transition-colors">ביטול</button>
                </div>
              </div>
            ) : (
              <div className="flex items-start justify-between gap-2">
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium border ${SEVERITY_STYLES[c.severity] ?? "bg-slate-100 text-slate-500 border-slate-200"}`}>
                      <span className={`w-1.5 h-1.5 rounded-full ${SEVERITY_DOTS[c.severity] ?? "bg-slate-400"}`} />
                      {c.severity === "hard" ? "קשה" : "רך"}
                    </span>
                    <span className="text-sm font-medium text-slate-700">{RULE_TYPES.find(r => r.value === c.ruleType)?.label ?? c.ruleType}</span>
                  </div>
                  <p className="text-xs text-slate-400 font-mono">{c.rulePayloadJson}</p>
                </div>
                {isAdmin && (
                  <div className="flex gap-1.5 flex-shrink-0">
                    <button onClick={() => onStartEdit(c)} className="text-xs text-slate-500 hover:text-slate-700 border border-slate-200 px-2 py-1 rounded-lg hover:bg-slate-50 transition-colors">ערוך</button>
                    <button onClick={() => onDeleteConstraint(c.id)} className="text-xs text-red-500 hover:text-red-700 border border-red-100 px-2 py-1 rounded-lg hover:bg-red-50 transition-colors">מחק</button>
                  </div>
                )}
                {constraintDeleteErrors[c.id] && <p className="text-xs text-red-600">{constraintDeleteErrors[c.id]}</p>}
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
