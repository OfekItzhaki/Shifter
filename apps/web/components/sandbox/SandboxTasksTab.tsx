"use client";

import { useState, useMemo, useCallback } from "react";
import { useTranslations } from "next-intl";
import { useAuthStore } from "@/lib/store/authStore";
import { formatLocalDateTime } from "@/lib/utils/formatTime";
import { useSandboxStore, TaskSlotDto, TaskOverrideMap, SolverInputDto } from "@/lib/store/sandboxStore";

// ── Types ─────────────────────────────────────────────────────────────────────

interface TaskFormData {
  taskTypeName: string;
  startsAt: string;
  endsAt: string;
  requiredHeadcount: number;
  burdenLevel: string;
  requiredRoleIds: string[];
  requiredQualificationIds: string[];
}

const EMPTY_FORM: TaskFormData = {
  taskTypeName: "",
  startsAt: "",
  endsAt: "",
  requiredHeadcount: 1,
  burdenLevel: "Normal",
  requiredRoleIds: [],
  requiredQualificationIds: [],
};

type OverrideStatus = "baseline" | "added" | "modified" | "removed";

interface DisplayTask {
  slotId: string;
  taskTypeName: string;
  startsAt: string;
  endsAt: string;
  requiredHeadcount: number;
  burdenLevel: string;
  requiredRoleIds: string[];
  requiredQualificationIds: string[];
  status: OverrideStatus;
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function getStatusBadgeClasses(status: OverrideStatus): string {
  switch (status) {
    case "added":
      return "bg-emerald-50 text-emerald-700 border-emerald-200 dark:bg-emerald-900/30 dark:text-emerald-400 dark:border-emerald-700";
    case "modified":
      return "bg-amber-50 text-amber-700 border-amber-200 dark:bg-amber-900/30 dark:text-amber-400 dark:border-amber-700";
    case "removed":
      return "bg-red-50 text-red-700 border-red-200 dark:bg-red-900/30 dark:text-red-400 dark:border-red-700";
    default:
      return "";
  }
}

function getRowClasses(status: OverrideStatus): string {
  switch (status) {
    case "added":
      return "border-l-2 border-l-emerald-400 dark:border-l-emerald-600";
    case "modified":
      return "border-l-2 border-l-amber-400 dark:border-l-amber-600";
    case "removed":
      return "border-l-2 border-l-red-400 dark:border-l-red-600 opacity-60";
    default:
      return "border-l-2 border-l-transparent";
  }
}

function toLocalDatetime(iso: string): string {
  if (!iso) return "";
  const d = new Date(iso);
  const pad = (n: number) => n.toString().padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

// ── Component ─────────────────────────────────────────────────────────────────

interface SandboxTasksTabProps {
  taskOverrides: TaskOverrideMap;
  baseline: SolverInputDto | null;
}

export default function SandboxTasksTab({ taskOverrides, baseline }: SandboxTasksTabProps) {
  const t = useTranslations("sandbox");
  const timezoneId = useAuthStore(s => s.timezoneId);
  const addTask = useSandboxStore((s) => s.addTask);
  const editTask = useSandboxStore((s) => s.editTask);
  const removeTask = useSandboxStore((s) => s.removeTask);
  const restoreTask = useSandboxStore((s) => s.restoreTask);

  const [showForm, setShowForm] = useState(false);
  const [editingSlotId, setEditingSlotId] = useState<string | null>(null);
  const [form, setForm] = useState<TaskFormData>(EMPTY_FORM);
  const [roleInput, setRoleInput] = useState("");
  const [qualInput, setQualInput] = useState("");

  // Build display list: baseline tasks + added tasks, with override status
  const displayTasks: DisplayTask[] = useMemo(() => {
    const tasks: DisplayTask[] = [];

    // Baseline tasks
    if (baseline?.taskSlots) {
      for (const slot of baseline.taskSlots) {
        const override = taskOverrides.get(slot.slotId);
        if (override?.action === "remove") {
          tasks.push({
            slotId: slot.slotId,
            taskTypeName: slot.taskTypeName,
            startsAt: slot.startsAt,
            endsAt: slot.endsAt,
            requiredHeadcount: slot.requiredHeadcount,
            burdenLevel: slot.burdenLevel,
            requiredRoleIds: slot.requiredRoleIds,
            requiredQualificationIds: slot.requiredQualificationIds,
            status: "removed",
          });
        } else if (override?.action === "edit") {
          const merged = { ...slot, ...override.modified };
          tasks.push({
            slotId: slot.slotId,
            taskTypeName: merged.taskTypeName ?? slot.taskTypeName,
            startsAt: merged.startsAt ?? slot.startsAt,
            endsAt: merged.endsAt ?? slot.endsAt,
            requiredHeadcount: merged.requiredHeadcount ?? slot.requiredHeadcount,
            burdenLevel: merged.burdenLevel ?? slot.burdenLevel,
            requiredRoleIds: merged.requiredRoleIds ?? slot.requiredRoleIds,
            requiredQualificationIds: merged.requiredQualificationIds ?? slot.requiredQualificationIds,
            status: "modified",
          });
        } else {
          tasks.push({
            slotId: slot.slotId,
            taskTypeName: slot.taskTypeName,
            startsAt: slot.startsAt,
            endsAt: slot.endsAt,
            requiredHeadcount: slot.requiredHeadcount,
            burdenLevel: slot.burdenLevel,
            requiredRoleIds: slot.requiredRoleIds,
            requiredQualificationIds: slot.requiredQualificationIds,
            status: "baseline",
          });
        }
      }
    }

    // Added tasks (from overrides)
    for (const [slotId, override] of taskOverrides) {
      if (override.action === "add" && override.modified) {
        const mod = override.modified as TaskSlotDto;
        tasks.push({
          slotId,
          taskTypeName: mod.taskTypeName ?? "",
          startsAt: mod.startsAt ?? "",
          endsAt: mod.endsAt ?? "",
          requiredHeadcount: mod.requiredHeadcount ?? 1,
          burdenLevel: mod.burdenLevel ?? "Normal",
          requiredRoleIds: mod.requiredRoleIds ?? [],
          requiredQualificationIds: mod.requiredQualificationIds ?? [],
          status: "added",
        });
      }
    }

    return tasks;
  }, [baseline, taskOverrides]);

  const totalTasks = displayTasks.filter((t) => t.status !== "removed").length;
  const overrideCount = taskOverrides.size;

  const handleOpenAddForm = useCallback(() => {
    setEditingSlotId(null);
    setForm(EMPTY_FORM);
    setRoleInput("");
    setQualInput("");
    setShowForm(true);
  }, []);

  const handleOpenEditForm = useCallback((task: DisplayTask) => {
    setEditingSlotId(task.slotId);
    setForm({
      taskTypeName: task.taskTypeName,
      startsAt: toLocalDatetime(task.startsAt),
      endsAt: toLocalDatetime(task.endsAt),
      requiredHeadcount: task.requiredHeadcount,
      burdenLevel: task.burdenLevel,
      requiredRoleIds: task.requiredRoleIds,
      requiredQualificationIds: task.requiredQualificationIds,
    });
    setRoleInput("");
    setQualInput("");
    setShowForm(true);
  }, []);

  const handleRemove = useCallback((slotId: string) => {
    removeTask(slotId);
  }, [removeTask]);

  const handleRestore = useCallback((slotId: string) => {
    restoreTask(slotId);
  }, [restoreTask]);

  const handleSubmit = useCallback((e: React.FormEvent) => {
    e.preventDefault();
    if (!form.taskTypeName || !form.startsAt || !form.endsAt) return;

    if (editingSlotId) {
      // Edit existing task
      editTask(editingSlotId, {
        taskTypeName: form.taskTypeName,
        startsAt: new Date(form.startsAt).toISOString(),
        endsAt: new Date(form.endsAt).toISOString(),
        requiredHeadcount: form.requiredHeadcount,
        burdenLevel: form.burdenLevel,
        requiredRoleIds: form.requiredRoleIds,
        requiredQualificationIds: form.requiredQualificationIds,
      });
    } else {
      // Add new task
      const newSlotId = `sandbox-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
      addTask({
        slotId: newSlotId,
        taskTypeId: "",
        taskTypeName: form.taskTypeName,
        burdenLevel: form.burdenLevel,
        startsAt: new Date(form.startsAt).toISOString(),
        endsAt: new Date(form.endsAt).toISOString(),
        requiredHeadcount: form.requiredHeadcount,
        priority: 5,
        requiredRoleIds: form.requiredRoleIds,
        requiredQualificationIds: form.requiredQualificationIds,
        allowsOverlap: false,
      });
    }

    setShowForm(false);
    setEditingSlotId(null);
    setForm(EMPTY_FORM);
  }, [form, editingSlotId, addTask, editTask]);

  const handleAddRole = useCallback(() => {
    const trimmed = roleInput.trim();
    if (trimmed && !form.requiredRoleIds.includes(trimmed)) {
      setForm((prev) => ({ ...prev, requiredRoleIds: [...prev.requiredRoleIds, trimmed] }));
    }
    setRoleInput("");
  }, [roleInput, form.requiredRoleIds]);

  const handleRemoveRole = useCallback((role: string) => {
    setForm((prev) => ({ ...prev, requiredRoleIds: prev.requiredRoleIds.filter((r) => r !== role) }));
  }, []);

  const handleAddQual = useCallback(() => {
    const trimmed = qualInput.trim();
    if (trimmed && !form.requiredQualificationIds.includes(trimmed)) {
      setForm((prev) => ({ ...prev, requiredQualificationIds: [...prev.requiredQualificationIds, trimmed] }));
    }
    setQualInput("");
  }, [qualInput, form.requiredQualificationIds]);

  const handleRemoveQual = useCallback((qual: string) => {
    setForm((prev) => ({ ...prev, requiredQualificationIds: prev.requiredQualificationIds.filter((q) => q !== qual) }));
  }, []);

  const burdenLabels: Record<string, string> = {
    Easy: t("burdenEasy"),
    Normal: t("burdenNormal"),
    Hard: t("burdenHard"),
  };

  const inp = "w-full border border-slate-200 dark:border-slate-600 dark:bg-slate-800 dark:text-white rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent";

  return (
    <div className="space-y-3">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200">
          {t("tabs.tasks")}
        </h3>
        <span className="text-xs text-slate-500 dark:text-slate-400">
          {totalTasks} {t("total")}
          {overrideCount > 0 && (
            <span className="ml-1 text-amber-600 dark:text-amber-400">
              ({overrideCount} {t("modified")})
            </span>
          )}
        </span>
      </div>

      {/* Add Task button */}
      <button
        onClick={handleOpenAddForm}
        className="flex items-center gap-1.5 text-xs font-medium text-blue-600 dark:text-blue-400 hover:text-blue-700 dark:hover:text-blue-300 transition-colors"
      >
        <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
        </svg>
        {t("addTask")}
      </button>

      {/* Task Form (Add / Edit) */}
      {showForm && (
        <form onSubmit={handleSubmit} className="bg-slate-50 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700 rounded-xl p-4 space-y-3">
          <h4 className="text-xs font-semibold text-slate-700 dark:text-slate-200">
            {editingSlotId ? t("editTask") : t("addTask")}
          </h4>

          <div className="grid grid-cols-2 gap-3">
            {/* Name */}
            <div className="col-span-2">
              <label className="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">{t("taskName")} *</label>
              <input
                type="text"
                value={form.taskTypeName}
                onChange={(e) => setForm((prev) => ({ ...prev, taskTypeName: e.target.value }))}
                required
                className={inp}
                placeholder={t("taskName")}
              />
            </div>

            {/* Time window */}
            <div>
              <label className="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">{t("startsAt")} *</label>
              <input
                type="datetime-local"
                value={form.startsAt}
                onChange={(e) => setForm((prev) => ({ ...prev, startsAt: e.target.value }))}
                required
                className={inp}
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">{t("endsAt")} *</label>
              <input
                type="datetime-local"
                value={form.endsAt}
                onChange={(e) => setForm((prev) => ({ ...prev, endsAt: e.target.value }))}
                required
                className={inp}
              />
            </div>

            {/* Headcount */}
            <div>
              <label className="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">{t("headcount")}</label>
              <input
                type="number"
                min={1}
                value={form.requiredHeadcount}
                onChange={(e) => setForm((prev) => ({ ...prev, requiredHeadcount: Math.max(1, Number(e.target.value)) }))}
                className={inp}
              />
            </div>

            {/* Burden level */}
            <div>
              <label className="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">{t("burdenLevel")}</label>
              <select
                value={form.burdenLevel}
                onChange={(e) => setForm((prev) => ({ ...prev, burdenLevel: e.target.value }))}
                className={inp}
              >
                <option value="Easy">{burdenLabels.Easy}</option>
                <option value="Normal">{burdenLabels.Normal}</option>
                <option value="Hard">{burdenLabels.Hard}</option>
              </select>
            </div>
          </div>

          {/* Required Roles (tags) */}
          <div>
            <label className="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">{t("requiredRoles")}</label>
            <div className="flex gap-2">
              <input
                type="text"
                value={roleInput}
                onChange={(e) => setRoleInput(e.target.value)}
                onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); handleAddRole(); } }}
                className={inp}
                placeholder={t("addRolePlaceholder")}
              />
              <button
                type="button"
                onClick={handleAddRole}
                className="px-3 py-2 text-xs font-medium bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-300 rounded-lg hover:bg-slate-300 dark:hover:bg-slate-600 transition-colors"
              >
                +
              </button>
            </div>
            {form.requiredRoleIds.length > 0 && (
              <div className="flex flex-wrap gap-1.5 mt-2">
                {form.requiredRoleIds.map((role) => (
                  <span key={role} className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs bg-blue-50 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300 border border-blue-200 dark:border-blue-700">
                    {role}
                    <button type="button" onClick={() => handleRemoveRole(role)} className="text-blue-400 hover:text-blue-600">×</button>
                  </span>
                ))}
              </div>
            )}
          </div>

          {/* Required Qualifications (tags) */}
          <div>
            <label className="block text-xs font-medium text-slate-500 dark:text-slate-400 mb-1">{t("requiredQualifications")}</label>
            <div className="flex gap-2">
              <input
                type="text"
                value={qualInput}
                onChange={(e) => setQualInput(e.target.value)}
                onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); handleAddQual(); } }}
                className={inp}
                placeholder={t("addQualPlaceholder")}
              />
              <button
                type="button"
                onClick={handleAddQual}
                className="px-3 py-2 text-xs font-medium bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-300 rounded-lg hover:bg-slate-300 dark:hover:bg-slate-600 transition-colors"
              >
                +
              </button>
            </div>
            {form.requiredQualificationIds.length > 0 && (
              <div className="flex flex-wrap gap-1.5 mt-2">
                {form.requiredQualificationIds.map((qual) => (
                  <span key={qual} className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs bg-purple-50 dark:bg-purple-900/30 text-purple-700 dark:text-purple-300 border border-purple-200 dark:border-purple-700">
                    {qual}
                    <button type="button" onClick={() => handleRemoveQual(qual)} className="text-purple-400 hover:text-purple-600">×</button>
                  </span>
                ))}
              </div>
            )}
          </div>

          {/* Form actions */}
          <div className="flex gap-2 pt-1">
            <button
              type="submit"
              className="bg-blue-500 hover:bg-blue-600 text-white text-xs font-medium px-3.5 py-2 rounded-lg transition-colors"
            >
              {editingSlotId ? t("saveChanges") : t("addTask")}
            </button>
            <button
              type="button"
              onClick={() => { setShowForm(false); setEditingSlotId(null); }}
              className="text-xs text-slate-500 hover:text-slate-700 dark:text-slate-400 dark:hover:text-slate-200 px-3 py-2"
            >
              {t("cancel")}
            </button>
          </div>
        </form>
      )}

      {/* Task List */}
      <div className="space-y-1.5">
        {displayTasks.length === 0 && (
          <p className="text-xs text-slate-400 dark:text-slate-500 py-4 text-center">
            {t("noTasks")}
          </p>
        )}
        {displayTasks.map((task) => (
          <div
            key={task.slotId}
            className={`flex items-center justify-between p-2.5 rounded-lg bg-white dark:bg-slate-800 border border-slate-100 dark:border-slate-700 ${getRowClasses(task.status)}`}
          >
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2">
                <span className={`text-sm font-medium text-slate-800 dark:text-slate-200 truncate ${task.status === "removed" ? "line-through" : ""}`}>
                  {task.taskTypeName}
                </span>
                {task.status !== "baseline" && (
                  <span className={`inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium border ${getStatusBadgeClasses(task.status)}`}>
                    {t(`status.${task.status}`)}
                  </span>
                )}
              </div>
              <div className="flex items-center gap-3 mt-0.5">
                <span className="text-[11px] text-slate-500 dark:text-slate-400">
                  {formatLocalDateTime(task.startsAt, timezoneId)}
                  {" – "}
                  {formatLocalDateTime(task.endsAt, timezoneId)}
                </span>
                <span className="text-[11px] text-slate-400 dark:text-slate-500">
                  👥 {task.requiredHeadcount}
                </span>
                <span className={`text-[11px] px-1.5 py-0.5 rounded ${
                  task.burdenLevel === "Hard" ? "bg-red-50 text-red-600 dark:bg-red-900/20 dark:text-red-400" :
                  task.burdenLevel === "Easy" ? "bg-emerald-50 text-emerald-600 dark:bg-emerald-900/20 dark:text-emerald-400" :
                  "bg-slate-100 text-slate-500 dark:bg-slate-700 dark:text-slate-400"
                }`}>
                  {burdenLabels[task.burdenLevel] ?? task.burdenLevel}
                </span>
              </div>
            </div>

            {/* Action buttons */}
            <div className="flex items-center gap-1 ml-2 shrink-0">
              {task.status === "removed" ? (
                <button
                  onClick={() => handleRestore(task.slotId)}
                  className="p-1.5 rounded-md text-slate-400 hover:text-emerald-600 hover:bg-emerald-50 dark:hover:bg-emerald-900/20 transition-colors"
                  title={t("restore")}
                >
                  <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M3 10h10a5 5 0 015 5v2M3 10l4-4M3 10l4 4" />
                  </svg>
                </button>
              ) : (
                <>
                  <button
                    onClick={() => handleOpenEditForm(task)}
                    className="p-1.5 rounded-md text-slate-400 hover:text-blue-600 hover:bg-blue-50 dark:hover:bg-blue-900/20 transition-colors"
                    title={t("editTask")}
                  >
                    <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                    </svg>
                  </button>
                  <button
                    onClick={() => handleRemove(task.slotId)}
                    className="p-1.5 rounded-md text-slate-400 hover:text-red-600 hover:bg-red-50 dark:hover:bg-red-900/20 transition-colors"
                    title={t("removeTask")}
                  >
                    <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                    </svg>
                  </button>
                </>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
