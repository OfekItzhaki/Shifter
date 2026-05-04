"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import AppShell from "@/components/shell/AppShell";
import {
  getTaskTypes, createTaskType, getTaskSlots, createTaskSlot,
  TaskTypeDto, TaskSlotDto,
} from "@/lib/api/tasks";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useAuthStore } from "@/lib/store/authStore";

export default function TasksPage() {
  const t = useTranslations("admin");
  const { currentSpaceId } = useSpaceStore();
  const { isAdminMode } = useAuthStore();

  const [taskTypes, setTaskTypes] = useState<TaskTypeDto[]>([]);
  const [slots, setSlots] = useState<TaskSlotDto[]>([]);
  const [tab, setTab] = useState<"types" | "slots">("types");
  const [loading, setLoading] = useState(true);

  // Task type form
  const [showTypeForm, setShowTypeForm] = useState(false);
  const [typeName, setTypeName] = useState("");
  const [typeDesc, setTypeDesc] = useState("");
  const [burden, setBurden] = useState("Neutral");
  const [priority, setPriority] = useState(5);
  const [allowsOverlap, setAllowsOverlap] = useState(false);
  const [savingType, setSavingType] = useState(false);

  // Task slot form
  const [showSlotForm, setShowSlotForm] = useState(false);
  const [slotTypeId, setSlotTypeId] = useState("");
  const [slotStart, setSlotStart] = useState("");
  const [slotEnd, setSlotEnd] = useState("");
  const [headcount, setHeadcount] = useState(1);
  const [slotPriority, setSlotPriority] = useState(5);
  const [location, setLocation] = useState("");
  const [savingSlot, setSavingSlot] = useState(false);

  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  useEffect(() => {
    if (!currentSpaceId) { setLoading(false); return; }
    Promise.all([getTaskTypes(currentSpaceId), getTaskSlots(currentSpaceId)])
      .then(([types, s]) => { setTaskTypes(types); setSlots(s); })
      .finally(() => setLoading(false));
  }, [currentSpaceId]);

  async function handleCreateType(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId) return;
    setSavingType(true); setError(null); setSuccess(null);
    try {
      await createTaskType(currentSpaceId, typeName, typeDesc || null, burden, priority, allowsOverlap);
      const updated = await getTaskTypes(currentSpaceId);
      setTaskTypes(updated);
      setTypeName(""); setTypeDesc(""); setBurden("Neutral"); setPriority(5); setAllowsOverlap(false);
      setShowTypeForm(false);
      setSuccess(t("taskTypeCreated"));
    } catch (err: any) {
      setError(err?.response?.data?.message || t("errorCreateTaskType"));
    } finally { setSavingType(false); }
  }

  async function handleCreateSlot(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId || !slotTypeId) return;
    setSavingSlot(true); setError(null); setSuccess(null);
    try {
      await createTaskSlot(currentSpaceId, slotTypeId,
        new Date(slotStart).toISOString(), new Date(slotEnd).toISOString(),
        headcount, slotPriority, location || null);
      const updated = await getTaskSlots(currentSpaceId);
      setSlots(updated);
      setSlotStart(""); setSlotEnd(""); setHeadcount(1); setLocation(""); setSlotTypeId("");
      setShowSlotForm(false);
      setSuccess(t("taskSlotCreated"));
    } catch (err: any) {
      setError(err?.response?.data?.message || t("errorCreateTaskSlot"));
    } finally { setSavingSlot(false); }
  }

  if (!isAdminMode) {
    return <AppShell><p className="text-slate-500 text-sm p-8">{t("adminRequired")}</p></AppShell>;
  }

  const inp = "w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent";

  const burdenLabels: Record<string, string> = {
    Favorable: t("burden.Favorable"),
    Neutral: t("burden.Neutral"),
    Disliked: t("burden.Disliked"),
    Hated: t("burden.Hated"),
  };
  const burdenColors: Record<string, string> = {
    Favorable: "bg-emerald-50 text-emerald-700 border-emerald-200",
    Neutral: "bg-slate-100 text-slate-600 border-slate-200",
    Disliked: "bg-amber-50 text-amber-700 border-amber-200",
    Hated: "bg-red-50 text-red-700 border-red-200",
  };

  return (
    <AppShell>
      <div className="max-w-4xl space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">{t("tasks")}</h1>
          <p className="text-sm text-slate-500 mt-1">{t("manageTasksSubtitle")}</p>
        </div>

        {/* Tabs + context button */}
        <div className="flex items-center justify-between border-b border-slate-200">
          <div className="flex gap-1">
            {(["types", "slots"] as const).map(tabKey => (
              <button key={tabKey} onClick={() => { setTab(tabKey); setShowTypeForm(false); setShowSlotForm(false); }}
                className={`px-4 py-2.5 text-sm font-medium border-b-2 -mb-px transition-colors ${
                  tab === tabKey ? "border-blue-500 text-blue-600" : "border-transparent text-slate-500 hover:text-slate-700"
                }`}>
                {tabKey === "types" ? t("taskTypes") : t("taskSlots")}
              </button>
            ))}
          </div>
          {/* Button only for current tab */}
          {tab === "types" && (
            <button onClick={() => { setShowTypeForm(true); setShowSlotForm(false); }}
              className="flex items-center gap-2 bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-3.5 py-2 rounded-xl mb-1 transition-colors">
              <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
              </svg>
              {t("addTaskType")}
            </button>
          )}
          {tab === "slots" && (
            <button onClick={() => { setShowSlotForm(true); setShowTypeForm(false); }}
              className="flex items-center gap-2 bg-emerald-500 hover:bg-emerald-600 text-white text-sm font-medium px-3.5 py-2 rounded-xl mb-1 transition-colors">
              <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
              </svg>
              {t("addTaskSlot")}
            </button>
          )}
        </div>

        {error && <div className="bg-red-50 border border-red-200 rounded-xl px-4 py-3 text-sm text-red-700">{error}</div>}
        {success && <div className="bg-emerald-50 border border-emerald-200 rounded-xl px-4 py-3 text-sm text-emerald-700">{success}</div>}

        {/* Task type form */}
        {tab === "types" && showTypeForm && (
          <form onSubmit={handleCreateType} className="bg-white border border-slate-200 rounded-2xl p-5 space-y-4 shadow-sm">
            <h2 className="text-sm font-semibold text-slate-900">{t("newTaskType")}</h2>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">{t("name")} *</label>
                <input value={typeName} onChange={e => setTypeName(e.target.value)} required className={inp} placeholder={t("name")} />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">{t("burdenLevel")}</label>
                <select value={burden} onChange={e => setBurden(e.target.value)} className={inp}>
                  {["Favorable", "Neutral", "Disliked", "Hated"].map(b => (
                    <option key={b} value={b}>{burdenLabels[b]}</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">{t("description")}</label>
                <input value={typeDesc} onChange={e => setTypeDesc(e.target.value)} className={inp} placeholder={t("optional")} />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">{t("priority")} (1–10)</label>
                <input type="number" min={1} max={10} value={priority} onChange={e => setPriority(Number(e.target.value))} className={inp} />
              </div>
            </div>
            <label className="flex items-center gap-2.5 text-sm text-slate-700 cursor-pointer">
              <input type="checkbox" checked={allowsOverlap} onChange={e => setAllowsOverlap(e.target.checked)} className="w-4 h-4 rounded" />
              {t("overlap")}
            </label>
            <div className="flex gap-2">
              <button type="submit" disabled={savingType}
                className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
                {savingType ? t("saving") : t("save")}
              </button>
              <button type="button" onClick={() => setShowTypeForm(false)}
                className="text-sm text-slate-500 hover:text-slate-700 px-3">{t("cancel")}</button>
            </div>
          </form>
        )}

        {/* Task slot form */}
        {tab === "slots" && showSlotForm && (
          <form onSubmit={handleCreateSlot} className="bg-white border border-slate-200 rounded-2xl p-5 space-y-4 shadow-sm">
            <h2 className="text-sm font-semibold text-slate-900">{t("newTaskSlot")}</h2>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">{t("taskType")} *</label>
                <select value={slotTypeId} onChange={e => setSlotTypeId(e.target.value)} required className={inp}>
                  <option value="">{t("taskType")}...</option>
                  {taskTypes.map(tt => <option key={tt.id} value={tt.id}>{tt.name}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">{t("headcount")}</label>
                <input type="number" min={1} value={headcount} onChange={e => setHeadcount(Number(e.target.value))} className={inp} />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">{t("startsAt")} *</label>
                <input type="datetime-local" value={slotStart} onChange={e => setSlotStart(e.target.value)} required className={inp} />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">{t("endsAt")} *</label>
                <input type="datetime-local" value={slotEnd} onChange={e => setSlotEnd(e.target.value)} required className={inp} />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">{t("location")}</label>
                <input value={location} onChange={e => setLocation(e.target.value)} className={inp} placeholder={t("optional")} />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">{t("priority")} (1–10)</label>
                <input type="number" min={1} max={10} value={slotPriority} onChange={e => setSlotPriority(Number(e.target.value))} className={inp} />
              </div>
            </div>
            <div className="flex gap-2">
              <button type="submit" disabled={savingSlot}
                className="bg-emerald-500 hover:bg-emerald-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
                {savingSlot ? t("saving") : t("save")}
              </button>
              <button type="button" onClick={() => setShowSlotForm(false)}
                className="text-sm text-slate-500 hover:text-slate-700 px-3">{t("cancel")}</button>
            </div>
          </form>
        )}

        {loading && <p className="text-slate-400 text-sm py-8">{t("loading")}</p>}

        {/* Task types table */}
        {tab === "types" && !loading && (
          <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-100 bg-slate-50/80">
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">{t("name")}</th>
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">{t("burdenLevel")}</th>
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">{t("priority")}</th>
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">{t("overlap")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {taskTypes.map(tt => (
                  <tr key={tt.id} className="hover:bg-slate-50/60">
                    <td className="px-4 py-3.5 font-medium text-slate-900">{tt.name}</td>
                    <td className="px-4 py-3.5">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border ${burdenColors[tt.burdenLevel] ?? "bg-slate-100 text-slate-600 border-slate-200"}`}>
                        {burdenLabels[tt.burdenLevel] ?? tt.burdenLevel}
                      </span>
                    </td>
                    <td className="px-4 py-3.5 text-slate-500">{tt.defaultPriority}</td>
                    <td className="px-4 py-3.5 text-slate-500">{tt.allowsOverlap ? t("yes") : t("no")}</td>
                  </tr>
                ))}
                {taskTypes.length === 0 && (
                  <tr><td colSpan={4} className="px-4 py-12 text-center text-slate-400 text-sm">{t("noData")}</td></tr>
                )}
              </tbody>
            </table>
          </div>
        )}

        {/* Task slots table */}
        {tab === "slots" && !loading && (
          <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-100 bg-slate-50/80">
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">{t("taskType")}</th>
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">{t("startsAt")}</th>
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">{t("endsAt")}</th>
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">{t("headcount")}</th>
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">{t("status")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {slots.map(s => (
                  <tr key={s.id} className="hover:bg-slate-50/60">
                    <td className="px-4 py-3.5 font-medium text-slate-900">{s.taskTypeName}</td>
                    <td className="px-4 py-3.5 text-slate-500 text-xs">{new Date(s.startsAt).toLocaleString(undefined)}</td>
                    <td className="px-4 py-3.5 text-slate-500 text-xs">{new Date(s.endsAt).toLocaleString(undefined)}</td>
                    <td className="px-4 py-3.5 text-slate-500">{s.requiredHeadcount}</td>
                    <td className="px-4 py-3.5 text-slate-500">{s.status}</td>
                  </tr>
                ))}
                {slots.length === 0 && (
                  <tr><td colSpan={5} className="px-4 py-12 text-center text-slate-400 text-sm">{t("noData")}</td></tr>
                )}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </AppShell>
  );
}
