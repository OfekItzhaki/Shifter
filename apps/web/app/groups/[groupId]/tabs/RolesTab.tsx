"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import type { GroupRoleDto, GroupMemberDto } from "@/lib/api/groups";

const PERM_LEVELS = ["view", "ViewAndEdit", "Owner"] as const;
type PermLevel = typeof PERM_LEVELS[number];

const PERM_META: Record<PermLevel, { label: string; color: string; description: string }> = {
  view:        { label: "View only",     color: "bg-slate-100 text-slate-600 border-slate-200",   description: "Can see the schedule, members, and alerts. Cannot make any changes." },
  ViewAndEdit: { label: "View + Edit",   color: "bg-blue-50 text-blue-700 border-blue-200",       description: "Can edit tasks, constraints, and member details. Cannot publish schedules or manage roles." },
  Owner:       { label: "Owner",         color: "bg-purple-50 text-purple-700 border-purple-200", description: "Full access — can publish schedules, manage roles, and transfer ownership." },
};

interface Props {
  isAdmin: boolean;
  groupRoles: GroupRoleDto[];
  groupRolesLoading: boolean;
  members: GroupMemberDto[];
  onCreateRole: (name: string, description: string | null, permissionLevel: string) => Promise<void>;
  onUpdateRole: (roleId: string, name: string, description: string | null, permissionLevel: string) => Promise<void>;
  onDeactivateRole: (roleId: string) => Promise<void>;
  onUpdateMemberRole: (personId: string, roleId: string | null) => Promise<void>;
}

