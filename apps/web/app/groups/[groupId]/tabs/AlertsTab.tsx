"use client";

import { getSeverityBadge } from "@/lib/utils/alertSeverity";
import Modal from "@/components/Modal";
import type { GroupAlertDto } from "../types";

interface Props {
  isAdmin: boolean;
  alerts: GroupAlertDto[];
  alertsLoading: boolean;
  alertsError: string | null;
  alertDeleteErrors: Record<string, string>;
  // Create form
  showAlertForm: boolean;
  newAlertTitle: string;
  newAlertBody: string;
  newAlertSeverity: string;
  alertSubmitting: boolean;
  alertSubmitError: string | null;
  // Edit form
  editingAlertId: string | null;
  editAlertTitle: string;
  editAlertBody: string;
  editAlertSeverity: string;
  editAlertSaving: boolean;
  editAlertError: string | null;
  // Handlers
  onOpenCreateForm: () => void;
  onCloseCreateForm: () => void;
  onCreateTitleChange: (v: string) => void;
  onCreateBodyChange: (v: string) => void;
  onCreateSeverityChange: (v: string) => void;
  onCreateSubmit: (e: React.FormEvent) => void;
  onDeleteAlert: (id: string) => void;
  onStartEdit: (alert: GroupAlertDto) => void;
  onCloseEdit: () => void;
  onEditTitleChange: (v: string) => void;
  onEditBodyChange: (v: string) => void;
  onEditSeverityChange: (v: string) => void;
  onUpdateAlert: (id: string) => void;
}

const INP = "border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500";

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

export default function AlertsTab({
  isAdmin, alerts, alertsLoading, alertsError, alertDeleteErrors,
  showAlertForm, newAlertTitle, newAlertBody, newAlertSeverity, alertSubmitting, alertSubmitError,
  editingAlertId, editAlertTitle, editAlertBody, editAlertSeverity, editAlertSaving, editAlertError,
  onOpenCreateForm, onCloseCreateForm,
  onCreateTitleChange, onCreateBodyChange, onCreateSeverityChange, onCreateSubmit,
  onDeleteAlert, onStartEdit, onCloseEdit,
  onEditTitleChange, onEditBodyChange, onEditSeverityChange, onUpdateAlert,
}: Props) {
  return (
    <div className="space-y-4">
      {isAdmin && (
        <div className="flex justify-end">
          <button
            onClick={onOpenCreateForm}
            className="flex items-center gap-2 bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-3.5 py-2 rounded-xl transition-colors"
          >
            <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
            </svg>
            התראה חדשה
          </button>
        </div>
      )}

      {alertsLoading ? (
        <LoadingSpinner />
      ) : alertsError ? (
        <p className="text-sm text-red-600 py-4">{alertsError}</p>
      ) : alerts.length === 0 ? (
        <p className="text-sm text-slate-400 py-8 text-center">אין התראות לקבוצה זו</p>
      ) : (
        <div className="space-y-3">
          {alerts.map(alert => {
            const badge = getSeverityBadge(alert.severity);
            return (
              <div key={alert.id} className={`rounded-2xl border p-4 ${badge.bg} ${badge.border}`}>
                <div className="flex items-start justify-between gap-3">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-1.5">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border ${badge.bg} ${badge.text} ${badge.border}`}>
                        {badge.label}
                      </span>
                      <span className="text-xs text-slate-400">
                        {new Date(alert.createdAt).toLocaleString("he-IL", { day: "numeric", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" })}
                      </span>
                    </div>
                    <h3 className={`text-sm font-semibold mb-1 ${badge.text}`}>{alert.title}</h3>
                    <p className="text-sm text-slate-700 whitespace-pre-wrap">{alert.body}</p>
                    <p className="text-xs text-slate-400 mt-2">פורסם על ידי: {alert.createdByDisplayName}</p>
                    {alertDeleteErrors[alert.id] && (
                      <p className="text-xs text-red-600 mt-1">{alertDeleteErrors[alert.id]}</p>
                    )}
                  </div>
                  {isAdmin && (
                    <div className="flex items-center gap-2 flex-shrink-0">
                      <button
                        onClick={() => onStartEdit(alert)}
                        className="text-xs text-blue-500 hover:text-blue-700 border border-blue-200 hover:border-blue-400 px-2.5 py-1 rounded-lg transition-colors"
                      >
                        ערוך
                      </button>
                      <button
                        onClick={() => onDeleteAlert(alert.id)}
                        className="text-xs text-red-500 hover:text-red-700 border border-red-200 hover:border-red-400 px-2.5 py-1 rounded-lg transition-colors"
                      >
                        מחק
                      </button>
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Create alert modal */}
      <Modal title="התראה חדשה" open={showAlertForm} onClose={onCloseCreateForm}>
        <form onSubmit={onCreateSubmit} className="space-y-4">
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1.5">כותרת *</label>
            <input value={newAlertTitle} onChange={e => onCreateTitleChange(e.target.value)}
              required maxLength={200} placeholder="כותרת ההתראה" className={`w-full ${INP}`} />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1.5">תוכן *</label>
            <textarea value={newAlertBody} onChange={e => onCreateBodyChange(e.target.value)}
              required maxLength={2000} rows={3} placeholder="תוכן ההתראה..."
              className={`w-full ${INP} resize-none`} />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1.5">רמת חומרה</label>
            <select value={newAlertSeverity} onChange={e => onCreateSeverityChange(e.target.value)}
              className={`w-full max-w-xs ${INP}`}>
              <option value="info">מידע</option>
              <option value="warning">אזהרה</option>
              <option value="critical">קריטי</option>
            </select>
          </div>
          {alertSubmitError && <p className="text-sm text-red-600">{alertSubmitError}</p>}
          <button type="submit" disabled={alertSubmitting}
            className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
            {alertSubmitting ? "שולח..." : "פרסם התראה"}
          </button>
        </form>
      </Modal>

      {/* Edit alert modal */}
      <Modal title="עריכת התראה" open={!!editingAlertId} onClose={onCloseEdit}>
        <div className="space-y-3">
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1">כותרת</label>
            <input value={editAlertTitle} onChange={e => onEditTitleChange(e.target.value)}
              maxLength={200}
              className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1">תוכן</label>
            <textarea value={editAlertBody} onChange={e => onEditBodyChange(e.target.value)}
              maxLength={2000} rows={3}
              className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none" />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1">רמת חומרה</label>
            <select value={editAlertSeverity} onChange={e => onEditSeverityChange(e.target.value)}
              className="border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
              <option value="info">מידע</option>
              <option value="warning">אזהרה</option>
              <option value="critical">קריטי</option>
            </select>
          </div>
          {editAlertError && <p className="text-xs text-red-600">{editAlertError}</p>}
          <div className="flex gap-2">
            <button onClick={() => editingAlertId && onUpdateAlert(editingAlertId)}
              disabled={editAlertSaving}
              className="bg-blue-500 hover:bg-blue-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors">
              {editAlertSaving ? "שומר..." : "שמור"}
            </button>
            <button onClick={onCloseEdit} className="text-xs text-slate-500 hover:text-slate-700 px-2">ביטול</button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
