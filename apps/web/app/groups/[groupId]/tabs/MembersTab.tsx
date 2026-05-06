"use client";

import { useState, useEffect } from "react";
import { useTranslations } from "next-intl";
import Modal from "@/components/Modal";
import ImageUpload from "@/components/ImageUpload";
import type { GroupMemberDto, GroupRoleDto } from "@/lib/api/groups";
import { apiClient } from "@/lib/api/client";
import { useSpaceStore } from "@/lib/store/spaceStore";

interface Props {
  isAdmin: boolean;
  /** True only when the current user is the group owner — can edit member roles */
  isOwner: boolean;
  members: GroupMemberDto[];
  membersLoading: boolean;
  membersError: string | null;
  membersSearch: string;
  removeErrors: Record<string, string>;
  groupRoles: GroupRoleDto[];
  onSearchChange: (v: string) => void;
  onSelectMember: (m: GroupMemberDto) => void;
  onRemoveMember: (id: string) => void;
  onOpenAddMember: () => void;
  onOpenImport?: () => void;
  onOpenInvite: (id: string) => void;
  onUpdateMemberRole: (personId: string, roleId: string | null) => Promise<void>;
}

export default function MembersTab({
  isAdmin, isOwner, members, membersLoading, membersError, membersSearch, removeErrors,
  groupRoles, onSearchChange, onSelectMember, onRemoveMember, onOpenAddMember,
  onOpenImport, onOpenInvite, onUpdateMemberRole,
}: Props) {
  const t = useTranslations("groups.members_tab");
  const tCommon = useTranslations("common");
  const [confirmRemove, setConfirmRemove] = useState<string | null>(null);

  const filtered = members.filter(m =>
    !membersSearch ||
    m.fullName.toLowerCase().includes(membersSearch.toLowerCase()) ||
    (m.displayName ?? "").toLowerCase().includes(membersSearch.toLowerCase())
  );

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3">
        <div className="relative flex-1 max-w-xs">
          <input
            type="text"
            value={membersSearch}
            onChange={e => onSearchChange(e.target.value)}
            placeholder={t("search")}
            className="w-full border border-slate-200 rounded-xl px-3.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 pr-9"
          />
          <svg className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>
        {isAdmin && (
          <div className="flex items-center gap-2">
            <button onClick={onOpenAddMember} className="flex items-center gap-1.5 text-sm font-medium text-blue-600 border border-blue-200 bg-blue-50 hover:bg-blue-100 px-3 py-2 rounded-xl transition-colors">
              {t("addMember")}
            </button>
            <button onClick={onOpenImport} className="flex items-center gap-1.5 text-sm font-medium text-slate-600 border border-slate-200 bg-white hover:bg-slate-50 px-3 py-2 rounded-xl transition-colors">
              <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
              </svg>
              📥
            </button>
          </div>
        )}
      </div>

      {membersLoading && <p className="text-sm text-slate-400 py-8">{tCommon("loading")}</p>}
      {membersError && <p className="text-sm text-red-600">{membersError}</p>}

      {!membersLoading && filtered.length === 0 && (
        <div className="flex flex-col items-center justify-center py-16 text-center bg-white rounded-xl border border-slate-200">
          <svg className="w-10 h-10 text-slate-200 mb-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
          </svg>
          <p className="text-slate-400 text-sm">{t("noMembers")}</p>
        </div>
      )}

      <div className="space-y-2">
        {filtered.map(m => (
          <div key={m.personId} className="bg-white border border-slate-200 rounded-xl px-4 py-3 hover:border-slate-300 transition-colors">
            <div className="flex items-center gap-3">
              {/* Avatar */}
              <div className="w-9 h-9 rounded-full bg-blue-500 flex items-center justify-center text-white text-sm font-bold flex-shrink-0">
                {m.fullName.charAt(0).toUpperCase()}
              </div>

              {/* Name + role badge */}
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 flex-wrap">
                  <p className="text-sm font-medium text-slate-900 truncate">{m.fullName}</p>
                  {m.isOwner && (
                    <span className="text-xs px-1.5 py-0.5 rounded-full bg-amber-100 text-amber-700 border border-amber-200 flex-shrink-0">
                      {t("owner")}
                    </span>
                  )}
                  {!m.isOwner && m.roleName && (
                    <span className="text-xs px-1.5 py-0.5 rounded-full bg-slate-100 text-slate-600 border border-slate-200 flex-shrink-0">
                      {m.roleName}
                    </span>
                  )}
                </div>
                {m.displayName && m.displayName !== m.fullName && (
                  <p className="text-xs text-slate-400 truncate">{m.displayName}</p>
                )}
                {m.phoneNumber && <p className="text-xs text-slate-400 tabular-nums" dir="ltr">{m.phoneNumber}</p>}
              </div>

              {/* Actions */}
              <div className="flex items-center gap-2 flex-shrink-0">
                <button onClick={() => onSelectMember(m)} className="text-xs text-blue-600 hover:underline">{t("details")}</button>
                {isAdmin && !m.isOwner && (
                  <>
                    <button onClick={() => onOpenInvite(m.personId)} className="text-xs text-slate-500 hover:text-slate-700 border border-slate-200 px-2 py-1 rounded-lg hover:bg-slate-50 transition-colors">{t("invite")}</button>
                    {confirmRemove === m.personId ? (
                      <>
                        <span className="text-xs text-slate-600">{t("permanentRemove")}</span>
                        <button
                          onClick={() => { setConfirmRemove(null); onRemoveMember(m.personId); }}
                          className="text-xs text-white bg-red-500 hover:bg-red-600 px-2 py-1 rounded-lg transition-colors"
                        >
                          {t("confirm")}
                        </button>
                        <button
                          onClick={() => setConfirmRemove(null)}
                          className="text-xs text-slate-500 border border-slate-200 px-2 py-1 rounded-lg hover:bg-slate-50 transition-colors"
                        >
                          {t("cancel")}
                        </button>
                      </>
                    ) : (
                      <button
                        onClick={() => setConfirmRemove(m.personId)}
                        className="text-xs text-red-500 hover:text-red-700 border border-red-100 px-2 py-1 rounded-lg hover:bg-red-50 transition-colors"
                      >
                        {t("remove")}
                      </button>
                    )}
                  </>
                )}
              </div>
            </div>

            {removeErrors[m.personId] && (
              <p className="text-xs text-red-600 mt-1">{removeErrors[m.personId]}</p>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

// ── Member profile modal ──────────────────────────────────────────────────────
interface MemberProfileModalProps {
  member: GroupMemberDto;
  isAdmin: boolean;
  editForm: { fullName: string; displayName: string; phoneNumber: string; profileImageUrl: string; birthday: string } | null;
  saving: boolean;
  error: string | null;
  onClose: () => void;
  onStartEdit: () => void;
  onCancelEdit: () => void;
  onChangeForm: (f: { fullName: string; displayName: string; phoneNumber: string; profileImageUrl: string; birthday: string }) => void;
  onSave: (personId: string) => void;
}

export function MemberProfileModal({ member, isAdmin, editForm, saving, error, onClose, onStartEdit, onCancelEdit, onChangeForm, onSave }: MemberProfileModalProps) {
  const t = useTranslations("groups.members_tab");
  const tCommon = useTranslations("common");
  const tProfile = useTranslations("profile");
  const [availTab, setAvailTab] = useState<"info" | "availability">("info");
  const [presenceWindows, setPresenceWindows] = useState<{ id: string; state: string; startsAt: string; endsAt: string; note: string | null }[]>([]);
  const [presenceLoading, setPresenceLoading] = useState(false);
  const [newPresenceStart, setNewPresenceStart] = useState("");
  const [newPresenceEnd, setNewPresenceEnd] = useState("");
  const [newPresenceNote, setNewPresenceNote] = useState("");
  const [presenceSaving, setPresenceSaving] = useState(false);
  const [presenceError, setPresenceError] = useState<string | null>(null);
  const { currentSpaceId } = useSpaceStore();

  useEffect(() => {
    if (availTab !== "availability" || !currentSpaceId || !isAdmin) return;
    setPresenceLoading(true);
    apiClient.get(`/spaces/${currentSpaceId}/people/${member.personId}/presence`)
      .then(r => setPresenceWindows(r.data ?? []))
      .catch(() => {})
      .finally(() => setPresenceLoading(false));
  }, [availTab, currentSpaceId, member.personId, isAdmin]);

  async function handleAddPresence(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId || !newPresenceStart || !newPresenceEnd) return;
    setPresenceSaving(true);
    setPresenceError(null);
    try {
      await apiClient.post(`/spaces/${currentSpaceId}/people/${member.personId}/presence`, {
        state: "at_home",
        startsAt: new Date(newPresenceStart).toISOString(),
        endsAt: new Date(newPresenceEnd).toISOString(),
        note: newPresenceNote || null,
      });
      setNewPresenceStart(""); setNewPresenceEnd(""); setNewPresenceNote("");
      // Reload
      const r = await apiClient.get(`/spaces/${currentSpaceId}/people/${member.personId}/presence`);
      setPresenceWindows(r.data ?? []);
    } catch {
      setPresenceError(t("errorAddPresence"));
    } finally {
      setPresenceSaving(false);
    }
  }

  return (
    <Modal title={t("memberDetails")} open onClose={onClose} maxWidth={520}>
      {/* Tab switcher */}
      {isAdmin && (
        <div className="flex gap-1 bg-slate-100 p-1 rounded-xl mb-4">
          <button onClick={() => setAvailTab("info")} className={`flex-1 py-1.5 rounded-lg text-sm font-medium transition-all ${availTab === "info" ? "bg-white text-slate-900 shadow-sm" : "text-slate-500"}`}>{t("tabInfo")}</button>
          <button onClick={() => setAvailTab("availability")} className={`flex-1 py-1.5 rounded-lg text-sm font-medium transition-all ${availTab === "availability" ? "bg-white text-slate-900 shadow-sm" : "text-slate-500"}`}>{t("tabAvailability")}</button>
        </div>
      )}

      {availTab === "info" ? (
        editForm ? (
          <div className="space-y-4">
            <div>
              <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1">{tProfile("fullName")}</label>
              <input type="text" value={editForm.fullName} onChange={e => onChangeForm({ ...editForm, fullName: e.target.value })} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1">{tProfile("displayName")}</label>
              <input type="text" value={editForm.displayName} onChange={e => onChangeForm({ ...editForm, displayName: e.target.value })} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1">{tProfile("phone")}</label>
              <input type="tel" value={editForm.phoneNumber} onChange={e => onChangeForm({ ...editForm, phoneNumber: e.target.value })} dir="ltr" className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1">{tProfile("profileImage")}</label>
              <ImageUpload value={editForm.profileImageUrl || null} onChange={url => onChangeForm({ ...editForm, profileImageUrl: url })} shape="circle" size={64} label={tProfile("uploadImage")} disabled={saving} />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1">{tProfile("birthday")}</label>
              <input type="date" value={editForm.birthday} onChange={e => onChangeForm({ ...editForm, birthday: e.target.value })} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
            {error && <p className="text-sm text-red-600">{error}</p>}
            <div className="flex gap-2 pt-1">
              <button onClick={() => onSave(member.personId)} disabled={saving} className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
                {saving ? tCommon("loading") : tCommon("save")}
              </button>
              <button onClick={onCancelEdit} className="text-sm text-slate-500 border border-slate-200 px-4 py-2.5 rounded-xl hover:bg-slate-50 transition-colors">{tCommon("cancel")}</button>
            </div>
          </div>
        ) : (
          <div className="space-y-4">
            <div className="flex items-center gap-4">
              {member.profileImageUrl ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  src={member.profileImageUrl}
                  alt={member.displayName ?? member.fullName}
                  className="w-16 h-16 rounded-full object-cover flex-shrink-0"
                />
              ) : (
                <div className="w-16 h-16 rounded-full bg-blue-500 flex items-center justify-center text-white text-2xl font-bold flex-shrink-0">
                  {(member.displayName ?? member.fullName).charAt(0).toUpperCase()}
                </div>
              )}
              <div>
                <p className="text-lg font-semibold text-slate-900">{member.displayName ?? member.fullName}</p>
                {member.roleName && <p className="text-sm text-slate-500">{member.roleName}</p>}
                {member.phoneNumber && <p className="text-sm text-slate-500 tabular-nums" dir="ltr">{member.phoneNumber}</p>}
              </div>
            </div>
            {isAdmin && (
              <button onClick={onStartEdit} className="text-sm text-blue-600 border border-blue-200 bg-blue-50 hover:bg-blue-100 px-4 py-2 rounded-xl transition-colors">
                {t("editDetails")}
              </button>
            )}
          </div>
        )
      ) : (
        /* Availability tab */
        <div className="space-y-4">
          <p className="text-xs text-slate-500">{t("availabilityHint")}</p>

          {/* Existing windows */}
          {presenceLoading ? (
            <p className="text-sm text-slate-400">{tCommon("loading")}</p>
          ) : presenceWindows.length === 0 ? (
            <p className="text-sm text-slate-400">{t("noAvailabilityWindows")}</p>
          ) : (
            <div className="space-y-2">
              {presenceWindows.map((w, i) => (
                <div key={w.id ?? i} className="flex items-center justify-between bg-slate-50 border border-slate-200 rounded-xl px-3 py-2 text-sm">
                  <div>
                    <span className="font-medium text-slate-700">
                      {new Date(w.startsAt).toLocaleDateString(undefined)} – {new Date(w.endsAt).toLocaleDateString(undefined)}
                    </span>
                    {w.note && <span className="text-slate-400 mr-2 text-xs"> · {w.note}</span>}
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="text-xs px-2 py-0.5 rounded-full bg-amber-100 text-amber-700">{t("unavailable")}</span>
                    <button
                      onClick={async () => {
                        if (!currentSpaceId) return;
                        try {
                          await apiClient.delete(`/spaces/${currentSpaceId}/people/${member.personId}/presence/${w.id}`);
                          setPresenceWindows(prev => prev.filter(p => p.id !== w.id));
                        } catch { /* non-fatal */ }
                      }}
                      className="text-xs text-red-400 hover:text-red-600 transition-colors"
                      title="Remove"
                    >✕</button>
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Add new window */}
          <form onSubmit={handleAddPresence} className="space-y-3 pt-2 border-t border-slate-100">
            <p className="text-xs font-semibold text-slate-500 uppercase tracking-wide">{t("addAvailabilityWindow")}</p>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-xs text-slate-500 mb-1">{t("from")}</label>
                <input type="datetime-local" value={newPresenceStart} onChange={e => setNewPresenceStart(e.target.value)} required className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
              </div>
              <div>
                <label className="block text-xs text-slate-500 mb-1">{t("until")}</label>
                <input type="datetime-local" value={newPresenceEnd} onChange={e => setNewPresenceEnd(e.target.value)} required className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
              </div>
            </div>
            <input type="text" value={newPresenceNote} onChange={e => setNewPresenceNote(e.target.value)} placeholder={tCommon("optional")} className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            {presenceError && <p className="text-xs text-red-600">{presenceError}</p>}
            <button type="submit" disabled={presenceSaving} className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2 rounded-xl disabled:opacity-50 transition-colors">
              {presenceSaving ? tCommon("loading") : tCommon("add")}
            </button>
          </form>
        </div>
      )}
    </Modal>
  );
}
