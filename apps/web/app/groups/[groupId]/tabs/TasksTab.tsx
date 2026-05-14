"use client";

import Modal from "@/components/Modal";
import { useState, useEffect } from "react";
import { useTranslations } from "next-intl";
import type { GroupTaskDto } from "@/lib/api/tasks";
import type { GroupQualificationDto } from "@/lib/api/groups";
import { burdenLabels, burdenColors } from "../types";

const BURDEN_OPTIONS = ["easy", "normal", "hard"];

export interface TaskForm {
  name: string;
  startsAt: string;
  endsAt: string;
  shiftDurationMinutes: number;
  requiredHeadcount: number;
  burdenLevel: string;
  allowsDoubleShift: boolean;
  allowsOverlap: boolean;
  concurrentTaskIds: string[];
  dailyStartTime: string;
  dailyEndTime: string;
  qualificationRequirements: Array<{ qualificationName: string; count: number; mandatory: boolean }>;
}

interface Props {
  isAdmin: boolean;
  groupTasks: GroupTaskDto[];
  groupTasksLoading: boolean;
  groupQualifications: GroupQualificationDto[];
  showTaskForm: boolean;
  editingTask: GroupTaskDto | null;
  taskForm: TaskForm;
  taskSaving: boolean;
  taskError: string | null;
  onOpenCreate: () => void;
  onOpenImport?: () => void;
  onCloseForm: () => void;
  onFormChange: (f: TaskForm) => void;
  onFormSubmit: (e: React.FormEvent) => void;
  onEditTask: (t: GroupTaskDto) => void;
  onDeleteTask: (id: string) => void;
}

/** Split total minutes into hours + minutes for display */
function minutesToHM(total: number): { hours: number; mins: number } {
  return { hours: Math.floor(total / 60), mins: total % 60 };
}

function formatDuration(mins: number): string {
  const h = Math.floor(mins / 60);
  const m = mins % 60;
  if (m === 0) return `${h}h`;
  return `${h}h ${m}m`;
}

