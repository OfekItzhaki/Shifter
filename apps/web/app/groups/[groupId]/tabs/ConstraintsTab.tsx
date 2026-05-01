"use client";

import { useState } from "react";
import Modal from "@/components/Modal";
import ConstraintPayloadEditor from "@/components/ConstraintPayloadEditor";
import type { ConstraintDto } from "@/lib/api/constraints";
import type { GroupRoleDto, GroupMemberDto } from "@/lib/api/groups";
import { SEVERITY_STYLES, SEVERITY_DOTS } from "../types";

const RULE_TYPES = [
  { value: "min_rest_hours", label: "מינימום שעות מנוחה" },
  { value: "max_kitchen_per_week", label: "מקסימום מטבח בשבוע" },
  { value: "no_consecutive_burden", label: "ללא עומס רצוף" },
  { value: "min_base_headcount", label: "מינימום כוח אדם בבסיס" },
  { value: "no_task_type_restriction", label: "הגבלת סוג משימה" },
  { value: "emergency_person_bypass", label: "🚨 חריגת חירום — אדם" },
  { value: "emergency_slot_bypass", label: "🚨 חריגת חירום — משמרת" },
  { value: "emergency_space_bypass", label: "🚨 חריגת חירום — כל המרחב" },
];

function formatPayload(ruleType: string, json: string): string {
  try {
    const p = JSON.parse(json);
    switch (ruleType) {
      case "min_rest_hours": return `${p.hours ?? 8} שעות מנוחה`;
      case "max_kitchen_per_week": return `מקסימום ${p.max ?? 2} מטבח בשבוע`;
      case "no_consecutive_burden": return `ללא ${p.burden_level ?? "hated"} רצוף`;
      case "min_base_headcount": return `מינימום ${p.min ?? 3} אנשים בכל ${p.window_hours ?? 24} שעות`;
      case "no_task_type_restriction": return `הגבלה על משימה: ${p.task_type_id ?? "—"}`;
      default: return json;
    }
  } catch { return json; }
}

function normalizeSeverity(sev: string | number): string {
  if (typeof sev === "number") return sev === 0 ? "hard" : "soft";
  return String(sev).toLowerCase();
}

interface ConstraintFormState {
  ruleType: string;
  severity: string;
  payload: string;
  from: string;
  until: string;
  scopeType: "group" | "role" | "person";
  scopeId: string; // roleId or personId (for group scope, filled by parent)
}

interface Props {
  isAdmin: boolean;
  groupId: string;
  constraints: ConstraintDto[];
  constraintsLoading: boolean;
  constraintDeleteErrors: Record<string, string>;
  showConstraintForm: boolean;
  newConstraintRuleType: string;
  newConstraintSeverity: string;
  newConstraintPayload: string;
  newConstraintFrom: string;
  newConstraintUntil: string;
  constraintSaving: boolean;
  constraintError: string | null;
  editingConstraintId: string | null;
  editConstraintPayload: string;
  editConstraintFrom: string;
  editConstraintUntil: string;
  editConstraintSeverity: string;
  editConstraintSaving: boolean;
  editConstraintError: string | null;
  // New props for role and person selectors
  groupRoles: GroupRoleDto[];
  groupRolesLoading: boolean;
  members: GroupMemberDto[];
  onOpenCreate: () => void;
  onCloseCreate: () => void;
  onRuleTypeChange: (v: string) => void;
  onSeverityChange: (v: string) => void;
  onPayloadChange: (v: string) => void;
  onFromChange: (v: string) => void;
  onUntilChange: (v: string) => void;
  onCreateSubmit: (e: React.FormEvent) => void;
  onDeleteConstraint: (id: string) => void;
  onStartEdit: (c: ConstraintDto) => void;
  onCloseEdit: () => void;
  onEditPayloadChange: (v: string) => void;
  onEditFromChange: (v: string) => void;
  onEditUntilChange: (v: string) => void;
  onEditSeverityChange: (v: string) => void;
  onUpdateConstraint: (id: string) => void;
  // Extended create: called with scopeType and scopeId override
  onCreateWithScope?: (scopeType: "group" | "role" | "person", scopeId: string, form: Omit<ConstraintFormState, "scopeType" | "scopeId">) => Promise<void>;
}

