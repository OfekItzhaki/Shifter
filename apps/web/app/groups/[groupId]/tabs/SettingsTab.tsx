"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import type { GroupMemberDto } from "@/lib/api/groups";

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
  members: GroupMemberDto[];
  transferPersonId: string;
  transferSaving: boolean;
  transferError: string | null;
  hasPendingTransfer: boolean;
  cancelTransferSaving: boolean;
  showDeleteConfirm: boolean;
  deleteSaving: boolean;
  deleteError: string | null;
  // Roles
  groupRoles?: never;
  groupRolesLoading?: never;
  onCreateRole?: never;
  onUpdateRole?: never;
  onDeactivateRole?: never;
  onGroupNameChange: (v: string) => void;
  onRenameGroup: () => void;
  onSolverHorizonChange: (v: number) => void;
  onSaveSettings: () => void;
  onTriggerSolver: (startTime?: string) => void;
  onOpenDraftModal: () => void;
  onRestoreGroup?: never;
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
  members,
  transferPersonId, transferSaving, transferError, hasPendingTransfer, cancelTransferSaving,
  showDeleteConfirm, deleteSaving, deleteError,
  onGroupNameChange, onRenameGroup, onSolverHorizonChange, onSaveSettings,
  onTriggerSolver, onOpenDraftModal,
  onTransferPersonChange, onInitiateTransfer, onCancelTransfer,
  onShowDeleteConfirm, onDeleteGroup,
}: Props) {
  // Solver start time — defaults to now  const [solverStartTime, setSolverStartTime] = useState(() => {
    const d = new Date();
    const pad = (n: number) => String(n).padStart(2, "0");
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  });

  const t = useTranslations("groups.settings_tab");
  const tCommon = useTranslations("common");

  if (!isAdmin) {
    return (
      <div className="flex flex-col items-center justify-center py-16 text-center bg-white rounded-xl border border-slate-200">
        <p className="text-slate-400 text-sm">{t("adminOnly")}</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Rename */}
      <Section title={t("groupName")}>
        <div className="flex gap-2">
          <input
            type="text"
            value={newGroupName}
            onChange={e => onGroupNameChange(e.target.value)}
            className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          <button onClick={onRenameGroup} disabled={renameSaving} className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
            {renameSaving ? t("saving") : t("save")}
          </button>
        </div>
        {renameError && <p className="text-sm text-red-600 mt-2">{renameError}</p>}
      </Section>

      {/* Solver horizon */}
      <Section title={t("planningHorizon")}>
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <span className="text-sm text-slate-600">{t("daysAhead")}: <strong>{solverHorizon}</strong></span>
          </div>
          <input
            type="range"
            min={3}
            max={7}
            value={solverHorizon}
            onChange={e => onSolverHorizonChange(Number(e.target.value))}
            className="w-full accent-blue-500"
          />
          <button onClick={onSaveSettings} disabled={savingSettings} className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
            {savingSettings ? t("saving") : t("saveSettings")}
          </button>
          {settingsError && <p className="text-sm text-red-600">{settingsError}</p>}
          {settingsSaved && <p className="text-sm text-emerald-600">{t("save")} ✓</p>}
        </div>
      </Section>

      {/* Trigger solver */}
      <Section title={t("runSchedule")}>
        <div className="space-y-3">
          {draftVersion && (
            <div className="flex items-center gap-2 bg-amber-50 border border-amber-200 rounded-xl px-4 py-3">
              <span className="text-sm text-amber-800">{t("draftPending")}</span>
              <button onClick={onOpenDraftModal} className="text-xs text-amber-700 border border-amber-300 hover:bg-amber-100 px-3 py-1.5 rounded-lg transition-colors font-medium">{t("viewDraft")}</button>
            </div>
          )}
          {/* Start time picker */}
          <div className="flex items-center gap-2">
            <label className="text-sm text-slate-600 whitespace-nowrap">{t("startFrom")}</label>
            <input
              type="datetime-local"
              value={solverStartTime}
              onChange={e => setSolverStartTime(e.target.value)}
              className="flex-1 border border-slate-200 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <button
            onClick={() => onTriggerSolver(solverStartTime ? new Date(solverStartTime).toISOString() : undefined)}
            disabled={solverPolling}
            className="flex items-center gap-2 bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
          >
            {solverPolling ? (
              <>
                <svg className="animate-spin h-4 w-4" fill="none" viewBox="0 0 24 24"><circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" /><path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" /></svg>
                {t("running")}
              </>
            ) : t("runSchedule")}
          </button>
          {solverStatus && (
            <p className={`text-sm ${solverStatus === "Completed" ? "text-emerald-600" : solverStatus === "Failed" ? "text-red-600" : "text-slate-600"}`}>
              {solverStatus}
            </p>
          )}
          {solverError && (
            <div className="bg-red-50 border border-red-200 rounded-xl px-4 py-3 text-sm text-red-700">
              {solverError}
            </div>
          )}
        </div>
      </Section>

      {/* Ownership transfer */}      <Section title={t("ownershipTransfer")}>
        {hasPendingTransfer ? (
          <div className="space-y-2">
            <p className="text-sm text-amber-700 bg-amber-50 border border-amber-200 rounded-xl px-4 py-3">{t("pendingTransfer")}</p>
            <button onClick={onCancelTransfer} disabled={cancelTransferSaving} className="text-sm text-red-600 border border-red-200 hover:bg-red-50 px-4 py-2 rounded-xl disabled:opacity-50 transition-colors">
              {cancelTransferSaving ? t("transferring") : t("cancelTransfer")}
            </button>
          </div>
        ) : (
          <div className="flex gap-2">
            <select
              value={transferPersonId}
              onChange={e => onTransferPersonChange(e.target.value)}
              className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="">{t("selectMember")}</option>
              {members.map(m => (
                <option key={m.personId} value={m.personId}>{m.displayName ?? m.fullName}</option>
              ))}
            </select>
            <button onClick={onInitiateTransfer} disabled={transferSaving || !transferPersonId} className="bg-amber-500 hover:bg-amber-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
              {transferSaving ? t("transferring") : t("transfer")}
            </button>
          </div>
        )}
        {transferError && <p className="text-sm text-red-600 mt-2">{transferError}</p>}
      </Section>

      {/* Delete group */}
      <Section title={t("deleteGroup")}>
        {showDeleteConfirm ? (
          <div className="bg-red-50 border border-red-200 rounded-xl p-4 space-y-3">
            <p className="text-sm text-red-700">{t("deleteConfirm")}</p>
            <div className="flex gap-2">
              <button onClick={onDeleteGroup} disabled={deleteSaving} className="bg-red-500 hover:bg-red-600 text-white text-sm font-medium px-4 py-2 rounded-xl disabled:opacity-50 transition-colors">
                {deleteSaving ? t("deleting") : t("yesDelete")}
              </button>
              <button onClick={() => onShowDeleteConfirm(false)} className="text-sm text-slate-500 border border-slate-200 px-4 py-2 rounded-xl hover:bg-slate-50 transition-colors">{t("cancel")}</button>
            </div>
            {deleteError && <p className="text-sm text-red-600">{deleteError}</p>}
          </div>
        ) : (
          <button onClick={() => onShowDeleteConfirm(true)} className="text-sm text-red-600 border border-red-200 hover:bg-red-50 px-4 py-2.5 rounded-xl transition-colors">
            {t("deleteGroup")}
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