/** Sub-shift editor: lets admin split a shift into N equal parts */
function SubShiftEditor({ totalMinutes, onChange }: { totalMinutes: number; onChange: (mins: number) => void }) {
  const t = useTranslations("groups.tasks_tab");
  const [originalMinutes, setOriginalMinutes] = useState(totalMinutes);
  const [numSubShifts, setNumSubShifts] = useState(1);

  useEffect(() => {
    setOriginalMinutes(totalMinutes);
    setNumSubShifts(1);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []); // only on mount

  function addSubShift() {
    const next = numSubShifts + 1;
    setNumSubShifts(next);
    onChange(Math.max(1, Math.round(originalMinutes / next)));
  }

  function removeSubShift() {
    if (numSubShifts <= 1) return;
    const next = numSubShifts - 1;
    setNumSubShifts(next);
    if (next === 1) {
      onChange(originalMinutes);
    } else {
      onChange(Math.max(1, Math.round(originalMinutes / next)));
    }
  }

  if (numSubShifts <= 1 && originalMinutes <= 60) return null;

  return (
    <div className="bg-slate-50 border border-slate-200 rounded-xl p-3 space-y-2">
      <div className="flex items-center justify-between">
        <div>
          <p className="text-xs font-semibold text-slate-600">{t("subShifts")}</p>
          {numSubShifts > 1 && (
            <p className="text-xs text-slate-400">
              {t("subShiftsCount", { count: numSubShifts, duration: formatDuration(Math.round(originalMinutes / numSubShifts)) })}
            </p>
          )}
        </div>
        <div className="flex items-center gap-2">
          {numSubShifts > 1 && (
            <button
              type="button"
              onClick={removeSubShift}
              className="w-7 h-7 rounded-lg border border-slate-200 bg-white text-slate-500 hover:bg-red-50 hover:text-red-600 hover:border-red-200 text-sm font-bold transition-colors flex items-center justify-center"
            >
              −
            </button>
          )}
          <span className="text-sm font-semibold text-slate-700 min-w-[1.5rem] text-center">{numSubShifts}</span>
          <button
            type="button"
            onClick={addSubShift}
            className="w-7 h-7 rounded-lg border border-blue-200 bg-blue-50 text-blue-600 hover:bg-blue-100 text-sm font-bold transition-colors flex items-center justify-center"
          >
            +
          </button>
        </div>
      </div>
      {numSubShifts === 1 && (
        <p className="text-xs text-slate-400">{t("subShiftsDesc")}</p>
      )}
    </div>
  );
}

export default function TasksTab({
  isAdmin, groupTasks, groupTasksLoading, groupQualifications, showTaskForm, editingTask, taskForm,
  taskSaving, taskError, onOpenCreate, onOpenImport, onCloseForm, onFormChange, onFormSubmit, onEditTask, onDeleteTask,
}: Props) {
  const t = useTranslations("groups.tasks_tab");
  const tCommon = useTranslations("common");
  const { hours: durHours, mins: durMins } = minutesToHM(taskForm.shiftDurationMinutes);
  const [confirmDeleteTask, setConfirmDeleteTask] = useState<string | null>(null);

  function setDuration(h: number, m: number) {
    onFormChange({ ...taskForm, shiftDurationMinutes: Math.max(1, h * 60 + m) });
  }

  // Tasks that can be selected as concurrent (all tasks except the one being edited)
  const concurrentOptions = groupTasks.filter(t => !editingTask || t.id !== editingTask.id);

  return (
    <div className="space-y-4">
      {isAdmin && (
        <div className="flex items-center gap-2">
          <button onClick={onOpenCreate} className="flex items-center gap-2 text-sm font-medium text-blue-600 border border-blue-200 bg-blue-50 hover:bg-blue-100 px-4 py-2.5 rounded-xl transition-colors">
            {t("newTask")}
          </button>
        </div>
      )}

      {groupTasksLoading && <p className="text-sm text-slate-400 py-8">{t("loadingTasks")}</p>}

      {!groupTasksLoading && groupTasks.length === 0 && (
        <div className="flex flex-col items-center justify-center py-16 text-center bg-white rounded-xl border border-slate-200">
          <p className="text-slate-400 text-sm">{t("noTasks")}</p>
        </div>
      )}

      <div className="space-y-2">
        {groupTasks.map(task => (
          <div key={task.id} className="flex items-center gap-3 bg-white border border-slate-200 rounded-xl px-4 py-3">
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium text-slate-900 truncate">{task.name}</p>
              <p className="text-xs text-slate-400">
                {task.requiredHeadcount} {t("people")} · {minutesToHM(task.shiftDurationMinutes).hours} {tCommon("hours")}{minutesToHM(task.shiftDurationMinutes).mins > 0 ? ` ${minutesToHM(task.shiftDurationMinutes).mins} ${tCommon("minutes")}` : ""}
              </p>
              {task.qualificationRequirements?.length > 0 && (
                <div className="flex flex-wrap gap-1 mt-1">
                  {task.qualificationRequirements.map((req, idx) => (
                    <span key={idx} className={`inline-flex items-center px-1.5 py-0.5 rounded-md text-[10px] font-medium border ${
                      req.mandatory
                        ? "bg-red-50 text-red-700 border-red-200"
                        : "bg-violet-50 text-violet-700 border-violet-200"
                    }`}>
                      {req.qualificationName} ×{req.count}
                    </span>
                  ))}
                </div>
              )}
            </div>
            <span className={`text-xs font-medium px-2 py-0.5 rounded-full border ${burdenColors[task.burdenLevel] ?? "bg-slate-100 text-slate-500 border-slate-200"}`}>
              {burdenLabels[task.burdenLevel] ?? task.burdenLevel}
            </span>
            {isAdmin && (
              <div className="flex gap-1.5 flex-shrink-0">
                <button onClick={() => onEditTask(task)} className="text-xs text-slate-500 hover:text-slate-700 border border-slate-200 px-2 py-1 rounded-lg hover:bg-slate-50 transition-colors">{t("edit")}</button>
                {confirmDeleteTask === task.id ? (
                  <>
                    <span className="text-xs text-slate-600">{t("confirmDelete")}</span>
                    <button onClick={() => { setConfirmDeleteTask(null); onDeleteTask(task.id); }} className="text-xs text-white bg-red-500 hover:bg-red-600 px-2 py-1 rounded-lg transition-colors">{tCommon("confirm")}</button>
                    <button onClick={() => setConfirmDeleteTask(null)} className="text-xs text-slate-500 border border-slate-200 px-2 py-1 rounded-lg hover:bg-slate-50 transition-colors">{t("cancel")}</button>
                  </>
                ) : (
                  <button onClick={() => setConfirmDeleteTask(task.id)} className="text-xs text-red-500 hover:text-red-700 border border-red-100 px-2 py-1 rounded-lg hover:bg-red-50 transition-colors">{t("delete")}</button>
                )}
              </div>
            )}
          </div>
        ))}
      </div>

      {/* Create / Edit modal */}
      <Modal
        title={editingTask ? t("editTask") : t("newTaskTitle")}
        open={showTaskForm}
        onClose={onCloseForm}
        maxWidth={560}
      >
        <form onSubmit={onFormSubmit} className="space-y-4">
          {/* Name */}
          <div>
            <label className="block text-xs text-slate-500 mb-1">{t("taskName")}</label>
            <input
              type="text"
              value={taskForm.name}
              onChange={e => onFormChange({ ...taskForm, name: e.target.value })}
              placeholder={t("taskNamePlaceholder")}
              required
              className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          {/* Date range — optional */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs text-slate-500 mb-1">{t("startDefault")}</label>
              <input
                type="datetime-local"
                value={taskForm.startsAt}
                onChange={e => onFormChange({ ...taskForm, startsAt: e.target.value })}
                className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div>
              <label className="block text-xs text-slate-500 mb-1">{t("endDefault")}</label>
              <input
                type="datetime-local"
                value={taskForm.endsAt}
                onChange={e => onFormChange({ ...taskForm, endsAt: e.target.value })}
                className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
          </div>

          {/* Duration in hours + minutes */}
          <div>
            <label className="block text-xs text-slate-500 mb-1">{t("shiftDuration")}</label>
            <div className="space-y-3">
              <label className="flex items-center gap-2 text-sm text-slate-600 cursor-pointer">
                <input
                  type="checkbox"
                  checked={taskForm.shiftDurationMinutes === 1440}
                  onChange={e => onFormChange({ ...taskForm, shiftDurationMinutes: e.target.checked ? 1440 : 240 })}
                  className="rounded"
                />
                {t("fullDay")}
              </label>
              {taskForm.shiftDurationMinutes !== 1440 && (
                <>
                  <div className="flex items-center gap-2">
                    <div className="flex items-center gap-1.5">
                      <input
                        type="number"
                        min={0}
                        value={durHours}
                        onChange={e => setDuration(Number(e.target.value), durMins)}
                        className="w-16 border border-slate-200 rounded-xl px-2.5 py-2.5 text-sm text-center focus:outline-none focus:ring-2 focus:ring-blue-500"
                      />
                      <span className="text-xs text-slate-500">{t("hours")}</span>
                    </div>
                    <div className="flex items-center gap-1.5">
                      <input
                        type="number"
                        min={0}
                        max={59}
                        step={5}
                        value={durMins}
                        onChange={e => setDuration(durHours, Number(e.target.value))}
                        className="w-16 border border-slate-200 rounded-xl px-2.5 py-2.5 text-sm text-center focus:outline-none focus:ring-2 focus:ring-blue-500"
                      />
                      <span className="text-xs text-slate-500">{t("minutes")}</span>
                    </div>
                  </div>

                  {/* Sub-shifts */}
                  <SubShiftEditor
                    totalMinutes={taskForm.shiftDurationMinutes}
                    onChange={mins => onFormChange({ ...taskForm, shiftDurationMinutes: mins })}
                  />
                </>
              )}
            </div>
          </div>

          {/* Headcount + burden */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs text-slate-500 mb-1">{t("headcount")}</label>
              <input
                type="number"
                min={1}
                value={taskForm.requiredHeadcount}
                onChange={e => onFormChange({ ...taskForm, requiredHeadcount: Number(e.target.value) })}
                className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div>
              <label className="block text-xs text-slate-500 mb-1">{t("burdenLevel")}</label>
              <select
                value={taskForm.burdenLevel}
                onChange={e => onFormChange({ ...taskForm, burdenLevel: e.target.value })}
                className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                {BURDEN_OPTIONS.map(b => <option key={b} value={b}>{burdenLabels[b] ?? b}</option>)}
              </select>
            </div>
          </div>

          {/* Qualification requirements */}
          {groupQualifications.length > 0 && (
            <div>
              <label className="block text-xs text-slate-500 mb-2">דרישות כישורים</label>
              <div className="space-y-2">
                {taskForm.qualificationRequirements.map((req, idx) => (
                  <div key={idx} className="flex items-center gap-2 bg-slate-50 border border-slate-200 rounded-xl px-3 py-2">
                    {/* Qualification selector */}
                    <select
                      value={req.qualificationName}
                      onChange={e => {
                        const updated = [...taskForm.qualificationRequirements];
                        updated[idx] = { ...req, qualificationName: e.target.value };
                        onFormChange({ ...taskForm, qualificationRequirements: updated });
                      }}
                      className="flex-1 border border-slate-200 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                    >
                      {groupQualifications.map(q => (
                        <option key={q.id} value={q.name}>{q.name}</option>
                      ))}
                    </select>
                    {/* Count */}
                    <input
                      type="number"
                      min={1}
                      max={taskForm.requiredHeadcount}
                      value={req.count}
                      onChange={e => {
                        const updated = [...taskForm.qualificationRequirements];
                        updated[idx] = { ...req, count: Number(e.target.value) };
                        onFormChange({ ...taskForm, qualificationRequirements: updated });
                      }}
                      className="w-14 border border-slate-200 rounded-lg px-2 py-1.5 text-sm text-center focus:outline-none focus:ring-2 focus:ring-blue-500"
                    />
                    {/* Mandatory toggle */}
                    <button
                      type="button"
                      onClick={() => {
                        const updated = [...taskForm.qualificationRequirements];
                        updated[idx] = { ...req, mandatory: !req.mandatory };
                        onFormChange({ ...taskForm, qualificationRequirements: updated });
                      }}
                      className={`text-xs px-2.5 py-1.5 rounded-lg border font-medium transition-colors ${
                        req.mandatory
                          ? "bg-red-50 text-red-700 border-red-200"
                          : "bg-slate-100 text-slate-500 border-slate-200"
                      }`}
                    >
                      {req.mandatory ? "חובה" : "רצוי"}
                    </button>
                    {/* Remove */}
                    <button
                      type="button"
                      onClick={() => {
                        const updated = taskForm.qualificationRequirements.filter((_, i) => i !== idx);
                        onFormChange({ ...taskForm, qualificationRequirements: updated });
                      }}
                      className="text-slate-400 hover:text-red-500 transition-colors"
                    >
                      ×
                    </button>
                  </div>
                ))}
                {/* Add requirement */}
                {groupQualifications.length > taskForm.qualificationRequirements.length && (
                  <button
                    type="button"
                    onClick={() => {
                      const usedNames = taskForm.qualificationRequirements.map(r => r.qualificationName);
                      const next = groupQualifications.find(q => !usedNames.includes(q.name));
                      if (!next) return;
                      onFormChange({
                        ...taskForm,
                        qualificationRequirements: [
                          ...taskForm.qualificationRequirements,
                          { qualificationName: next.name, count: 1, mandatory: true }
                        ]
                      });
                    }}
                    className="text-sm text-blue-600 border border-blue-200 bg-blue-50 hover:bg-blue-100 px-3 py-2 rounded-xl transition-colors w-full"
                  >
                    + הוסף דרישת כישור
                  </button>
                )}
              </div>
              {taskForm.qualificationRequirements.length > 0 && (
                <p className="text-xs text-slate-400 mt-1.5">
                  {taskForm.requiredHeadcount - taskForm.qualificationRequirements.reduce((s, r) => s + r.count, 0)} מקומות פנויים לכל אחד
                </p>
              )}
            </div>
          )}

          {/* Concurrent tasks */}
          {concurrentOptions.length > 0 && (
            <div>
              <label className="block text-xs text-slate-500 mb-1">
                {t("concurrentTasks")}
                <span className="text-slate-400 ml-1">{t("concurrentTasksHint")}</span>
              </label>
              <div className="border border-slate-200 rounded-xl p-3 space-y-1.5 max-h-36 overflow-y-auto">
                {concurrentOptions.map(opt => (
                  <label key={opt.id} className="flex items-center gap-2 text-sm text-slate-700 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={taskForm.concurrentTaskIds.includes(opt.id)}
                      onChange={e => {
                        const ids = e.target.checked
                          ? [...taskForm.concurrentTaskIds, opt.id]
                          : taskForm.concurrentTaskIds.filter(id => id !== opt.id);
                        onFormChange({ ...taskForm, concurrentTaskIds: ids });
                      }}
                      className="rounded"
                    />
                    {opt.name}
                  </label>
                ))}
              </div>
            </div>
          )}

          {/* Flags */}
          <div className="flex gap-4">
            <label className="flex items-center gap-2 text-sm text-slate-600 cursor-pointer">
              <input
                type="checkbox"
                checked={taskForm.allowsDoubleShift}
                onChange={e => onFormChange({ ...taskForm, allowsDoubleShift: e.target.checked })}
                className="rounded"
              />
              {t("doubleShift")}
            </label>
            <label className="flex items-center gap-2 text-sm text-slate-600 cursor-pointer">
              <input
                type="checkbox"
                checked={taskForm.allowsOverlap}
                onChange={e => onFormChange({ ...taskForm, allowsOverlap: e.target.checked })}
                className="rounded"
              />
              {t("overlapAllowed")}
            </label>
          </div>

          {/* Daily time window */}
          <div>
            <label className="block text-xs text-slate-500 mb-1">
              {t("dailyWindow")}
            </label>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-xs text-slate-400 mb-1">{t("dailyStart")}</label>
                <input
                  type="time"
                  value={taskForm.dailyStartTime}
                  onChange={e => onFormChange({ ...taskForm, dailyStartTime: e.target.value })}
                  className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
              </div>
              <div>
                <label className="block text-xs text-slate-400 mb-1">{t("dailyEnd")}</label>
                <input
                  type="time"
                  value={taskForm.dailyEndTime}
                  onChange={e => onFormChange({ ...taskForm, dailyEndTime: e.target.value })}
                  className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
              </div>
            </div>
          </div>

          {taskError && <p className="text-sm text-red-600">{taskError}</p>}
          <div className="flex gap-2 pt-1">
            <button type="submit" disabled={taskSaving} className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
              {taskSaving ? t("saving") : editingTask ? t("update") : t("create")}
            </button>
            <button type="button" onClick={onCloseForm} className="text-sm text-slate-500 border border-slate-200 px-4 py-2.5 rounded-xl hover:bg-slate-50 transition-colors">{t("cancel")}</button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
