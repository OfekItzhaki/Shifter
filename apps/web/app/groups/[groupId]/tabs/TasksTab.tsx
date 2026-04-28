"use client";

import type { GroupTaskDto } from "@/lib/api/tasks";
import { burdenLabels, burdenColors } from "../types";

const BURDEN_OPTIONS = ["favorable", "neutral", "disliked", "hated"];

interface TaskForm {
  name: string; startsAt: string; endsAt: string;
  shiftDurationMinutes: number; requiredHeadcount: number;
  burdenLevel: string; allowsDoubleShift: boolean; allowsOverlap: boolean;
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

export default function TasksTab({
  isAdmin, groupTasks, groupTasksLoading, showTaskForm, editingTask, taskForm,
  taskSaving, taskError, onOpenCreate, onCloseForm, onFormChange, onFormSubmit, onEditTask, onDeleteTask,
}: Props) {
  return (
    <div className="space-y-4">
      {isAdmin && !showTaskForm && (
        <button onClick={onOpenCreate} className="flex items-center gap-2 text-sm font-medium text-blue-600 border border-blue-200 bg-blue-50 hover:bg-blue-100 px-4 py-2.5 rounded-xl transition-colors">
          + משימה חדשה
        </button>
      )}

      {showTaskForm && (
        <form onSubmit={onFormSubmit} className="bg-white border border-slate-200 rounded-2xl p-4 space-y-3">
          <h3 className="text-sm font-semibold text-slate-700">{editingTask ? "עריכת משימה" : "משימה חדשה"}</h3>
          <input type="text" value={taskForm.name} onChange={e => onFormChange({ ...taskForm, name: e.target.value })} placeholder="שם המשימה *" required className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs text-slate-500 mb-1">התחלה</label>
              <input type="datetime-local" value={taskForm.startsAt} onChange={e => onFormChange({ ...taskForm, startsAt: e.target.value })} required className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
            <div>
              <label className="block text-xs text-slate-500 mb-1">סיום</label>
              <input type="datetime-local" value={taskForm.endsAt} onChange={e => onFormChange({ ...taskForm, endsAt: e.target.value })} required className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs text-slate-500 mb-1">משך משמרת (דקות)</label>
              <input type="number" min={1} value={taskForm.shiftDurationMinutes} onChange={e => onFormChange({ ...taskForm, shiftDurationMinutes: Number(e.target.value) })} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
            <div>
              <label className="block text-xs text-slate-500 mb-1">כמות נדרשת</label>
              <input type="number" min={1} value={taskForm.requiredHeadcount} onChange={e => onFormChange({ ...taskForm, requiredHeadcount: Number(e.target.value) })} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
          </div>
          <div>
            <label className="block text-xs text-slate-500 mb-1">רמת עומס</label>
            <select value={taskForm.burdenLevel} onChange={e => onFormChange({ ...taskForm, burdenLevel: e.target.value })} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
              {BURDEN_OPTIONS.map(b => <option key={b} value={b}>{burdenLabels[b] ?? b}</option>)}
            </select>
          </div>
          <div className="flex gap-4">
            <label className="flex items-center gap-2 text-sm text-slate-600">
              <input type="checkbox" checked={taskForm.allowsDoubleShift} onChange={e => onFormChange({ ...taskForm, allowsDoubleShift: e.target.checked })} className="rounded" />
              משמרת כפולה
            </label>
            <label className="flex items-center gap-2 text-sm text-slate-600">
              <input type="checkbox" checked={taskForm.allowsOverlap} onChange={e => onFormChange({ ...taskForm, allowsOverlap: e.target.checked })} className="rounded" />
              חפיפה מותרת
            </label>
          </div>
          {taskError && <p className="text-sm text-red-600">{taskError}</p>}
          <div className="flex gap-2">
            <button type="submit" disabled={taskSaving} className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
              {taskSaving ? "שומר..." : editingTask ? "עדכן" : "צור"}
            </button>
            <button type="button" onClick={onCloseForm} className="text-sm text-slate-500 border border-slate-200 px-4 py-2.5 rounded-xl hover:bg-slate-50 transition-colors">ביטול</button>
          </div>
        </form>
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
                {t.requiredHeadcount} אנשים · {t.shiftDurationMinutes} דק׳
              </p>
            </div>
            <span className={`text-xs font-medium px-2 py-0.5 rounded-full border ${burdenColors[t.burdenLevel] ?? "bg-slate-100 text-slate-500 border-slate-200"}`}>
              {burdenLabels[t.burdenLevel] ?? t.burdenLevel}
            </span>
            {isAdmin && (
              <div className="flex gap-1.5 flex-shrink-0">
                <button onClick={() => onEditTask(t)} className="text-xs text-slate-500 hover:text-slate-700 border border-slate-200 px-2 py-1 rounded-lg hover:bg-slate-50 transition-colors">ערוך</button>
                <button onClick={() => onDeleteTask(t.id)} className="text-xs text-red-500 hover:text-red-700 border border-red-100 px-2 py-1 rounded-lg hover:bg-red-50 transition-colors">מחק</button>
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
