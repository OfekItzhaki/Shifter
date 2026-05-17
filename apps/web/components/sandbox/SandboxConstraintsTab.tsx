"use client";

import { useState, useCallback, useMemo } from "react";
import { useTranslations } from "next-intl";
import {
  useSandboxStore,
  HardConstraintDto,
  SoftConstraintDto,
  ConstraintOverrideMap,
  SolverInputDto,
} from "@/lib/store/sandboxStore";
import ConstraintPayloadEditor from "@/components/ConstraintPayloadEditor";

// ── Types ─────────────────────────────────────────────────────────────────────

type UnifiedConstraint = {
  constraintId: string;
  ruleType: string;
  scopeType: string;
  scopeId: string | null;
  severity: "hard" | "soft";
  payload: Record<string, unknown>;
  weight?: number;
  /** Override status for visual indicators */
  status: "unmodified" | "added" | "modified" | "removed";
};

interface ConstraintFormData {
  ruleType: string;
  severity: "hard" | "soft";
  scopeType: string;
  scopeId: string;
  payload: string; // JSON string
}

// ── Props ─────────────────────────────────────────────────────────────────────

interface SandboxConstraintsTabProps {
  constraintOverrides: ConstraintOverrideMap;
  baseline: SolverInputDto | null;
}

// ── Helper: merge baseline constraints with overrides ─────────────────────────

function buildConstraintList(
  baseline: SolverInputDto | null,
  overrides: ConstraintOverrideMap
): UnifiedConstraint[] {
  const result: UnifiedConstraint[] = [];

  // Collect all baseline constraints (hard + soft)
  const baselineHard = baseline?.hardConstraints ?? [];
  const baselineSoft = baseline?.softConstraints ?? [];

  for (const c of baselineHard) {
    const override = overrides.get(c.constraintId);
    if (!override) {
      result.push({ ...c, severity: "hard", status: "unmodified" });
    } else if (override.action === "edit") {
      const merged = { ...c, ...override.modified } as HardConstraintDto;
      result.push({
        constraintId: merged.constraintId,
        ruleType: merged.ruleType,
        scopeType: merged.scopeType,
        scopeId: merged.scopeId,
        severity: "hard",
        payload: merged.payload,
        status: "modified",
      });
    } else if (override.action === "remove") {
      result.push({ ...c, severity: "hard", status: "removed" });
    }
  }

  for (const c of baselineSoft) {
    const override = overrides.get(c.constraintId);
    if (!override) {
      result.push({ ...c, severity: "soft", status: "unmodified", weight: c.weight });
    } else if (override.action === "edit") {
      const merged = { ...c, ...override.modified } as SoftConstraintDto;
      result.push({
        constraintId: merged.constraintId,
        ruleType: merged.ruleType,
        scopeType: merged.scopeType,
        scopeId: merged.scopeId,
        severity: "soft",
        payload: merged.payload,
        weight: merged.weight,
        status: "modified",
      });
    } else if (override.action === "remove") {
      result.push({ ...c, severity: "soft", status: "removed", weight: c.weight });
    }
  }

  // Add new constraints from overrides
  for (const [id, override] of overrides) {
    if (override.action === "add" && override.modified) {
      const mod = override.modified as HardConstraintDto | SoftConstraintDto;
      const isSoft = "weight" in mod;
      result.push({
        constraintId: mod.constraintId ?? id,
        ruleType: mod.ruleType ?? "",
        scopeType: mod.scopeType ?? "group",
        scopeId: mod.scopeId ?? null,
        severity: isSoft ? "soft" : "hard",
        payload: mod.payload ?? {},
        weight: isSoft ? (mod as SoftConstraintDto).weight : undefined,
        status: "added",
      });
    }
  }

  return result;
}

// ── Status badge styles ───────────────────────────────────────────────────────

const STATUS_STYLES: Record<string, string> = {
  unmodified: "",
  added: "ring-2 ring-green-300 bg-green-50 dark:bg-green-900/20 dark:ring-green-700",
  modified: "ring-2 ring-amber-300 bg-amber-50 dark:bg-amber-900/20 dark:ring-amber-700",
  removed: "ring-2 ring-red-300 bg-red-50 dark:bg-red-900/20 dark:ring-red-700 opacity-60",
};

