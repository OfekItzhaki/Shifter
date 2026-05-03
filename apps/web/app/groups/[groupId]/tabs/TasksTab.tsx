"use client";

import Modal from "@/components/Modal";
import { useState } from "react";
import type { GroupTaskDto } from "@/lib/api/tasks";
import { burdenLabels, burdenColors } from "../types";

const BURDEN_OPTIONS = ["favorable", "neutral", "disliked", "hated"];

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
}

interface Props {
  isAdmin: boolean;
  groupTasks: GroupTaskDto[];
  groupTasksLoading: boolean;
  showTaskForm: boolean;
  editingTask: GroupTaskDto | null;
  taskForm: TaskForm;
  taskSaving: boolean;
  taskError: string | null;
  onOpenCreate: () => void;
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

export default function TasksTab({
  isAdmin, groupTasks, groupTasksLoading, showTaskForm, editingTask, taskForm,
  taskSaving, taskError, onOpenCreate, onCloseForm, onFormChange, onFormSubmit, onEditTask, onDeleteTask,
}: Props) {
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
        <button onClick={onOpenCreate} className="flex items-center gap-2 text-sm font-medium text-blue-600 border border-blue-200 bg-blue-50 hover:bg-blue-100 px-4 py-2.5 rounded-xl transition-colors">
          + משימה חדשה
        </button>
      )}

      {groupTasksLoading && <p className="text-sm text-slate-400 py-8">טוען משימות...</p>}

      {!groupTasksLoading && groupTasks.length === 0 && (
        <div className="flex flex-col items-center justify-center py-16 text-center bg-white rounded-xl border border-slate-200">
          <p className="text-slate-400 text-sm">אין משימות מוגדרות</p>
        </div>
      )}

      <div className="space-y-2">
        {groupTasks.map(t => (
          <div key={t.id} className="flex items-center gap-3 bg-white border border-slate-200 rounded-xl px-4 py-3">
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium text-slate-900 truncate">{t.name}</p>
              <p className="text-xs text-slate-400">
                {t.requiredHeadcount} אנשים · {minutesToHM(t.shiftDurationMinutes).hours}ש׳ {minutesToHM(t.shiftDurationMinutes).mins > 0 ? `${minutesToHM(t.shiftDurationMinutes).mins}ד׳` : ""}
              </p>
            </div>
            <span className={`text-xs font-medium px-2 py-0.5 rounded-full border ${burdenColors[t.burdenLevel] ?? "bg-slate-100 text-slate-500 border-slate-200"}`}>
              {burdenLabels[t.burdenLevel] ?? t.burdenLevel}
            </span>
            {isAdmin && (
              <div className="flex gap-1.5 flex-shrink-0">
                <button onClick={() => onEditTask(t)} className="text-xs text-slate-500 hover:text-slate-700 border border-slate-200 px-2 py-1 rounded-lg hover:bg-slate-50 transition-colors">ערוך</button>
                {confirmDeleteTask === t.id ? (
                  <>
                    <span className="text-xs text-slate-600">למחוק?</span>
                    <button onClick={() => { setConfirmDeleteTask(null); onDeleteTask(t.id); }} className="text-xs text-white bg-red-500 hover:bg-red-600 px-2 py-1 rounded-lg transition-colors">אישור</button>
                    <button onClick={() => setConfirmDeleteTask(null)} className="text-xs text-slate-500 border border-slate-200 px-2 py-1 rounded-lg hover:bg-slate-50 transition-colors">ביטול</button>
                  </>
                ) : (
                  <button onClick={() => setConfirmDeleteTask(t.id)} className="text-xs text-red-500 hover:text-red-700 border border-red-100 px-2 py-1 rounded-lg hover:bg-red-50 transition-colors">מחק</button>
                )}
              </div>
            )}
          </div>
        ))}
      </div>

      {/* Create / Edit modal */}
      <Modal
        title={editingTask ? "עריכת משימה" : "משימה חדשה"}
        open={showTaskForm}
        onClose={onCloseForm}
        maxWidth={560}
      >
        <form onSubmit={onFormSubmit} className="space-y-4">
          {/* Name */}
          <div>
            <label className="block text-xs text-slate-500 mb-1">שם המשימה *</label>
            <input
              type="text"
              value={taskForm.name}
              onChange={e => onFormChange({ ...taskForm, name: e.target.value })}
              placeholder="שם המשימה"
              required
              className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          {/* Date range — optional */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs text-slate-500 mb-1">התחלה <span className="text-slate-400">(ברירת מחדל: היום)</span></label>
              <input
                type="datetime-local"
                value={taskForm.startsAt}
                onChange={e => onFormChange({ ...taskForm, startsAt: e.target.value })}
                className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div>
              <label className="block text-xs text-slate-500 mb-1">סיום <span className="text-slate-400">(ריק = 90 יום קדימה)</span></label>
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
            <label className="block text-xs text-slate-500 mb-1">משך משמרת</label>
            <div className="space-y-2">
              <label className="flex items-center gap-2 text-sm text-slate-600 cursor-pointer">
                <input
                  type="checkbox"
                  checked={taskForm.shiftDurationMinutes === 1440}
                  onChange={e => onFormChange({ ...taskForm, shiftDurationMinutes: e.target.checked ? 1440 : 240 })}
                  className="rounded"
                />
                יום מלא (24 שעות)
              </label>
              {taskForm.shiftDurationMinutes !== 1440 && (
                <div className="flex items-center gap-2">
                  <div className="flex items-center gap-1.5">
                    <input
                      type="number"
                      min={0}
                      value={durHours}
                      onChange={e => setDuration(Number(e.target.value), durMins)}
                      className="w-16 border border-slate-200 rounded-xl px-2.5 py-2.5 text-sm text-center focus:outline-none focus:ring-2 focus:ring-blue-500"
                    />
                    <span className="text-xs text-slate-500">שעות</span>
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
                    <span className="text-xs text-slate-500">דקות</span>
                  </div>
                </div>
              )}
            </div>
          </div>

          {/* Headcount + burden */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs text-slate-500 mb-1">כמות נדרשת</label>
              <input
                type="number"
                min={1}
                value={taskForm.requiredHeadcount}
                onChange={e => onFormChange({ ...taskForm, requiredHeadcount: Number(e.target.value) })}
                className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div>
              <label className="block text-xs text-slate-500 mb-1">רמת עומס</label>
              <select
                value={taskForm.burdenLevel}
                onChange={e => onFormChange({ ...taskForm, burdenLevel: e.target.value })}
                className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                {BURDEN_OPTIONS.map(b => <option key={b} value={b}>{burdenLabels[b] ?? b}</option>)}
              </select>
            </div>
          </div>

          {/* Concurrent tasks — which other tasks can be done simultaneously */}
          {concurrentOptions.length > 0 && (
            <div>
              <label className="block text-xs text-slate-500 mb-1">
                משימות שניתן לבצע במקביל
                <span className="text-slate-400 mr-1">(בחר אחת או יותר)</span>
              </label>
              <div className="border border-slate-200 rounded-xl p-3 space-y-1.5 max-h-36 overflow-y-auto">
                {concurrentOptions.map(t => (
                  <label key={t.id} className="flex items-center gap-2 text-sm text-slate-700 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={taskForm.concurrentTaskIds.includes(t.id)}
                      onChange={e => {
                        const ids = e.target.checked
                          ? [...taskForm.concurrentTaskIds, t.id]
                          : taskForm.concurrentTaskIds.filter(id => id !== t.id);
                        onFormChange({ ...taskForm, concurrentTaskIds: ids });
                      }}
                      className="rounded"
                    />
                    {t.name}
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
              משמרת כפולה
            </label>
            <label className="flex items-center gap-2 text-sm text-slate-600 cursor-pointer">
              <input
                type="checkbox"
                checked={taskForm.allowsOverlap}
                onChange={e => onFormChange({ ...taskForm, allowsOverlap: e.target.checked })}
                className="rounded"
              />
              חפיפה מותרת
            </label>
          </div>

          {/* Daily time window */}
          <div>
            <label className="block text-xs text-slate-500 mb-1">
              חלון שעות יומי <span className="text-slate-400">(ריק = 24/7)</span>
            </label>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-xs text-slate-400 mb-1">שעת התחלה</label>
                <input
                  type="time"
                  value={taskForm.dailyStartTime}
                  onChange={e => onFormChange({ ...taskForm, dailyStartTime: e.target.value })}
                  className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
              </div>
              <div>
                <label className="block text-xs text-slate-400 mb-1">שעת סיום</label>
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
              {taskSaving ? "שומר..." : editingTask ? "עדכן" : "צור"}
            </button>
            <button type="button" onClick={onCloseForm} className="text-sm text-slate-500 border border-slate-200 px-4 py-2.5 rounded-xl hover:bg-slate-50 transition-colors">ביטול</button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
