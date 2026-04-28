"use client";

import { getSeverityBadge } from "@/lib/utils/alertSeverity";
import type { GroupAlertDto } from "@/lib/api/groups";

interface Props {
  isAdmin: boolean;
  alerts: GroupAlertDto[];
  alertsLoading: boolean;
  alertsError: string | null;
  alertDeleteErrors: Record<string, string>;
  showAlertForm: boolean;
  newAlertTitle: string;
  newAlertBody: string;
  newAlertSeverity: string;
  alertSubmitting: boolean;
  alertSubmitError: string | null;
  editingAlertId: string | null;
  editAlertTitle: string;
  editAlertBody: string;
  editAlertSeverity: string;
  editAlertSaving: boolean;
  editAlertError: string | null;
  onOpenCreateForm: () => void;
  onCloseCreateForm: () => void;
  onCreateTitleChange: (v: string) => void;
  onCreateBodyChange: (v: string) => void;
  onCreateSeverityChange: (v: string) => void;
  onCreateSubmit: (e: React.FormEvent) => void;
  onDeleteAlert: (id: string) => void;
  onStartEdit: (a: GroupAlertDto) => void;
  onCloseEdit: () => void;
  onEditTitleChange: (v: string) => void;
  onEditBodyChange: (v: string) => void;
  onEditSeverityChange: (v: string) => void;
  onUpdateAlert: (id: string) => void;
}

const SEVERITIES = ["info", "warning", "critical", "success"];

export default function AlertsTab({
  isAdmin, alerts, alertsLoading, alertsError, alertDeleteErrors,
  showAlertForm, newAlertTitle, newAlertBody, newAlertSeverity, alertSubmitting, alertSubmitError,
  editingAlertId, editAlertTitle, editAlertBody, editAlertSeverity, editAlertSaving, editAlertError,
  onOpenCreateForm, onCloseCreateForm, onCreateTitleChange, onCreateBodyChange, onCreateSeverityChange, onCreateSubmit,
  onDeleteAlert, onStartEdit, onCloseEdit, onEditTitleChange, onEditBodyChange, onEditSeverityChange, onUpdateAlert,
}: Props) {
  return (
    <div className="space-y-4">
      {isAdmin && !showAlertForm && (
        <button onClick={onOpenCreateForm} className="flex items-center gap-2 text-sm font-medium text-blue-600 border border-blue-200 bg-blue-50 hover:bg-blue-100 px-4 py-2.5 rounded-xl transition-colors">
          + צור התראה חדשה
        </button>
      )}

      {showAlertForm && (
        <form onSubmit={onCreateSubmit} className="bg-white border border-slate-200 rounded-2xl p-4 space-y-3">
          <h3 className="text-sm font-semibold text-slate-700">התראה חדשה</h3>
          <input type="text" value={newAlertTitle} onChange={e => onCreateTitleChange(e.target.value)} placeholder="כותרת *" required className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
          <textarea value={newAlertBody} onChange={e => onCreateBodyChange(e.target.value)} placeholder="תוכן *" required rows={3} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none" />
          <select value={newAlertSeverity} onChange={e => onCreateSeverityChange(e.target.value)} className="border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
            {SEVERITIES.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
          {alertSubmitError && <p className="text-sm text-red-600">{alertSubmitError}</p>}
          <div className="flex gap-2">
            <button type="submit" disabled={alertSubmitting} className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2 rounded-xl disabled:opacity-50 transition-colors">
              {alertSubmitting ? "שולח..." : "שלח"}
            </button>
            <button type="button" onClick={onCloseCreateForm} className="text-sm text-slate-500 border border-slate-200 px-4 py-2 rounded-xl hover:bg-slate-50 transition-colors">ביטול</button>
          </div>
        </form>
      )}

      {alertsLoading && <p className="text-sm text-slate-400 py-8">טוען התראות...</p>}
      {alertsError && <p className="text-sm text-red-600">{alertsError}</p>}

      {!alertsLoading && alerts.length === 0 && (
        <div className="flex flex-col items-center justify-center py-16 text-center bg-white rounded-xl border border-slate-200">
          <p className="text-slate-400 text-sm">אין התראות</p>
        </div>
      )}

      <div className="space-y-3">
        {alerts.map(a => (
          <div key={a.id} className="bg-white border border-slate-200 rounded-2xl p-4 space-y-2">
            {editingAlertId === a.id ? (
              <div className="space-y-3">
                <input type="text" value={editAlertTitle} onChange={e => onEditTitleChange(e.target.value)} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
                <textarea value={editAlertBody} onChange={e => onEditBodyChange(e.target.value)} rows={3} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none" />
                <select value={editAlertSeverity} onChange={e => onEditSeverityChange(e.target.value)} className="border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
                  {SEVERITIES.map(s => <option key={s} value={s}>{s}</option>)}
                </select>
                {editAlertError && <p className="text-sm text-red-600">{editAlertError}</p>}
                <div className="flex gap-2">
                  <button onClick={() => onUpdateAlert(a.id)} disabled={editAlertSaving} className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors">
                    {editAlertSaving ? "שומר..." : "שמור"}
                  </button>
                  <button onClick={onCloseEdit} className="text-sm text-slate-500 border border-slate-200 px-3 py-1.5 rounded-lg hover:bg-slate-50 transition-colors">ביטול</button>
                </div>
              </div>
            ) : (
              <>
                <div className="flex items-start justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border ${getSeverityBadge(a.severity).bg} ${getSeverityBadge(a.severity).text} ${getSeverityBadge(a.severity).border}`}>
                      {getSeverityBadge(a.severity).label}
                    </span>
                    <h4 className="text-sm font-semibold text-slate-900">{a.title}</h4>
                  </div>
                  {isAdmin && (
                    <div className="flex gap-1.5 flex-shrink-0">
                      <button onClick={() => onStartEdit(a)} className="text-xs text-slate-500 hover:text-slate-700 border border-slate-200 px-2 py-1 rounded-lg hover:bg-slate-50 transition-colors">ערוך</button>
                      <button onClick={() => onDeleteAlert(a.id)} className="text-xs text-red-500 hover:text-red-700 border border-red-100 px-2 py-1 rounded-lg hover:bg-red-50 transition-colors">מחק</button>
                    </div>
                  )}
                </div>
                <p className="text-sm text-slate-600">{a.body}</p>
                {alertDeleteErrors[a.id] && <p className="text-xs text-red-600">{alertDeleteErrors[a.id]}</p>}
              </>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
