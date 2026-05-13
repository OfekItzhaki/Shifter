"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import AppShell from "@/components/shell/AppShell";
import { getConstraints, createConstraint, ConstraintDto } from "@/lib/api/constraints";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useAuthStore } from "@/lib/store/authStore";
import AiConstraintParser from "@/components/admin/AiConstraintParser";
import { clsx } from "clsx";

const SEVERITY_STYLES: Record<string, string> = {
  hard: "bg-red-50 text-red-700 border-red-200",
  soft: "bg-blue-50 text-blue-700 border-blue-200",
};
const SEVERITY_DOTS: Record<string, string> = {
  hard: "bg-red-500",
  soft: "bg-blue-500",
};

export default function ConstraintsPage() {
  const t = useTranslations("admin");
  const { currentSpaceId } = useSpaceStore();
  const { isAdminMode } = useAuthStore();
  const [constraints, setConstraints] = useState<ConstraintDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [showManual, setShowManual] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const [scopeType, setScopeType] = useState("space");
  const [severity, setSeverity] = useState("hard");
  const [ruleType, setRuleType] = useState("min_rest_hours");
  const [payload, setPayload] = useState('{"hours": 8}');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!currentSpaceId) { setLoading(false); return; }
    getConstraints(currentSpaceId).then(setConstraints).finally(() => setLoading(false));
  }, [currentSpaceId]);

  async function handleSave(st: string, sid: string | null, sev: string, rt: string, pl: string) {
    if (!currentSpaceId) return;
    setSaving(true); setError(null);
    try {
      await createConstraint(currentSpaceId, st, sid, sev, rt, pl, null, null);
      const updated = await getConstraints(currentSpaceId);
      setConstraints(updated);
      setShowManual(false);
      setSuccess(t("constraintSaved"));
      setTimeout(() => setSuccess(null), 3000);
    } catch { setError(t("errorSaveConstraint")); }
    finally { setSaving(false); }
  }

  async function handleManualSubmit(e: React.FormEvent) {
    e.preventDefault();
    await handleSave(scopeType, null, severity, ruleType, payload);
  }

  async function handleAiConfirm(parsed: any) {
    if (!parsed.ruleType || !parsed.scopeType) return;
    await handleSave(parsed.scopeType, null, "hard", parsed.ruleType, parsed.rulePayloadJson ?? "{}");
  }

  if (!isAdminMode) {
    return (
      <AppShell>
        <div className="flex flex-col items-center justify-center py-20 text-center">
          <svg className="w-12 h-12 text-slate-200 mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
          </svg>
          <p className="text-slate-500 text-sm">{t("adminRequired")}</p>
        </div>
      </AppShell>
    );
  }

  const inputClass = "w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-shadow";

  return (
    <AppShell>
      <div className="max-w-4xl space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-slate-900">{t("constraints")}</h1>
            <p className="text-sm text-slate-500 mt-1">{t("manageConstraintsSubtitle")}</p>
          </div>
          <button onClick={() => setShowManual(!showManual)}
            className="flex items-center gap-2 bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl shadow-sm shadow-blue-500/20 transition-all">
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
            </svg>
            {t("addManually")}
          </button>
        </div>

        <AiConstraintParser onConfirm={handleAiConfirm} />

        {success && (
          <div className="flex items-center gap-3 bg-emerald-50 border border-emerald-200 rounded-xl px-4 py-3 text-sm text-emerald-700">
            <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            {success}
          </div>
        )}
        {error && (
          <div className="flex items-center gap-3 bg-red-50 border border-red-200 rounded-xl px-4 py-3 text-sm text-red-700">
            <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            {error}
          </div>
        )}

        {showManual && (
          <form onSubmit={handleManualSubmit} className="bg-white border border-slate-200 rounded-2xl p-5 space-y-4 shadow-sm">
            <h2 className="text-sm font-semibold text-slate-900">{t("newConstraint")}</h2>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">{t("scopeType")}</label>
                <select value={scopeType} onChange={e => setScopeType(e.target.value)} className={inputClass}>
                  {["space", "person", "role", "group", "task_type"].map(s => (
                    <option key={s} value={s}>{s}</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">{t("severity")}</label>
                <select value={severity} onChange={e => setSeverity(e.target.value)} className={inputClass}>
                  <option value="hard">{t("hard")}</option>
                  <option value="soft">{t("soft")}</option>
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">{t("ruleType")}</label>
                <select value={ruleType} onChange={e => {
                  setRuleType(e.target.value);
                  const defaults: Record<string, string> = {
                    min_rest_hours: '{"hours": 8}',
                    max_kitchen_per_week: '{"max": 2, "task_type_name": "kitchen"}',
                    no_consecutive_burden: '{"burden_level": "hard"}',
                    min_base_headcount: '{"min": 3, "window_hours": 24}',
                    no_task_type_restriction: '{"task_type_id": ""}',
                  };
                  setPayload(defaults[e.target.value] ?? "{}");
                }} className={inputClass}>
                  <option value="min_rest_hours">min_rest_hours</option>
                  <option value="max_kitchen_per_week">max_kitchen_per_week</option>
                  <option value="no_consecutive_burden">no_consecutive_burden</option>
                  <option value="min_base_headcount">min_base_headcount</option>
                  <option value="no_task_type_restriction">no_task_type_restriction</option>
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">Payload (JSON)</label>
                <input value={payload} onChange={e => setPayload(e.target.value)}
                  className={clsx(inputClass, "font-mono text-xs")} />
              </div>
            </div>
            <div className="flex gap-2">
              <button type="submit" disabled={saving}
                className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
                {saving ? t("saving") : t("save")}
              </button>
              <button type="button" onClick={() => setShowManual(false)}
                className="text-sm text-slate-500 hover:text-slate-700 px-3 transition-colors">{t("cancel")}</button>
            </div>
          </form>
        )}

        {loading && (
          <div className="flex items-center gap-3 text-slate-400 text-sm py-8">
            <svg className="animate-spin h-5 w-5 text-blue-400" fill="none" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
            </svg>
            {t("loading")}
          </div>
        )}

        <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-100 bg-slate-50/80">
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">{t("ruleType")}</th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">{t("scopeType")}</th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">{t("severity")}</th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">Payload</th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">{t("active")}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {constraints.map(c => (
                <tr key={c.id} className="hover:bg-slate-50/60 transition-colors">
                  <td className="px-4 py-3.5 font-mono text-xs text-slate-700">{c.ruleType}</td>
                  <td className="px-4 py-3.5 text-slate-500">{c.scopeType}</td>
                  <td className="px-4 py-3.5">
                    <span className={clsx("inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-xs font-medium border",
                      SEVERITY_STYLES[c.severity] ?? "bg-slate-100 text-slate-600 border-slate-200")}>
                      <span className={clsx("w-1.5 h-1.5 rounded-full", SEVERITY_DOTS[c.severity] ?? "bg-slate-400")} />
                      {c.severity}
                    </span>
                  </td>
                  <td className="px-4 py-3.5 font-mono text-xs text-slate-500 max-w-xs truncate">{c.rulePayloadJson}</td>
                  <td className="px-4 py-3.5">
                    <span className={clsx("text-xs font-medium", c.isActive ? "text-emerald-600" : "text-slate-400")}>
                      {c.isActive ? t("yes") : t("no")}
                    </span>
                  </td>
                </tr>
              ))}
              {!loading && constraints.length === 0 && (
                <tr><td colSpan={5} className="px-4 py-12 text-center text-slate-400 text-sm">{t("noData")}</td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </AppShell>
  );
}