export default function RolesTab({
  isAdmin, groupRoles, groupRolesLoading, members,
  onCreateRole, onUpdateRole, onDeactivateRole, onUpdateMemberRole,
}: Props) {
  const t = useTranslations("groups.roles_tab");
  const tSettings = useTranslations("groups.settings_tab");

  const [newRoleName, setNewRoleName] = useState("");
  const [newRoleDesc, setNewRoleDesc] = useState("");
  const [newRolePermLevel, setNewRolePermLevel] = useState<PermLevel>("view");
  const [roleFormSaving, setRoleFormSaving] = useState(false);
  const [roleFormError, setRoleFormError] = useState<string | null>(null);

  const [editingRoleId, setEditingRoleId] = useState<string | null>(null);
  const [editRoleName, setEditRoleName] = useState("");
  const [editRoleDesc, setEditRoleDesc] = useState("");
  const [editRolePermLevel, setEditRolePermLevel] = useState<PermLevel>("view");
  const [editRoleSaving, setEditRoleSaving] = useState(false);
  const [editRoleError, setEditRoleError] = useState<string | null>(null);
  const [confirmDeactivateRole, setConfirmDeactivateRole] = useState<string | null>(null);

  const [memberRoleSaving, setMemberRoleSaving] = useState<string | null>(null);

  async function handleCreateRole(e: React.FormEvent) {
    e.preventDefault();
    if (!newRoleName.trim()) return;
    setRoleFormSaving(true);
    setRoleFormError(null);
    try {
      await onCreateRole(newRoleName.trim(), newRoleDesc.trim() || null, newRolePermLevel);
      setNewRoleName(""); setNewRoleDesc(""); setNewRolePermLevel("view");
    } catch {
      setRoleFormError(tSettings("errorCreateRole"));
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
      setEditRoleError(tSettings("errorUpdateRole"));
    } finally {
      setEditRoleSaving(false);
    }
  }

  async function handleMemberRoleChange(personId: string, roleId: string | null) {
    setMemberRoleSaving(personId);
    try {
      await onUpdateMemberRole(personId, roleId);
    } finally {
      setMemberRoleSaving(null);
    }
  }

  const activeRoles = groupRoles.filter(r => r.isActive);

  if (!isAdmin) {
    return (
      <div className="flex flex-col items-center justify-center py-16 text-center bg-white rounded-xl border border-slate-200">
        <p className="text-slate-400 text-sm">{t("adminOnly")}</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">

      {/* Explainer */}
      <div className="bg-blue-50 border border-blue-200 rounded-2xl p-5 space-y-3">
        <div className="flex items-start gap-3">
          <svg className="w-5 h-5 text-blue-500 flex-shrink-0 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <div className="space-y-1">
            <p className="text-sm font-semibold text-blue-800">{t("explainerTitle")}</p>
            <p className="text-sm text-blue-700">{t("explainerBody")}</p>
          </div>
        </div>

        {/* Hierarchy */}
        <div className="space-y-2 pt-1">
          {(["view", "ViewAndEdit", "Owner"] as PermLevel[]).map((level, i) => {
            const meta = PERM_META[level];
            return (
              <div key={level} className="flex items-start gap-3">
                <div className="flex items-center gap-1.5 flex-shrink-0 mt-0.5">
                  <span className="text-xs font-bold text-blue-400 w-4 text-center">{i + 1}</span>
                  <span className={`text-xs font-semibold px-2 py-0.5 rounded-full border ${meta.color}`}>
                    {meta.label}
                  </span>
                </div>
                <p className="text-xs text-blue-700">{meta.description}</p>
              </div>
            );
          })}
        </div>

        <p className="text-xs text-blue-600 pt-1 border-t border-blue-200">
          {t("singleRoleNote")}
        </p>
      </div>

      {/* Role definitions */}
      <div className="bg-white border border-slate-200 rounded-2xl p-5 space-y-4">
        <h3 className="text-sm font-semibold text-slate-700">{t("definedRoles")}</h3>

        {groupRolesLoading ? (
          <p className="text-sm text-slate-400">{tSettings("loadingRoles")}</p>
        ) : (
          <div className="space-y-2">
            {groupRoles.length === 0 && (
              <p className="text-xs text-slate-400">{tSettings("noRoles")}</p>
            )}

            {groupRoles.map(role => {
              const permLevel = (role.permissionLevel?.toLowerCase() === "viewandedit"
                ? "ViewAndEdit"
                : role.permissionLevel === "Owner"
                ? "Owner"
                : "view") as PermLevel;
              const meta = PERM_META[permLevel] ?? PERM_META.view;

              return (
                <div key={role.id} className={`flex items-center gap-2 px-3 py-2.5 rounded-xl border ${role.isActive ? "border-slate-200 bg-white" : "border-slate-100 bg-slate-50"}`}>
                  {editingRoleId === role.id ? (
                    <div className="flex-1 space-y-2">
                      <input
                        type="text"
                        value={editRoleName}
                        onChange={e => setEditRoleName(e.target.value)}
                        className="w-full border border-slate-200 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                        placeholder={tSettings("addRole")}
                      />
                      <input
                        type="text"
                        value={editRoleDesc}
                        onChange={e => setEditRoleDesc(e.target.value)}
                        className="w-full border border-slate-200 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                        placeholder={tSettings("roleDescription")}
                      />
                      <select
                        value={editRolePermLevel}
                        onChange={e => setEditRolePermLevel(e.target.value as PermLevel)}
                        className="w-full border border-slate-200 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                      >
                        <option value="view">{tSettings("viewOnly")}</option>
                        <option value="ViewAndEdit">{tSettings("viewAndEdit")}</option>
                        <option value="Owner">{tSettings("ownerRole")}</option>
                      </select>
                      {editRoleError && <p className="text-xs text-red-600">{editRoleError}</p>}
                      <div className="flex gap-2">
                        <button onClick={() => handleUpdateRole(role.id)} disabled={editRoleSaving}
                          className="bg-blue-500 hover:bg-blue-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors">
                          {editRoleSaving ? tSettings("saving") : tSettings("save")}
                        </button>
                        <button onClick={() => setEditingRoleId(null)}
                          className="text-xs text-slate-500 border border-slate-200 px-3 py-1.5 rounded-lg hover:bg-slate-50 transition-colors">
                          {tSettings("cancel")}
                        </button>
                      </div>
                    </div>
                  ) : (
                    <>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 flex-wrap">
                          <span className={`text-sm font-medium ${role.isActive ? "text-slate-800" : "text-slate-400 line-through"}`}>
                            {role.name}
                          </span>
                          {role.isDefault && (
                            <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-slate-100 text-slate-500 border border-slate-200">
                              {t("default")}
                            </span>
                          )}
                          {role.isActive && (
                            <span className={`text-xs px-1.5 py-0.5 rounded-full border ${meta.color}`}>
                              {meta.label}
                            </span>
                          )}
                        </div>
                        {role.description && (
                          <p className="text-xs text-slate-400 mt-0.5">{role.description}</p>
                        )}
                      </div>
                      {role.isActive && (
                        <div className="flex items-center gap-1 flex-shrink-0">
                          <button
                            onClick={() => {
                              setEditingRoleId(role.id);
                              setEditRoleName(role.name);
                              setEditRoleDesc(role.description ?? "");
                              setEditRolePermLevel(permLevel);
                              setEditRoleError(null);
                            }}
                            className="text-xs text-slate-500 border border-slate-200 hover:bg-slate-50 px-2.5 py-1 rounded-lg transition-colors"
                          >
                            {tSettings("edit")}
                          </button>
                          {!role.isDefault && (
                            confirmDeactivateRole === role.id ? (
                              <>
                                <span className="text-xs text-slate-600">{tSettings("deactivateConfirm")}</span>
                                <button onClick={() => { setConfirmDeactivateRole(null); onDeactivateRole(role.id); }}
                                  className="text-xs text-white bg-red-500 hover:bg-red-600 px-2.5 py-1 rounded-lg transition-colors">
                                  {tSettings("confirm")}
                                </button>
                                <button onClick={() => setConfirmDeactivateRole(null)}
                                  className="text-xs text-slate-500 border border-slate-200 px-2.5 py-1 rounded-lg hover:bg-slate-50 transition-colors">
                                  {tSettings("cancel")}
                                </button>
                              </>
                            ) : (
                              <button onClick={() => setConfirmDeactivateRole(role.id)}
                                className="text-xs text-red-500 border border-red-100 hover:bg-red-50 px-2.5 py-1 rounded-lg transition-colors">
                                {tSettings("deactivate")}
                              </button>
                            )
                          )}
                        </div>
                      )}
                    </>
                  )}
                </div>
              );
            })}

            {/* Add role form */}
            <form onSubmit={handleCreateRole} className="space-y-2 pt-2 border-t border-slate-100">
              <div className="flex gap-2">
                <input
                  type="text"
                  value={newRoleName}
                  onChange={e => setNewRoleName(e.target.value)}
                  placeholder={tSettings("addRole")}
                  className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
                <button type="submit" disabled={roleFormSaving || !newRoleName.trim()}
                  className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2 rounded-xl disabled:opacity-50 transition-colors">
                  {roleFormSaving ? "..." : tSettings("add")}
                </button>
              </div>
              <input
                type="text"
                value={newRoleDesc}
                onChange={e => setNewRoleDesc(e.target.value)}
                placeholder={tSettings("roleDescription")}
                className="w-full border border-slate-200 rounded-xl px-3.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
              <select
                value={newRolePermLevel}
                onChange={e => setNewRolePermLevel(e.target.value as PermLevel)}
                className="w-full border border-slate-200 rounded-xl px-3.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <option value="view">{tSettings("viewOnly")}</option>
                <option value="ViewAndEdit">{tSettings("viewAndEdit")}</option>
                <option value="Owner">{tSettings("ownerRole")}</option>
              </select>
              {roleFormError && <p className="text-xs text-red-600">{roleFormError}</p>}
            </form>
          </div>
        )}
      </div>

      {/* Member role assignments */}
      {activeRoles.length > 0 && members.length > 0 && (
        <div className="bg-white border border-slate-200 rounded-2xl overflow-hidden">
          <div className="px-5 py-4 border-b border-slate-100">
            <h3 className="text-sm font-semibold text-slate-700">{t("memberRoles")}</h3>
            <p className="text-xs text-slate-400 mt-0.5">{t("memberRolesHint")}</p>
          </div>
          <div className="divide-y divide-slate-100">
            {members.map(m => {
              const currentRole = activeRoles.find(r => r.id === m.roleId);
              const isSaving = memberRoleSaving === m.personId;
              return (
                <div key={m.personId} className="flex items-center gap-3 px-5 py-3">
                  <div className="w-8 h-8 rounded-full bg-blue-500 flex items-center justify-center text-white text-xs font-bold flex-shrink-0">
                    {(m.displayName ?? m.fullName).charAt(0)}
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-slate-800 truncate">{m.displayName ?? m.fullName}</p>
                    {m.isOwner && (
                      <p className="text-xs text-amber-600">{tSettings("ownerRole")}</p>
                    )}
                  </div>
                  {isSaving ? (
                    <svg className="animate-spin h-4 w-4 text-blue-400 flex-shrink-0" fill="none" viewBox="0 0 24 24">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                    </svg>
                  ) : (
                    <select
                      value={m.roleId ?? ""}
                      onChange={e => handleMemberRoleChange(m.personId, e.target.value || null)}
                      disabled={m.isOwner}
                      className="border border-slate-200 rounded-lg px-2.5 py-1.5 text-xs focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed bg-white"
                    >
                      <option value="">{t("noRole")}</option>
                      {activeRoles.map(r => (
                        <option key={r.id} value={r.id}>{r.name}</option>
                      ))}
                    </select>
                  )}
                  {currentRole && !m.isOwner && (
                    <span className={`text-xs px-1.5 py-0.5 rounded-full border flex-shrink-0 ${PERM_META[(currentRole.permissionLevel?.toLowerCase() === "viewandedit" ? "ViewAndEdit" : currentRole.permissionLevel === "Owner" ? "Owner" : "view") as PermLevel]?.color ?? ""}`}>
                      {PERM_META[(currentRole.permissionLevel?.toLowerCase() === "viewandedit" ? "ViewAndEdit" : currentRole.permissionLevel === "Owner" ? "Owner" : "view") as PermLevel]?.label}
                    </span>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
