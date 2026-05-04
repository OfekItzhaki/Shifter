"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import type { GroupMemberDto, GroupRoleDto } from "@/lib/api/groups";

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
  groupRoles: GroupRoleDto[];
  groupRolesLoading: boolean;
  onCreateRole: (name: string, description: string | null, permissionLevel: string) => Promise<void>;
  onUpdateRole: (roleId: string, name: string, description: string | null, permissionLevel: string) => Promise<void>;
  onDeactivateRole: (roleId: string) => Promise<void>;
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
  groupRoles, groupRolesLoading, onCreateRole, onUpdateRole, onDeactivateRole,
  transferPersonId, transferSaving, transferError, hasPendingTransfer, cancelTransferSaving,
  showDeleteConfirm, deleteSaving, deleteError,
  onGroupNameChange, onRenameGroup, onSolverHorizonChange, onSaveSettings,
  onTriggerSolver, onOpenDraftModal,
  onTransferPersonChange, onInitiateTransfer, onCancelTransfer,
  onShowDeleteConfirm, onDeleteGroup,
}: Props) {
  // Roles form state
  const [newRoleName, setNewRoleName] = useState("");
  const [newRoleDesc, setNewRoleDesc] = useState("");
  const [newRolePermLevel, setNewRolePermLevel] = useState("view");
  const [roleFormSaving, setRoleFormSaving] = useState(false);
  const [roleFormError, setRoleFormError] = useState<string | null>(null);
  const [editingRoleId, setEditingRoleId] = useState<string | null>(null);
  const [editRoleName, setEditRoleName] = useState("");
  const [editRoleDesc, setEditRoleDesc] = useState("");
  const [editRolePermLevel, setEditRolePermLevel] = useState("view");
  const [editRoleSaving, setEditRoleSaving] = useState(false);
  const [editRoleError, setEditRoleError] = useState<string | null>(null);
  const [confirmDeactivateRole, setConfirmDeactivateRole] = useState<string | null>(null);

  // Solver start time — defaults to now
  const [solverStartTime, setSolverStartTime] = useState(() => {
    const d = new Date();
    const pad = (n: number) => String(n).padStart(2, "0");
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  });

  const t = useTranslations("groups.settings_tab");
  const tCommon = useTranslations("common");

  async function handleCreateRole(e: React.FormEvent) {
    e.preventDefault();
    if (!newRoleName.trim()) return;
    setRoleFormSaving(true);
    setRoleFormError(null);
    try {
      await onCreateRole(newRoleName.trim(), newRoleDesc.trim() || null, newRolePermLevel);
      setNewRoleName("");
      setNewRoleDesc("");
      setNewRolePermLevel("view");
    } catch {
      setRoleFormError(t("errorCreateRole"));
    } finally {
      setRoleFormSaving(false);
    }
  }

  async function handleUpdateRole(roleId: string) {
    if (!editRoleName.trim()) return;
    setEditRoleSaving(true);
    setEditRoleError(null);
    try {
      await onUpdateRole(roleId, editRoleName.trim(), editRoleDesc.trim() || null, editRolePermLevel);
      setEditingRoleId(null);
    } catch {
      setEditRoleError(t("errorUpdateRole"));
    } finally {
      setEditRoleSaving(false);
    }
  }

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

      {/* Roles management */}
      <Section title={t("roles")}>
        {groupRolesLoading ? (
          <p className="text-sm text-slate-400">{t("loadingRoles")}</p>
        ) : (
          <div className="space-y-3">
            {/* Existing roles list */}
            {groupRoles.length > 0 && (
              <div className="space-y-2">
                {groupRoles.map(role => (
                  <div key={role.id} className={`flex items-center gap-2 px-3 py-2 rounded-xl border ${role.isActive ? "border-slate-200 bg-white" : "border-slate-100 bg-slate-50"}`}>
                    {editingRoleId === role.id ? (
                      <div className="flex-1 space-y-2">
                        <input
                          type="text"
                          value={editRoleName}
                          onChange={e => setEditRoleName(e.target.value)}
                          className="w-full border border-slate-200 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                          placeholder={t("addRole")}
                        />
                        <input
                          type="text"
                          value={editRoleDesc}
                          onChange={e => setEditRoleDesc(e.target.value)}
                          className="w-full border border-slate-200 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                          placeholder={t("roleDescription")}
                        />
                        <select
                          value={editRolePermLevel}
                          onChange={e => setEditRolePermLevel(e.target.value)}
                          className="w-full border border-slate-200 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                        >
                          <option value="view">{t("viewOnly")}</option>
                          <option value="ViewAndEdit">{t("viewAndEdit")}</option>
                          <option value="Owner">{t("ownerRole")}</option>
                        </select>
                        {editRoleError && <p className="text-xs text-red-600">{editRoleError}</p>}
                        <div className="flex gap-2">
                          <button
                            onClick={() => handleUpdateRole(role.id)}
                            disabled={editRoleSaving}
                            className="bg-blue-500 hover:bg-blue-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors"
                          >
                            {editRoleSaving ? t("saving") : t("save")}
                          </button>
                          <button
                            onClick={() => setEditingRoleId(null)}
                            className="text-xs text-slate-500 border border-slate-200 px-3 py-1.5 rounded-lg hover:bg-slate-50 transition-colors"
                          >
                            {t("cancel")}
                          </button>
                        </div>
                      </div>
                    ) : (
                      <>
                        <div className="flex-1 min-w-0">
                          <span className={`text-sm font-medium ${role.isActive ? "text-slate-800" : "text-slate-400 line-through"}`}>
                            {role.name}
                          </span>
                          {role.description && (
                            <span className="text-xs text-slate-400 mr-2">{role.description}</span>
                          )}
                          {role.isActive && (
                            <span className={`mr-2 text-xs px-1.5 py-0.5 rounded-full ${
                              role.permissionLevel === "Owner" ? "bg-purple-100 text-purple-700" :
                              role.permissionLevel === "ViewAndEdit" ? "bg-blue-100 text-blue-700" :
                              "bg-slate-100 text-slate-500"
                            }`}>
                              {role.permissionLevel === "Owner" ? t("ownerRole") :
                               role.permissionLevel === "ViewAndEdit" ? t("viewAndEdit") : t("viewOnly")}
                            </span>
                          )}
                        </div>
                        {role.isActive && (
                          <div className="flex items-center gap-1 flex-shrink-0">
                            <button
                              onClick={() => {
                                setEditingRoleId(role.id);
                                setEditRoleName(role.name);
                                setEditRoleDesc(role.description ?? "");
                                setEditRolePermLevel(role.permissionLevel ?? "view");
                                setEditRoleError(null);
                              }}
                              className="text-xs text-slate-500 border border-slate-200 hover:bg-slate-50 px-2.5 py-1 rounded-lg transition-colors"
                            >
                              {t("edit")}
                            </button>
                            {confirmDeactivateRole === role.id ? (
                              <>
                                <span className="text-xs text-slate-600">{t("deactivateConfirm")}</span>
                                <button onClick={() => { setConfirmDeactivateRole(null); onDeactivateRole(role.id); }} className="text-xs text-white bg-red-500 hover:bg-red-600 px-2.5 py-1 rounded-lg transition-colors">{t("confirm")}</button>
                                <button onClick={() => setConfirmDeactivateRole(null)} className="text-xs text-slate-500 border border-slate-200 px-2.5 py-1 rounded-lg hover:bg-slate-50 transition-colors">{t("cancel")}</button>
                              </>
                            ) : (
                              <button
                                onClick={() => setConfirmDeactivateRole(role.id)}
                                className="text-xs text-red-500 border border-red-100 hover:bg-red-50 px-2.5 py-1 rounded-lg transition-colors"
                              >
                                {t("deactivate")}
                              </button>
                            )}
                          </div>
                        )}
                      </>
                    )}
                  </div>
                ))}
              </div>
            )}

            {/* Add role form */}
            <form onSubmit={handleCreateRole} className="space-y-2 pt-1">
              <div className="flex gap-2">
                <input
                  type="text"
                  value={newRoleName}
                  onChange={e => setNewRoleName(e.target.value)}
                  placeholder={t("addRole")}
                  className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
                <button
                  type="submit"
                  disabled={roleFormSaving || !newRoleName.trim()}
                  className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2 rounded-xl disabled:opacity-50 transition-colors"
                >
                  {roleFormSaving ? "..." : t("add")}
                </button>
              </div>
              <input
                type="text"
                value={newRoleDesc}
                onChange={e => setNewRoleDesc(e.target.value)}
                placeholder={t("roleDescription")}
                className="w-full border border-slate-200 rounded-xl px-3.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
              <select
                value={newRolePermLevel}
                onChange={e => setNewRolePermLevel(e.target.value)}
                className="w-full border border-slate-200 rounded-xl px-3.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <option value="view">{t("viewOnly")}</option>
                <option value="ViewAndEdit">{t("viewAndEdit")}</option>
                <option value="Owner">{t("ownerRole")}</option>
              </select>
              {roleFormError && <p className="text-xs text-red-600">{roleFormError}</p>}
            </form>
          </div>
        )}
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