const STATUS_BADGE_STYLES: Record<string, string> = {
  added: "bg-green-100 text-green-700 dark:bg-green-800 dark:text-green-200",
  modified: "bg-amber-100 text-amber-700 dark:bg-amber-800 dark:text-amber-200",
  removed: "bg-red-100 text-red-700 dark:bg-red-800 dark:text-red-200",
};

// ── Main Component ────────────────────────────────────────────────────────────

export default function SandboxConstraintsTab({
  constraintOverrides,
  baseline,
}: SandboxConstraintsTabProps) {
  const t = useTranslations("sandbox");
  const tConstraints = useTranslations("sandbox.constraints");

  const addConstraint = useSandboxStore((s) => s.addConstraint);
  const editConstraint = useSandboxStore((s) => s.editConstraint);
  const removeConstraint = useSandboxStore((s) => s.removeConstraint);

  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);

  const constraintList = useMemo(
    () => buildConstraintList(baseline, constraintOverrides),
    [baseline, constraintOverrides]
  );

  const totalActive = constraintList.filter((c) => c.status !== "removed").length;
  const overrideCount = constraintOverrides.size;

  // ── Add constraint handler ──────────────────────────────────────────────────

  const handleAdd = useCallback(
    (form: ConstraintFormData) => {
      const id = crypto.randomUUID();
      const payload = safeParseJson(form.payload);

      if (form.severity === "hard") {
        const constraint: HardConstraintDto = {
          constraintId: id,
          ruleType: form.ruleType,
          scopeType: form.scopeType,
          scopeId: form.scopeId || null,
          payload,
        };
        addConstraint(constraint);
      } else {
        const constraint: SoftConstraintDto = {
          constraintId: id,
          ruleType: form.ruleType,
          scopeType: form.scopeType,
          scopeId: form.scopeId || null,
          payload,
          weight: 1,
        };
        addConstraint(constraint);
      }
      setShowForm(false);
    },
    [addConstraint]
  );

  // ── Edit constraint handler ─────────────────────────────────────────────────

  const handleEdit = useCallback(
    (constraintId: string, form: ConstraintFormData) => {
      const payload = safeParseJson(form.payload);
      editConstraint(constraintId, {
        ruleType: form.ruleType,
        scopeType: form.scopeType,
        scopeId: form.scopeId || null,
        payload,
      });
      setEditingId(null);
    },
    [editConstraint]
  );

  // ── Remove constraint handler ───────────────────────────────────────────────

  const handleRemove = useCallback(
    (constraintId: string) => {
      removeConstraint(constraintId);
    },
    [removeConstraint]
  );

  // ── Undo remove (re-add from baseline) ─────────────────────────────────────

  const handleUndoRemove = useCallback(
    (constraintId: string) => {
      // Removing the override effectively restores the baseline constraint
      // We need to delete the override entry from the map
      // The store's removeConstraint marks it as removed, but we need to undo that
      // We can edit it back to its original state by calling editConstraint with empty changes
      // Actually, the cleanest way is to use the store directly
      const state = useSandboxStore.getState();
      const newOverrides = new Map(state.constraintOverrides);
      newOverrides.delete(constraintId);
      useSandboxStore.setState({ constraintOverrides: newOverrides });
    },
    []
  );

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200">
          {t("tabs.constraints")}
        </h3>
        <span className="text-xs text-slate-500 dark:text-slate-400">
          {totalActive} {t("total")}
          {overrideCount > 0 && (
            <span className="ml-1 text-amber-600 dark:text-amber-400">
              ({overrideCount} {t("modified")})
            </span>
          )}
        </span>
      </div>

      {/* Add button */}
      {!showForm && (
        <button
          onClick={() => setShowForm(true)}
          className="flex items-center gap-1.5 text-xs font-medium text-blue-600 dark:text-blue-400 border border-blue-200 dark:border-blue-700 bg-blue-50 dark:bg-blue-900/30 hover:bg-blue-100 dark:hover:bg-blue-900/50 px-3 py-1.5 rounded-lg transition-colors"
        >
          <span>+</span>
          <span>{tConstraints("addConstraint")}</span>
        </button>
      )}

      {/* Add form */}
      {showForm && (
        <ConstraintForm
          onSubmit={handleAdd}
          onCancel={() => setShowForm(false)}
        />
      )}

      {/* Constraint list */}
      <div className="space-y-2">
        {constraintList.length === 0 && (
          <p className="text-xs text-slate-400 dark:text-slate-500 py-2">
            {t("constraintsPlaceholder")}
          </p>
        )}
        {constraintList.map((constraint) => (
          <div key={constraint.constraintId}>
            {editingId === constraint.constraintId ? (
              <ConstraintForm
                initialData={{
                  ruleType: constraint.ruleType,
                  severity: constraint.severity,
                  scopeType: constraint.scopeType,
                  scopeId: constraint.scopeId ?? "",
                  payload: JSON.stringify(constraint.payload, null, 2),
                }}
                onSubmit={(form) => handleEdit(constraint.constraintId, form)}
                onCancel={() => setEditingId(null)}
              />
            ) : (
              <ConstraintRow
                constraint={constraint}
                onEdit={() => setEditingId(constraint.constraintId)}
                onRemove={() => handleRemove(constraint.constraintId)}
                onUndoRemove={() => handleUndoRemove(constraint.constraintId)}
              />
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

// ── Constraint Row ────────────────────────────────────────────────────────────

function ConstraintRow({
  constraint,
  onEdit,
  onRemove,
  onUndoRemove,
}: {
  constraint: UnifiedConstraint;
  onEdit: () => void;
  onRemove: () => void;
  onUndoRemove: () => void;
}) {
  const tConstraints = useTranslations("sandbox.constraints");
  const isRemoved = constraint.status === "removed";

  return (
    <div
      className={`border border-slate-200 dark:border-slate-700 rounded-xl p-3 transition-all ${STATUS_STYLES[constraint.status]}`}
    >
      <div className="flex items-start justify-between gap-2">
        <div className={`space-y-1 flex-1 min-w-0 ${isRemoved ? "line-through" : ""}`}>
          <div className="flex items-center gap-2 flex-wrap">
            {/* Severity badge */}
            <span
              className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-medium border ${
                constraint.severity === "hard"
                  ? "bg-red-50 text-red-700 border-red-200 dark:bg-red-900/30 dark:text-red-300 dark:border-red-700"
                  : "bg-yellow-50 text-yellow-700 border-yellow-200 dark:bg-yellow-900/30 dark:text-yellow-300 dark:border-yellow-700"
              }`}
            >
              {constraint.severity === "hard" ? tConstraints("hard") : tConstraints("soft")}
            </span>

            {/* Rule type */}
            <span className="text-xs font-medium text-slate-700 dark:text-slate-200 truncate">
              {constraint.ruleType}
            </span>

            {/* Scope */}
            {constraint.scopeType && constraint.scopeType !== "group" && (
              <span className="text-[10px] text-slate-500 dark:text-slate-400 bg-slate-100 dark:bg-slate-700 px-1.5 py-0.5 rounded">
                {constraint.scopeType}
                {constraint.scopeId ? `: ${constraint.scopeId.slice(0, 8)}…` : ""}
              </span>
            )}

            {/* Status badge */}
            {constraint.status !== "unmodified" && (
              <span
                className={`text-[10px] font-medium px-1.5 py-0.5 rounded ${STATUS_BADGE_STYLES[constraint.status]}`}
              >
                {tConstraints(constraint.status)}
              </span>
            )}
          </div>

          {/* Payload summary */}
          <p className="text-[11px] text-slate-500 dark:text-slate-400 truncate">
            {formatPayloadSummary(constraint.payload)}
          </p>
        </div>

        {/* Actions */}
        <div className="flex gap-1 flex-shrink-0">
          {isRemoved ? (
            <button
              onClick={onUndoRemove}
              className="text-[10px] text-blue-600 dark:text-blue-400 hover:text-blue-700 border border-blue-200 dark:border-blue-700 px-2 py-1 rounded-lg hover:bg-blue-50 dark:hover:bg-blue-900/30 transition-colors"
            >
              {tConstraints("undo")}
            </button>
          ) : (
            <>
              <button
                onClick={onEdit}
                className="text-[10px] text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 border border-slate-200 dark:border-slate-600 px-2 py-1 rounded-lg hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors"
              >
                {tConstraints("edit")}
              </button>
              <button
                onClick={onRemove}
                className="text-[10px] text-red-500 dark:text-red-400 hover:text-red-700 border border-red-200 dark:border-red-700 px-2 py-1 rounded-lg hover:bg-red-50 dark:hover:bg-red-900/30 transition-colors"
              >
                {tConstraints("remove")}
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
}

// ── Constraint Form ───────────────────────────────────────────────────────────

function ConstraintForm({
  initialData,
  onSubmit,
  onCancel,
}: {
  initialData?: ConstraintFormData;
  onSubmit: (form: ConstraintFormData) => void;
  onCancel: () => void;
}) {
  const tConstraints = useTranslations("sandbox.constraints");

  const [ruleType, setRuleType] = useState(initialData?.ruleType ?? "");
  const [severity, setSeverity] = useState<"hard" | "soft">(initialData?.severity ?? "hard");
  const [scopeType, setScopeType] = useState(initialData?.scopeType ?? "group");
  const [scopeId, setScopeId] = useState(initialData?.scopeId ?? "");
  const [payload, setPayload] = useState(initialData?.payload ?? "{}");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit({ ruleType, severity, scopeType, scopeId, payload });
  };

  return (
    <form
      onSubmit={handleSubmit}
      className="bg-slate-50 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700 rounded-xl p-3 space-y-3"
    >
      {/* Rule type */}
      <div>
        <label className="block text-[11px] font-medium text-slate-500 dark:text-slate-400 mb-1">
          {tConstraints("ruleType")}
        </label>
        <input
          type="text"
          value={ruleType}
          onChange={(e) => setRuleType(e.target.value)}
          required
          placeholder={tConstraints("ruleTypePlaceholder")}
          className="w-full border border-slate-200 dark:border-slate-600 dark:bg-slate-700 dark:text-white rounded-lg px-3 py-2 text-xs focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      {/* Severity */}
      <div>
        <label className="block text-[11px] font-medium text-slate-500 dark:text-slate-400 mb-1">
          {tConstraints("severity")}
        </label>
        <select
          value={severity}
          onChange={(e) => setSeverity(e.target.value as "hard" | "soft")}
          className="w-full border border-slate-200 dark:border-slate-600 dark:bg-slate-700 dark:text-white rounded-lg px-3 py-2 text-xs focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          <option value="hard">{tConstraints("hard")}</option>
          <option value="soft">{tConstraints("soft")}</option>
        </select>
      </div>

      {/* Scope type + ID */}
      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="block text-[11px] font-medium text-slate-500 dark:text-slate-400 mb-1">
            {tConstraints("scopeType")}
          </label>
          <input
            type="text"
            value={scopeType}
            onChange={(e) => setScopeType(e.target.value)}
            placeholder="group"
            className="w-full border border-slate-200 dark:border-slate-600 dark:bg-slate-700 dark:text-white rounded-lg px-3 py-2 text-xs focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>
        <div>
          <label className="block text-[11px] font-medium text-slate-500 dark:text-slate-400 mb-1">
            {tConstraints("scopeId")}
          </label>
          <input
            type="text"
            value={scopeId}
            onChange={(e) => setScopeId(e.target.value)}
            placeholder={tConstraints("scopeIdPlaceholder")}
            className="w-full border border-slate-200 dark:border-slate-600 dark:bg-slate-700 dark:text-white rounded-lg px-3 py-2 text-xs focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>
      </div>

      {/* Payload editor */}
      <div>
        <label className="block text-[11px] font-medium text-slate-500 dark:text-slate-400 mb-1">
          {tConstraints("payload")}
        </label>
        <ConstraintPayloadEditor
          ruleType={ruleType}
          value={payload}
          onChange={setPayload}
        />
      </div>

      {/* Actions */}
      <div className="flex gap-2">
        <button
          type="submit"
          className="bg-blue-500 hover:bg-blue-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg transition-colors"
        >
          {initialData ? tConstraints("save") : tConstraints("add")}
        </button>
        <button
          type="button"
          onClick={onCancel}
          className="text-xs text-slate-500 dark:text-slate-400 border border-slate-200 dark:border-slate-600 px-3 py-1.5 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 transition-colors"
        >
          {tConstraints("cancel")}
        </button>
      </div>
    </form>
  );
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function safeParseJson(json: string): Record<string, unknown> {
  try {
    return JSON.parse(json) ?? {};
  } catch {
    return {};
  }
}

function formatPayloadSummary(payload: Record<string, unknown>): string {
  const entries = Object.entries(payload);
  if (entries.length === 0) return "{}";
  const parts = entries.slice(0, 3).map(([k, v]) => `${k}: ${JSON.stringify(v)}`);
  if (entries.length > 3) parts.push("…");
  return parts.join(", ");
}