// ── Constraint row ────────────────────────────────────────────────────────────
function ConstraintRow({
  c, isAdmin, deleteError, roleName, personName,
  onStartEdit, onDeleteConstraint,
}: {
  c: ConstraintDto;
  isAdmin: boolean;
  deleteError?: string;
  roleName?: string;
  personName?: string;
  onStartEdit: (c: ConstraintDto) => void;
  onDeleteConstraint: (id: string) => void;
}) {
  const sev = normalizeSeverity(c.severity);
  const [confirmDelete, setConfirmDelete] = useState(false);

  return (
    <div className="bg-white border border-slate-200 rounded-xl p-4">
      <div className="flex items-start justify-between gap-2">
        <div className="space-y-1">
          <div className="flex items-center gap-2 flex-wrap">
            <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium border ${SEVERITY_STYLES[sev] ?? "bg-slate-100 text-slate-500 border-slate-200"}`}>
              <span className={`w-1.5 h-1.5 rounded-full ${SEVERITY_DOTS[sev] ?? "bg-slate-400"}`} />
              {sev === "hard" ? "קשה" : sev === "emergency" ? "🚨 חירום" : "רך"}
            </span>
            <span className="text-sm font-medium text-slate-700">{RULE_TYPES.find(r => r.value === c.ruleType)?.label ?? c.ruleType}</span>
            {roleName && <span className="text-xs text-slate-500 bg-slate-100 px-2 py-0.5 rounded-full">{roleName}</span>}
            {personName && <span className="text-xs text-slate-500 bg-slate-100 px-2 py-0.5 rounded-full">{personName}</span>}
          </div>
          <p className="text-xs text-slate-500">{formatPayload(c.ruleType, c.rulePayloadJson)}</p>
          {(c.effectiveFrom || c.effectiveUntil) && (
            <p className="text-xs text-slate-400">
              {c.effectiveFrom ? `מ-${c.effectiveFrom.slice(0, 10)}` : ""}
              {c.effectiveFrom && c.effectiveUntil ? " " : ""}
              {c.effectiveUntil ? `עד ${c.effectiveUntil.slice(0, 10)}` : ""}
            </p>
          )}
        </div>
        {isAdmin && (
          <div className="flex gap-1.5 flex-shrink-0 items-center">
            <button onClick={() => onStartEdit(c)} className="text-xs text-slate-500 hover:text-slate-700 border border-slate-200 px-2 py-1 rounded-lg hover:bg-slate-50 transition-colors">ערוך</button>
            {confirmDelete ? (
              <>
                <span className="text-xs text-slate-600">האם למחוק?</span>
                <button
                  onClick={() => { setConfirmDelete(false); onDeleteConstraint(c.id); }}
                  className="text-xs text-white bg-red-500 hover:bg-red-600 border border-red-500 px-2 py-1 rounded-lg transition-colors"
                >
                  אישור
                </button>
                <button
                  onClick={() => setConfirmDelete(false)}
                  className="text-xs text-slate-500 border border-slate-200 px-2 py-1 rounded-lg hover:bg-slate-50 transition-colors"
                >
                  ביטול
                </button>
              </>
            ) : (
              <button
                onClick={() => setConfirmDelete(true)}
                className="text-xs text-red-500 hover:text-red-700 border border-red-100 px-2 py-1 rounded-lg hover:bg-red-50 transition-colors"
              >
                מחק
              </button>
            )}
          </div>
        )}
      </div>
      {deleteError && <p className="text-xs text-red-600 mt-1">{deleteError}</p>}
    </div>
  );
}

// ── Collapsible section ───────────────────────────────────────────────────────
function ConstraintSection({
  title, count, children, defaultOpen = true,
}: {
  title: string;
  count: number;
  children: React.ReactNode;
  defaultOpen?: boolean;
}) {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <div className="bg-white border border-slate-200 rounded-2xl overflow-hidden">
      <button
        onClick={() => setOpen(o => !o)}
        className="w-full flex items-center justify-between px-5 py-4 hover:bg-slate-50 transition-colors"
      >
        <div className="flex items-center gap-2">
          <span className="text-sm font-semibold text-slate-800">{title}</span>
          <span className="text-xs text-slate-400 bg-slate-100 px-2 py-0.5 rounded-full">{count}</span>
        </div>
        <svg
          className={`w-4 h-4 text-slate-400 transition-transform ${open ? "rotate-180" : ""}`}
          fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}
        >
          <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
        </svg>
      </button>
      {open && <div className="px-5 pb-5 space-y-3">{children}</div>}
    </div>
  );
}

// ── Inline create form for a section ─────────────────────────────────────────
function SectionCreateForm({
  scopeType, groupId, groupRoles, members,
  onSubmit,
}: {
  scopeType: "group" | "role" | "person";
  groupId: string;
  groupRoles: GroupRoleDto[];
  members: GroupMemberDto[];
  onSubmit: (form: ConstraintFormState) => Promise<void>;
}) {
  const [open, setOpen] = useState(false);
  const [ruleType, setRuleType] = useState("min_rest_hours");
  const [severity, setSeverity] = useState("hard");
  const [payload, setPayload] = useState('{"hours": 8}');
  const [from, setFrom] = useState("");
  const [until, setUntil] = useState("");
  const [scopeId, setScopeId] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const activeRoles = groupRoles.filter(r => r.isActive);
  const registeredMembers = members.filter(m => m.invitationStatus === "accepted");

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const resolvedScopeId = scopeType === "group" ? groupId : scopeId;
    if ((scopeType === "role" || scopeType === "person") && !resolvedScopeId) return;
    setSaving(true);
    setError(null);
    try {
      await onSubmit({ ruleType, severity, payload, from, until, scopeType, scopeId: resolvedScopeId });
      setOpen(false);
      setRuleType("min_rest_hours");
      setSeverity("hard");
      setPayload('{"hours": 8}');
      setFrom(""); setUntil(""); setScopeId("");
    } catch (err: unknown) {
      const msg =
        (err as { message?: string })?.message ||
        "שגיאה ביצירת אילוץ";
      setError(msg);
    } finally {
      setSaving(false);
    }
  }

  if (!open) {
    return (
      <button
        onClick={() => setOpen(true)}
        className="flex items-center gap-2 text-sm font-medium text-blue-600 border border-blue-200 bg-blue-50 hover:bg-blue-100 px-4 py-2 rounded-xl transition-colors"
      >
        + {scopeType === "group" ? "אילוץ קבוצה חדש" : scopeType === "role" ? "אילוץ תפקיד חדש" : "אילוץ אישי חדש"}
      </button>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="bg-slate-50 border border-slate-200 rounded-xl p-4 space-y-3">
      {/* Scope selector for role/person */}
      {scopeType === "role" && (
        <div>
          <label className="block text-xs text-slate-500 mb-1">תפקיד</label>
          <select
            value={scopeId}
            onChange={e => setScopeId(e.target.value)}
            required
            className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="">בחר תפקיד...</option>
            {activeRoles.map(r => (
              <option key={r.id} value={r.id}>{r.name}</option>
            ))}
          </select>
        </div>
      )}
      {scopeType === "person" && (
        <div>
          <label className="block text-xs text-slate-500 mb-1">חבר</label>
          <select
            value={scopeId}
            onChange={e => setScopeId(e.target.value)}
            required
            className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="">בחר חבר...</option>
            {registeredMembers.map(m => (
              <option key={m.personId} value={m.personId}>{m.displayName ?? m.fullName}</option>
            ))}
          </select>
        </div>
      )}

      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="block text-xs text-slate-500 mb-1">סוג אילוץ</label>
          <select value={ruleType} onChange={e => setRuleType(e.target.value)} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
            {RULE_TYPES.map(r => <option key={r.value} value={r.value}>{r.label}</option>)}
          </select>
        </div>
        <div>
          <label className="block text-xs text-slate-500 mb-1">חומרה</label>
          <select value={severity} onChange={e => setSeverity(e.target.value)} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
            <option value="hard">קשה (Hard)</option>
            <option value="soft">רך (Soft)</option>
            <option value="emergency">🚨 חירום (Emergency)</option>
          </select>
        </div>
      </div>

      <ConstraintPayloadEditor ruleType={ruleType} value={payload} onChange={setPayload} />

      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="block text-xs text-slate-500 mb-1">בתוקף מ <span className="text-slate-400">(אופציונלי)</span></label>
          <input type="date" value={from} onChange={e => setFrom(e.target.value)} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
        </div>
        <div>
          <label className="block text-xs text-slate-500 mb-1">בתוקף עד <span className="text-slate-400">(אופציונלי)</span></label>
          <input type="date" value={until} onChange={e => setUntil(e.target.value)} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
        </div>
      </div>

      {error && <p className="text-sm text-red-600">{error}</p>}

      <div className="flex gap-2">
        <button type="submit" disabled={saving} className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2 rounded-xl disabled:opacity-50 transition-colors">
          {saving ? "שומר..." : "צור"}
        </button>
        <button type="button" onClick={() => setOpen(false)} className="text-sm text-slate-500 border border-slate-200 px-4 py-2 rounded-xl hover:bg-slate-50 transition-colors">ביטול</button>
      </div>
    </form>
  );
}

// ── Main component ────────────────────────────────────────────────────────────
export default function ConstraintsTab({
  isAdmin, groupId, constraints, constraintsLoading, constraintDeleteErrors,
  showConstraintForm, newConstraintRuleType, newConstraintSeverity, newConstraintPayload, newConstraintFrom, newConstraintUntil, constraintSaving, constraintError,
  editingConstraintId, editConstraintPayload, editConstraintFrom, editConstraintUntil, editConstraintSeverity, editConstraintSaving, editConstraintError,
  groupRoles, groupRolesLoading: _groupRolesLoading, members,
  onOpenCreate, onCloseCreate, onRuleTypeChange, onSeverityChange, onPayloadChange, onFromChange, onUntilChange, onCreateSubmit,
  onDeleteConstraint, onStartEdit, onCloseEdit, onEditPayloadChange, onEditFromChange, onEditUntilChange, onEditSeverityChange, onUpdateConstraint,
  onCreateWithScope,
}: Props) {
  const editingConstraint = constraints.find(c => c.id === editingConstraintId) ?? null;

  // Section-level create state — each SectionCreateForm manages its own internally
  async function handleSectionCreate(form: ConstraintFormState) {
    if (!onCreateWithScope) return;
    await onCreateWithScope(form.scopeType, form.scopeId, form);
  }

  // Partition constraints by scope type
  const groupConstraints = constraints.filter(c => c.scopeType?.toLowerCase() === "group" && c.scopeId === groupId);
  const roleConstraints = constraints.filter(c => c.scopeType?.toLowerCase() === "role");
  const personConstraints = constraints.filter(c => c.scopeType?.toLowerCase() === "person");

  // Build lookup maps
  const roleMap = new Map(groupRoles.map(r => [r.id, r.name]));
  const memberMap = new Map(members.map(m => [m.personId, m.displayName ?? m.fullName]));

  if (constraintsLoading) {
    return <p className="text-sm text-slate-400 py-8">טוען אילוצים...</p>;
  }

  return (
    <div className="space-y-4">
      {/* Group constraints section */}
      <ConstraintSection title="אילוצי קבוצה" count={groupConstraints.length}>
        {groupConstraints.length === 0 && (
          <p className="text-xs text-slate-400 py-2">אין אילוצי קבוצה</p>
        )}
        {groupConstraints.map(c => (
          <ConstraintRow
            key={c.id} c={c} isAdmin={isAdmin}
            deleteError={constraintDeleteErrors[c.id]}
            onStartEdit={onStartEdit}
            onDeleteConstraint={onDeleteConstraint}
          />
        ))}
        {isAdmin && onCreateWithScope && (
          <SectionCreateForm
            scopeType="group" groupId={groupId}
            groupRoles={groupRoles} members={members}
            onSubmit={handleSectionCreate}
          />
        )}
        {isAdmin && !onCreateWithScope && (
          <button onClick={onOpenCreate} className="flex items-center gap-2 text-sm font-medium text-blue-600 border border-blue-200 bg-blue-50 hover:bg-blue-100 px-4 py-2 rounded-xl transition-colors">
            + אילוץ קבוצה חדש
          </button>
        )}
      </ConstraintSection>

      {/* Role constraints section */}
      <ConstraintSection title="אילוצי תפקיד" count={roleConstraints.length} defaultOpen={false}>
        {roleConstraints.length === 0 && (
          <p className="text-xs text-slate-400 py-2">אין אילוצי תפקיד</p>
        )}
        {roleConstraints.map(c => (
          <ConstraintRow
            key={c.id} c={c} isAdmin={isAdmin}
            deleteError={constraintDeleteErrors[c.id]}
            roleName={c.scopeId ? roleMap.get(c.scopeId) : undefined}
            onStartEdit={onStartEdit}
            onDeleteConstraint={onDeleteConstraint}
          />
        ))}
        {isAdmin && onCreateWithScope && (
          <SectionCreateForm
            scopeType="role" groupId={groupId}
            groupRoles={groupRoles} members={members}
            onSubmit={handleSectionCreate}
          />
        )}
      </ConstraintSection>

      {/* Personal constraints section */}
      <ConstraintSection title="אילוצים אישיים" count={personConstraints.length} defaultOpen={false}>
        {personConstraints.length === 0 && (
          <p className="text-xs text-slate-400 py-2">אין אילוצים אישיים</p>
        )}
        {personConstraints.map(c => (
          <ConstraintRow
            key={c.id} c={c} isAdmin={isAdmin}
            deleteError={constraintDeleteErrors[c.id]}
            personName={c.scopeId ? memberMap.get(c.scopeId) : undefined}
            onStartEdit={onStartEdit}
            onDeleteConstraint={onDeleteConstraint}
          />
        ))}
        {isAdmin && onCreateWithScope && (
          <SectionCreateForm
            scopeType="person" groupId={groupId}
            groupRoles={groupRoles} members={members}
            onSubmit={handleSectionCreate}
          />
        )}
      </ConstraintSection>

      {/* Legacy create modal (used when onCreateWithScope is not provided) */}
      <Modal title="אילוץ חדש" open={showConstraintForm} onClose={onCloseCreate} maxWidth={520}>
        <form onSubmit={onCreateSubmit} className="space-y-4">
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
                <option value="hard">קשה (Hard)</option>
                <option value="soft">רך (Soft)</option>
                <option value="emergency">🚨 חירום (Emergency)</option>
              </select>
            </div>
          </div>
          <ConstraintPayloadEditor ruleType={newConstraintRuleType} value={newConstraintPayload} onChange={onPayloadChange} />
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs text-slate-500 mb-1">בתוקף מ <span className="text-slate-400">(אופציונלי)</span></label>
              <input type="date" value={newConstraintFrom} onChange={e => onFromChange(e.target.value)} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
            <div>
              <label className="block text-xs text-slate-500 mb-1">בתוקף עד <span className="text-slate-400">(אופציונלי)</span></label>
              <input type="date" value={newConstraintUntil} onChange={e => onUntilChange(e.target.value)} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
          </div>
          {constraintError && <p className="text-sm text-red-600">{constraintError}</p>}
          <div className="flex gap-2">
            <button type="submit" disabled={constraintSaving} className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
              {constraintSaving ? "שומר..." : "צור"}
            </button>
            <button type="button" onClick={onCloseCreate} className="text-sm text-slate-500 border border-slate-200 px-4 py-2.5 rounded-xl hover:bg-slate-50 transition-colors">ביטול</button>
          </div>
        </form>
      </Modal>

      {/* Edit modal */}
      {editingConstraint && (
        <Modal title="עריכת אילוץ" open={!!editingConstraintId} onClose={onCloseEdit} maxWidth={520}>
          <div className="space-y-4">
            {/* Read-only scope display for personal / role constraints */}
            {editingConstraint.scopeType?.toLowerCase() === "person" && (
              <div>
                <label className="block text-xs text-slate-500 mb-1">חבר (לא ניתן לשינוי)</label>
                <p className="text-sm text-slate-700 bg-slate-50 border border-slate-200 rounded-xl px-3.5 py-2.5">
                  {editingConstraint.scopeId
                    ? (memberMap.get(editingConstraint.scopeId) ?? editingConstraint.scopeId)
                    : "—"}
                </p>
              </div>
            )}
            {editingConstraint.scopeType?.toLowerCase() === "role" && (
              <div>
                <label className="block text-xs text-slate-500 mb-1">תפקיד (לא ניתן לשינוי)</label>
                <p className="text-sm text-slate-700 bg-slate-50 border border-slate-200 rounded-xl px-3.5 py-2.5">
                  {editingConstraint.scopeId
                    ? (roleMap.get(editingConstraint.scopeId) ?? editingConstraint.scopeId)
                    : "—"}
                </p>
              </div>
            )}
            <div>
              <label className="block text-xs text-slate-500 mb-1">חומרה</label>
              <select value={editConstraintSeverity} onChange={e => onEditSeverityChange(e.target.value)} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
                <option value="hard">קשה (Hard)</option>
                <option value="soft">רך (Soft)</option>
                <option value="emergency">🚨 חירום (Emergency)</option>
              </select>
            </div>
            <ConstraintPayloadEditor ruleType={editingConstraint.ruleType} value={editConstraintPayload} onChange={onEditPayloadChange} />
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
              <button onClick={() => onUpdateConstraint(editingConstraint.id)} disabled={editConstraintSaving} className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
                {editConstraintSaving ? "שומר..." : "שמור"}
              </button>
              <button onClick={onCloseEdit} className="text-sm text-slate-500 border border-slate-200 px-4 py-2.5 rounded-xl hover:bg-slate-50 transition-colors">ביטול</button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
}
