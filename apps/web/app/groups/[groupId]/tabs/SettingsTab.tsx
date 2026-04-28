"use client";

import type { GroupMemberDto, DeletedGroupDto } from "../types";

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
  draftVersion: { id: string; status: string } | null;
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

export default function SettingsTab({
  isAdmin, groupId, newGroupName, renameSaving, renameError,
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
  const nonOwnerMembers = members.filter(m => !m.isOwner);

  return (
    <div className="max-w-lg space-y-5">
      {/* Rename section */}
      {isAdmin && (
        <div className="border-b border-slate-200 pb-5">
          <h3 className="text-sm font-semibold text-slate-700 mb-3">שינוי שם קבוצה</h3>
          <div className="flex gap-2 max-w-sm">
            <input
              value={newGroupName}
              onChange={e => onGroupNameChange(e.target.value)}
              className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            <button
              onClick={onRenameGroup}
              disabled={renameSaving}
              className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50"
            >
              {renameSaving ? "שומר..." : "שמור"}
            </button>
          </div>
          {renameError && <p className="text-sm text-red-600 mt-2">{renameError}</p>}
        </div>
      )}

      {/* Auto-schedule horizon */}
      <div className={isAdmin ? "border-b border-slate-200 pb-5" : ""}>
        <div className="flex items-center gap-2 mb-1">
          <label className="block text-sm font-medium text-slate-700">אופק סידור אוטומטי</label>
          <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-emerald-50 text-emerald-700 border border-emerald-200">
            🔄 אוטומטי
          </span>
        </div>
        <p className="text-xs text-slate-400 mb-3">
          המערכת תחשב סידור חדש אוטומטית כל פעם שהסידור הקיים לא מכסה את מספר הימים הזה קדימה.
        </p>
        <div className="flex items-center gap-4">
          <input
            type="range"
            min={1}
            max={90}
            value={solverHorizon}
            onChange={e => onSolverHorizonChange(Number(e.target.value))}
            className="flex-1"
          />
          <span className="text-sm font-semibold text-slate-900 w-20 text-center">{solverHorizon} ימים</span>
        </div>
        {solverHorizon > 30 && (
          <p className="mt-2 text-sm text-amber-700 bg-amber-50 border border-amber-200 rounded-xl px-3 py-2">
            ⚠️ אופק זמן ארוך מגדיל משמעותית את זמן החישוב. מעל 14 ימים — מומלץ להשתמש בזהירות.
          </p>
        )}
        <div className="flex items-center gap-3 mt-3">
          <button
            onClick={onSaveSettings}
            disabled={savingSettings}
            className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
          >
            {savingSettings ? "שומר..." : "שמור"}
          </button>
          {settingsSaved && <span className="text-sm text-emerald-600 font-medium">נשמר בהצלחה</span>}
        </div>
        {settingsError && <p className="text-sm text-red-600">{settingsError}</p>}
      </div>

      {/* Solver trigger section */}
      {isAdmin && (
        <div className="border-b border-slate-200 pb-5">
          <h3 className="text-sm font-semibold text-slate-700 mb-1">הפעל סידור</h3>
          <p className="text-xs text-slate-400 mb-3">הפעלת הסולבר תחשב סידור חדש ותיצור טיוטה לעיון.</p>
          {solverPolling ? (
            <div className="flex items-center gap-3 text-slate-600 text-sm">
              <svg className="animate-spin h-5 w-5 text-blue-400 flex-shrink-0" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
              </svg>
              הסידור מחושב...
            </div>
          ) : solverStatus === "Completed" ? (
            <div className="flex items-center gap-3">
              <div className="flex items-center gap-2 text-emerald-700 bg-emerald-50 border border-emerald-200 rounded-xl px-4 py-3 text-sm">
                <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
                </svg>
                הסידור הושלם! הטיוטה מוכנה.
              </div>
              {draftVersion && (
                <button
                  onClick={onOpenDraftModal}
                  className="bg-amber-500 hover:bg-amber-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl transition-colors"
                >
                  👁 צפה בטיוטה
                </button>
              )}
            </div>
          ) : (
            <>
              <button
                onClick={onTriggerSolver}
                disabled={solverPolling}
                className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
              >
                הפעל סידור
              </button>
              {solverError && <p className="text-sm text-red-600 mt-2">{solverError}</p>}
            </>
          )}
        </div>
      )}

      {/* Deleted groups section */}
      {isAdmin && (
        <div className="border-t border-slate-200 pt-5">
          <h3 className="text-sm font-semibold text-slate-700 mb-3">קבוצות מחוקות</h3>
          {deletedGroupsLoading ? (
            <p className="text-sm text-slate-400">טוען...</p>
          ) : deletedGroups.length === 0 ? (
            <p className="text-sm text-slate-400">אין קבוצות מחוקות</p>
          ) : (
            <div className="space-y-2">
              {deletedGroups.map(dg => (
                <div key={dg.id} className="flex items-center justify-between bg-white border border-slate-200 rounded-xl px-4 py-3">
                  <div>
                    <span className="text-sm font-medium text-slate-900">{dg.name}</span>
                    <p className="text-xs text-slate-400 mt-0.5">{new Date(dg.deletedAt).toLocaleDateString("he-IL")}</p>
                  </div>
                  <button
                    onClick={() => onRestoreGroup(dg.id)}
                    className="text-xs text-emerald-600 border border-emerald-200 hover:bg-emerald-50 px-3 py-1.5 rounded-lg transition-colors"
                  >
                    שחזר
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Ownership transfer section */}
      {isAdmin && (
        <div className="border-t border-slate-200 pt-5">
          <h3 className="text-sm font-semibold text-slate-700 mb-3">העברת בעלות</h3>
          {hasPendingTransfer ? (
            <div className="flex items-center gap-3">
              <span className="text-sm text-amber-700 bg-amber-50 border border-amber-200 px-3 py-2 rounded-xl">ממתין לאישור</span>
              <button
                onClick={onCancelTransfer}
                disabled={cancelTransferSaving}
                className="text-sm text-slate-500 hover:text-slate-700 border border-slate-200 hover:border-slate-400 px-3 py-2 rounded-xl transition-colors disabled:opacity-50"
              >
                {cancelTransferSaving ? "מבטל..." : "בטל העברה"}
              </button>
            </div>
          ) : (
            <div className="space-y-3">
              <div className="flex gap-2 max-w-sm">
                <select
                  value={transferPersonId}
                  onChange={e => onTransferPersonChange(e.target.value)}
                  className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                >
                  <option value="">בחר חבר</option>
                  {nonOwnerMembers.map(m => (
                    <option key={m.personId} value={m.personId}>
                      {m.displayName ?? m.fullName}
                    </option>
                  ))}
                </select>
                <button
                  onClick={onInitiateTransfer}
                  disabled={transferSaving || !transferPersonId}
                  className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50"
                >
                  {transferSaving ? "שולח..." : "העבר"}
                </button>
              </div>
              {transferError && <p className="text-sm text-red-600">{transferError}</p>}
            </div>
          )}
        </div>
      )}

      {/* Delete group section */}
      {isAdmin && (
        <div className="border-t border-slate-200 pt-5">
          <h3 className="text-sm font-semibold text-red-600 mb-3">מחיקת קבוצה</h3>
          {!showDeleteConfirm ? (
            <button
              onClick={() => onShowDeleteConfirm(true)}
              className="text-sm text-red-600 border border-red-200 hover:bg-red-50 px-4 py-2.5 rounded-xl transition-colors"
            >
              מחק קבוצה
            </button>
          ) : (
            <div className="bg-red-50 border border-red-200 rounded-xl p-4 space-y-3">
              <p className="text-sm text-red-700">האם אתה בטוח? ניתן לשחזר תוך 30 יום</p>
              <div className="flex gap-2">
                <button
                  onClick={onDeleteGroup}
                  disabled={deleteSaving}
                  className="bg-red-500 hover:bg-red-600 text-white text-sm font-medium px-4 py-2 rounded-xl disabled:opacity-50"
                >
                  {deleteSaving ? "מוחק..." : "כן, מחק"}
                </button>
                <button
                  onClick={() => onShowDeleteConfirm(false)}
                  className="text-sm text-slate-500 hover:text-slate-700 px-3"
                >
                  ביטול
                </button>
              </div>
              {deleteError && <p className="text-sm text-red-600">{deleteError}</p>}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
