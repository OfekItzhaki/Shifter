"use client";

import Modal from "@/components/Modal";
import { burdenLabels, burdenColors } from "../types";
import type { GroupTaskDto } from "../types";

interface TaskForm {
  name: string;
  startsAt: string;
  endsAt: string;
  shiftDurationMinutes: number;
  requiredHeadcount: number;
  burdenLevel: string;
  allowsDoubleShift: boolean;
  allowsOverlap: boolean;
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
  onFormChange: (form: TaskForm) => void;
  onFormSubmit: (e: React.FormEvent) => void;
  onEditTask: (task: GroupTaskDto) => void;
  onDeleteTask: (id: string) => void;
}

const INP = "border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500";

const BURDEN_OPTIONS = [
  { value: "favorable", label: "נוח" },
  { value: "neutral", label: "ניטרלי" },
  { value: "disliked", label: "לא אהוב" },
  { value: "hated", label: "שנוא" },
];

function formatDuration(minutes: number): string {
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return `${h > 0 ? `${h}ש'` : ""}${m > 0 ? ` ${m}ד'` : ""}`.trim();
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

export default function TasksTab({
  isAdmin, groupTasks, groupTasksLoading,
  showTaskForm, editingTask, taskForm, taskSaving, taskError,
  onOpenCreate, onCloseForm, onFormChange, onFormSubmit, onEditTask, onDeleteTask,
}: Props) {
  if (groupTasksLoading) return <LoadingSpinner />;

  return (
    <div className="space-y-4">
      {isAdmin && (
        <div className="flex justify-end">
          <button
            onClick={onOpenCreate}
            className="flex items-center gap-2 bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-3.5 py-2 rounded-xl transition-colors"
          >
            <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
            </svg>
            הוסף משימה
          </button>
        </div>
      )}

      <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-slate-100 bg-slate-50/80">
              <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">שם</th>
              <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">חלון זמן</th>
              <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">משך</th>
              <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">כוח אדם</th>
              <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">עומס</th>
              {isAdmin && <th className="px-4 py-3" />}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {groupTasks.map(t => (
              <tr key={t.id} className="hover:bg-slate-50/60">
                <td className="px-4 py-3.5 font-medium text-slate-900">{t.name}</td>
                <td className="px-4 py-3.5 text-slate-500 text-xs tabular-nums">
                  {new Date(t.startsAt).toLocaleString("he-IL", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" })}
                  <span className="mx-1 text-slate-300">–</span>
                  {new Date(t.endsAt).toLocaleString("he-IL", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" })}
                </td>
                <td className="px-4 py-3.5 text-slate-500 text-xs">{formatDuration(t.shiftDurationMinutes)}</td>
                <td className="px-4 py-3.5 text-slate-500">{t.requiredHeadcount}</td>
                <td className="px-4 py-3.5">
                  <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border ${burdenColors[t.burdenLevel] ?? "bg-slate-100 text-slate-600 border-slate-200"}`}>
                    {burdenLabels[t.burdenLevel] ?? t.burdenLevel}
                  </span>
                </td>
                {isAdmin && (
                  <td className="px-4 py-3.5">
                    <div className="flex items-center gap-2">
                      <button
                        onClick={() => onEditTask(t)}
                        className="text-xs text-blue-500 hover:text-blue-700 border border-blue-200 hover:border-blue-400 px-2.5 py-1 rounded-lg transition-colors"
                      >
                        ערוך
                      </button>
                      <button
                        onClick={() => onDeleteTask(t.id)}
                        className="text-xs text-red-500 hover:text-red-700 border border-red-200 hover:border-red-400 px-2.5 py-1 rounded-lg transition-colors"
                      >
                        מחק
                      </button>
                    </div>
                  </td>
                )}
              </tr>
            ))}
            {groupTasks.length === 0 && (
              <tr>
                <td colSpan={isAdmin ? 6 : 5} className="px-4 py-12 text-center text-slate-400 text-sm">
                  אין משימות לקבוצה זו
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Task create/edit modal */}
      <Modal
        title={editingTask ? "עריכת משימה" : "משימה חדשה"}
        open={showTaskForm}
        onClose={onCloseForm}
      >
        <form onSubmit={onFormSubmit} className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div className="col-span-2">
              <label className="block text-xs font-medium text-slate-500 mb-1.5">שם *</label>
              <input
                value={taskForm.name}
                onChange={e => onFormChange({ ...taskForm, name: e.target.value })}
                required className={`w-full ${INP}`} placeholder="שם המשימה"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-500 mb-1.5">התחלה *</label>
              <input type="datetime-local" value={taskForm.startsAt}
                onChange={e => onFormChange({ ...taskForm, startsAt: e.target.value })}
                required className={`w-full ${INP}`} />
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-500 mb-1.5">סיום *</label>
              <input type="datetime-local" value={taskForm.endsAt}
                onChange={e => onFormChange({ ...taskForm, endsAt: e.target.value })}
                required className={`w-full ${INP}`} />
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-500 mb-1.5">משך משמרת *</label>
              <div className="flex items-center gap-2">
                <input type="number" min={15} step={15} value={taskForm.shiftDurationMinutes}
                  onChange={e => onFormChange({ ...taskForm, shiftDurationMinutes: Number(e.target.value) })}
                  required className={`w-24 ${INP}`} />
                <span className="text-xs text-slate-500">דקות</span>
                <span className="text-xs text-slate-400">({formatDuration(taskForm.shiftDurationMinutes)})</span>
              </div>
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-500 mb-1.5">כוח אדם נדרש *</label>
              <input type="number" min={1} value={taskForm.requiredHeadcount}
                onChange={e => onFormChange({ ...taskForm, requiredHeadcount: Number(e.target.value) })}
                required className={`w-full ${INP}`} />
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-500 mb-1.5">רמת עומס</label>
              <select value={taskForm.burdenLevel}
                onChange={e => onFormChange({ ...taskForm, burdenLevel: e.target.value })}
                className={`w-full ${INP}`}>
                {BURDEN_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
              </select>
            </div>
          </div>
          <div className="flex gap-6">
            <label className="flex items-center gap-2.5 text-sm text-slate-700 cursor-pointer">
              <input type="checkbox" checked={taskForm.allowsDoubleShift}
                onChange={e => onFormChange({ ...taskForm, allowsDoubleShift: e.target.checked })}
                className="w-4 h-4 rounded" />
              מאפשר משמרת כפולה
            </label>
            <label className="flex items-center gap-2.5 text-sm text-slate-700 cursor-pointer">
              <input type="checkbox" checked={taskForm.allowsOverlap}
                onChange={e => onFormChange({ ...taskForm, allowsOverlap: e.target.checked })}
                className="w-4 h-4 rounded" />
              מאפשר חפיפה
            </label>
          </div>
          {taskError && <p className="text-sm text-red-600">{taskError}</p>}
          <div className="flex gap-2">
            <button type="submit" disabled={taskSaving}
              className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
              {taskSaving ? "שומר..." : "שמור"}
            </button>
            <button type="button" onClick={onCloseForm}
              className="text-sm text-slate-500 hover:text-slate-700 px-3">ביטול</button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
