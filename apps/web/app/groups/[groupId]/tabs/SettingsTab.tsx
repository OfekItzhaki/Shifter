"use client";

import type { GroupMemberDto } from "@/lib/api/groups";
import type { DeletedGroupDto } from "@/lib/api/groups";

interface DraftVersion { id: string; status: string; }

interface Props {
  isAdmin: boolean;
  groupId: string;
  newGroupName: string;
  renameSaving: boolean;
  renameError: string | null;
  solverHorizon: number;
  savingSettings: boolean;
  settingsError: string | null;
  settingsSaved: boolean;
  solverPolling: boolean;
  solverStatus: string | null;
  solverError: string | null;
  draftVersion: DraftVersion | null;
  deletedGroups: DeletedGroupDto[];
  deletedGroupsLoading: boolean;
  members: GroupMemberDto[];
  transferPersonId: string;
  transferSaving: boolean;
  transferError: string | null;
  hasPendingTransfer: boolean;
  cancelTransferSaving: boolean;
  showDeleteConfirm: boolean;
  deleteSaving: boolean;
  deleteError: string | null;
  onGroupNameChange: (v: string) => void;
  onRenameGroup: () => void;
  onSolverHorizonChange: (v: number) => void;
  onSaveSettings: () => void;
  onTriggerSolver: () => void;
  onOpenDraftModal: () => void;
  onRestoreGroup: (id: string) => void;
  onTransferPersonChange: (v: string) => void;
  onInitiateTransfer: () => void;
  onCancelTransfer: () => void;
  onShowDeleteConfirm: (v: boolean) => void;
  onDeleteGroup: () => void;
}

export default function SettingsTab({
  isAdmin, newGroupName, renameSaving, renameError,
  solverHorizon, savingSettings, settingsError, settingsSaved,
  solverPolling, solverStatus, solverError, draftVersion,
  deletedGroups, deletedGroupsLoading, members,
  transferPersonId, transferSaving, transferError, hasPendingTransfer, cancelTransferSaving,
  showDeleteConfirm, deleteSaving, deleteError,
  onGroupNameChange, onRenameGroup, onSolverHorizonChange, onSaveSettings,
  onTriggerSolver, onOpenDraftModal, onRestoreGroup,
  onTransferPersonChange, onInitiateTransfer, onCancelTransfer,
  onShowDeleteConfirm, onDeleteGroup,
}: Props) {
  if (!isAdmin) {
    return (
      <div className="flex flex-col items-center justify-center py-16 text-center bg-white rounded-xl border border-slate-200">
        <p className="text-slate-400 text-sm">הגדרות זמינות למנהלים בלבד</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Rename */}
      <Section title="שם הקבוצה">
        <div className="flex gap-2">
          <input
            type="text"
            value={newGroupName}
            onChange={e => onGroupNameChange(e.target.value)}
            className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          <button onClick={onRenameGroup} disabled={renameSaving} className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
            {renameSaving ? "שומר..." : "שמור"}
          </button>
        </div>
        {renameError && <p className="text-sm text-red-600 mt-2">{renameError}</p>}
      </Section>

      {/* Solver horizon */}
      <Section title="אופק תכנון">
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <span className="text-sm text-slate-600">ימים קדימה: <strong>{solverHorizon}</strong></span>
          </div>
          <input
            type="range"
            min={3}
            max={30}
            value={solverHorizon}
            onChange={e => onSolverHorizonChange(Number(e.target.value))}
            className="w-full accent-blue-500"
          />
          <button onClick={onSaveSettings} disabled={savingSettings} className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
            {savingSettings ? "שומר..." : "שמור הגדרות"}
          </button>
          {settingsError && <p className="text-sm text-red-600">{settingsError}</p>}
          {settingsSaved && <p className="text-sm text-emerald-600">ההגדרות נשמרו ✓</p>}
        </div>
      </Section>

      {/* Trigger solver */}
      <Section title="הפעלת סידור">
        <div className="space-y-3">
          {draftVersion && (
            <div className="flex items-center gap-2 bg-amber-50 border border-amber-200 rounded-xl px-4 py-3">
              <span className="text-sm text-amber-800">יש טיוטה ממתינה לפרסום</span>
              <button onClick={onOpenDraftModal} className="text-xs text-amber-700 border border-amber-300 hover:bg-amber-100 px-3 py-1.5 rounded-lg transition-colors font-medium">צפה בטיוטה</button>
            </div>
          )}
          <button
            onClick={onTriggerSolver}
            disabled={solverPolling}
            className="flex items-center gap-2 bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
          >
            {solverPolling ? (
              <>
                <svg className="animate-spin h-4 w-4" fill="none" viewBox="0 0 24 24"><circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" /><path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" /></svg>
                מחשב סידור...
              </>
            ) : "הפעל סידור"}
          </button>
          {solverStatus && <p className="text-sm text-slate-600">סטטוס: {solverStatus}</p>}
          {solverError && <p className="text-sm text-red-600">{solverError}</p>}
        </div>
      </Section>

      {/* Ownership transfer */}
      <Section title="העברת בעלות">
        {hasPendingTransfer ? (
          <div className="space-y-2">
            <p className="text-sm text-amber-700 bg-amber-50 border border-amber-200 rounded-xl px-4 py-3">יש בקשת העברה ממתינה</p>
            <button onClick={onCancelTransfer} disabled={cancelTransferSaving} className="text-sm text-red-600 border border-red-200 hover:bg-red-50 px-4 py-2 rounded-xl disabled:opacity-50 transition-colors">
              {cancelTransferSaving ? "מבטל..." : "בטל העברה"}
            </button>
          </div>
        ) : (
          <div className="flex gap-2">
            <select
              value={transferPersonId}
              onChange={e => onTransferPersonChange(e.target.value)}
              className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="">בחר חבר...</option>
              {members.map(m => (
                <option key={m.personId} value={m.personId}>{m.displayName ?? m.fullName}</option>
              ))}
            </select>
            <button onClick={onInitiateTransfer} disabled={transferSaving || !transferPersonId} className="bg-amber-500 hover:bg-amber-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
              {transferSaving ? "שולח..." : "העבר"}
            </button>
          </div>
        )}
        {transferError && <p className="text-sm text-red-600 mt-2">{transferError}</p>}
      </Section>

      {/* Restore deleted groups */}
      {deletedGroups.length > 0 && (
        <Section title="קבוצות מחוקות">
          {deletedGroupsLoading ? (
            <p className="text-sm text-slate-400">טוען...</p>
          ) : (
            <div className="space-y-2">
              {deletedGroups.map(g => (
                <div key={g.id} className="flex items-center justify-between bg-white border border-slate-200 rounded-xl px-4 py-3">
                  <span className="text-sm text-slate-700">{g.name}</span>
                  <button onClick={() => onRestoreGroup(g.id)} className="text-xs text-blue-600 border border-blue-200 hover:bg-blue-50 px-3 py-1.5 rounded-lg transition-colors">שחזר</button>
                </div>
              ))}
            </div>
          )}
        </Section>
      )}

      {/* Delete group */}
      <Section title="מחיקת קבוצה">
        {showDeleteConfirm ? (
          <div className="bg-red-50 border border-red-200 rounded-xl p-4 space-y-3">
            <p className="text-sm text-red-700">האם אתה בטוח? הקבוצה תועבר לארכיון ותוכל לשחזרה מאוחר יותר.</p>
            <div className="flex gap-2">
              <button onClick={onDeleteGroup} disabled={deleteSaving} className="bg-red-500 hover:bg-red-600 text-white text-sm font-medium px-4 py-2 rounded-xl disabled:opacity-50 transition-colors">
                {deleteSaving ? "מוחק..." : "כן, מחק"}
              </button>
              <button onClick={() => onShowDeleteConfirm(false)} className="text-sm text-slate-500 border border-slate-200 px-4 py-2 rounded-xl hover:bg-slate-50 transition-colors">ביטול</button>
            </div>
            {deleteError && <p className="text-sm text-red-600">{deleteError}</p>}
          </div>
        ) : (
          <button onClick={() => onShowDeleteConfirm(true)} className="text-sm text-red-600 border border-red-200 hover:bg-red-50 px-4 py-2.5 rounded-xl transition-colors">
            מחק קבוצה
          </button>
        )}
      </Section>
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="bg-white border border-slate-200 rounded-2xl p-5 space-y-3">
      <h3 className="text-sm font-semibold text-slate-700">{title}</h3>
      {children}
    </div>
  );
}
