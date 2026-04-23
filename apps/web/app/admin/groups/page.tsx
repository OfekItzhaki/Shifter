"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import AppShell from "@/components/shell/AppShell";
import { apiClient } from "@/lib/api/client";
import { createPerson } from "@/lib/api/people";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useAuthStore } from "@/lib/store/authStore";
import Link from "next/link";

interface GroupDto { id: string; name: string; memberCount: number; }
interface MemberDto { personId: string; fullName: string; displayName: string | null; }

export default function GroupsPage() {
  const t = useTranslations("admin");
  const { currentSpaceId } = useSpaceStore();
  const { isAdminMode } = useAuthStore();

  const [groups, setGroups] = useState<GroupDto[]>([]);
  const [selectedGroup, setSelectedGroup] = useState<GroupDto | null>(null);
  const [members, setMembers] = useState<MemberDto[]>([]);
  const [loading, setLoading] = useState(true);

  const [newGroupName, setNewGroupName] = useState("");
  const [savingGroup, setSavingGroup] = useState(false);

  const [showAddPerson, setShowAddPerson] = useState(false);
  const [newPersonFull, setNewPersonFull] = useState("");
  const [newPersonDisplay, setNewPersonDisplay] = useState("");
  const [addingPerson, setAddingPerson] = useState(false);

  const [error, setError] = useState<string | null>(null);

  async function loadGroups() {
    if (!currentSpaceId) return;
    const { data } = await apiClient.get(`/spaces/${currentSpaceId}/groups`);
    setGroups(data);
  }

  useEffect(() => {
    if (!currentSpaceId) { setLoading(false); return; }
    loadGroups().finally(() => setLoading(false));
  }, [currentSpaceId]);

  async function loadMembers(group: GroupDto) {
    setSelectedGroup(group);
    setShowAddPerson(false);
    const { data } = await apiClient.get(`/spaces/${currentSpaceId}/groups/${group.id}/members`);
    setMembers(data);
  }

  async function handleCreateGroup(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId) { setError("טוען מרחב... נסה שוב בעוד שנייה"); return; }
    if (!newGroupName.trim()) return;
    setSavingGroup(true);
    setError(null);
    try {
      await apiClient.post(`/spaces/${currentSpaceId}/groups`, {
        name: newGroupName.trim(), description: null,
      });
      await loadGroups();
      setNewGroupName("");
    } catch (err: any) {
      setError(err?.response?.data?.error || err?.response?.data?.message || "שגיאה ביצירת קבוצה");
    }
    finally { setSavingGroup(false); }
  }

  async function handleAddNewPerson(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId || !selectedGroup || !newPersonFull.trim()) return;
    setAddingPerson(true);
    try {
      const { data: newP } = await apiClient.post(`/spaces/${currentSpaceId}/people`, {
        fullName: newPersonFull.trim(),
        displayName: newPersonDisplay.trim() || null,
        linkedUserId: null,
      });
      await apiClient.post(`/spaces/${currentSpaceId}/groups/${selectedGroup.id}/members`, { personId: newP.id });
      await loadMembers(selectedGroup);
      await loadGroups();
      setNewPersonFull(""); setNewPersonDisplay(""); setShowAddPerson(false);
    } catch { setError("שגיאה בהוספת אדם"); }
    finally { setAddingPerson(false); }
  }

  if (!isAdminMode) {
    return <AppShell><p className="text-slate-500 text-sm p-8">{t("adminRequired")}</p></AppShell>;
  }

  const inp = "border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent";

  return (
    <AppShell>
      <div className="max-w-5xl space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">{t("groups")}</h1>
          <p className="text-sm text-slate-500 mt-1">ניהול קבוצות ואנשים</p>
        </div>

        {error && (
          <div className="bg-red-50 border border-red-200 rounded-xl px-4 py-3 text-sm text-red-700">{error}</div>
        )}

        <div className="grid grid-cols-3 gap-6">
          {/* Left: create group + groups table */}
          <div className="col-span-2 space-y-4">
            {/* Create group — just a name */}
            <form onSubmit={handleCreateGroup} className="flex gap-2">
              <input
                value={newGroupName}
                onChange={e => setNewGroupName(e.target.value)}
                placeholder="שם קבוצה חדשה (לדוגמה: כיתה א׳)"
                className={`flex-1 ${inp}`}
              />
              <button type="submit" disabled={savingGroup}
                className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 whitespace-nowrap">
                {savingGroup ? "..." : `+ ${t("addGroup")}`}
              </button>
            </form>

            {/* Groups table */}
            {loading ? <p className="text-slate-400 text-sm py-8">טוען...</p> : (
              <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-slate-100 bg-slate-50/80">
                      <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">קבוצה</th>
                      <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">חברים</th>
                      <th className="px-4 py-3"></th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {groups.map(g => (
                      <tr key={g.id}
                        className={`hover:bg-slate-50/60 transition-colors ${selectedGroup?.id === g.id ? "bg-blue-50/40" : ""}`}>
                        <td className="px-4 py-3.5 font-medium text-slate-900">{g.name}</td>
                        <td className="px-4 py-3.5">
                          <span className="inline-flex items-center justify-center w-6 h-6 rounded-full bg-slate-100 text-slate-600 text-xs font-semibold">
                            {g.memberCount}
                          </span>
                        </td>
                        <td className="px-4 py-3.5">
                          <button onClick={() => loadMembers(g)}
                            className="text-xs font-medium text-blue-600 hover:text-blue-700">
                            ניהול →
                          </button>
                        </td>
                      </tr>
                    ))}
                    {groups.length === 0 && (
                      <tr>
                        <td colSpan={3} className="px-4 py-12 text-center text-slate-400 text-sm">
                          {t("noData")}
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {/* Right: members panel */}
          <div>
            {selectedGroup ? (
              <div className="bg-white border border-slate-200 rounded-2xl p-4 shadow-sm space-y-4">
                <div className="flex items-center justify-between">
                  <div>
                    <h2 className="text-sm font-semibold text-slate-900">{selectedGroup.name}</h2>
                    <p className="text-xs text-slate-500 mt-0.5">{t("members")}</p>
                  </div>
                  <button onClick={() => setShowAddPerson(!showAddPerson)}
                    className="text-xs font-medium text-blue-600 hover:text-blue-700 bg-blue-50 px-2.5 py-1.5 rounded-lg">
                    + {t("addPerson")}
                  </button>
                </div>

                {showAddPerson && (
                  <form onSubmit={handleAddNewPerson} className="space-y-2 border-t pt-3">
                    <input value={newPersonFull} onChange={e => setNewPersonFull(e.target.value)}
                      placeholder="שם מלא *" required
                      className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
                    <input value={newPersonDisplay} onChange={e => setNewPersonDisplay(e.target.value)}
                      placeholder="שם תצוגה (אופציונלי)"
                      className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
                    <div className="flex gap-2">
                      <button type="submit" disabled={addingPerson}
                        className="flex-1 bg-blue-500 hover:bg-blue-600 text-white text-xs font-medium py-2 rounded-xl disabled:opacity-50">
                        {addingPerson ? "..." : "הוסף"}
                      </button>
                      <button type="button" onClick={() => setShowAddPerson(false)}
                        className="text-xs text-slate-500 px-2">ביטול</button>
                    </div>
                  </form>
                )}

                <div className="space-y-1.5">
                  {members.map(m => (
                    <div key={m.personId}
                      className="flex items-center gap-2.5 px-3 py-2.5 bg-slate-50 border border-slate-100 rounded-xl">
                      <div className="w-7 h-7 rounded-full bg-blue-100 flex items-center justify-center shrink-0">
                        <span className="text-xs font-semibold text-blue-600">
                          {(m.displayName ?? m.fullName).charAt(0).toUpperCase()}
                        </span>
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="text-sm text-slate-700 truncate">{m.displayName ?? m.fullName}</p>
                        {m.displayName && <p className="text-xs text-slate-400 truncate">{m.fullName}</p>}
                      </div>
                      <Link href={`/admin/people/${m.personId}`}
                        className="text-xs text-blue-500 hover:text-blue-700 shrink-0">
                        פרטים
                      </Link>
                    </div>
                  ))}
                  {members.length === 0 && (
                    <p className="text-xs text-slate-400 text-center py-4">אין חברים עדיין.</p>
                  )}
                </div>
              </div>
            ) : (
              <div className="flex flex-col items-center justify-center py-12 text-center bg-white border border-slate-200 rounded-2xl">
                <svg width="32" height="32" fill="none" viewBox="0 0 24 24" stroke="#e2e8f0" strokeWidth={1.5} style={{ marginBottom: 8 }}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
                </svg>
                <p className="text-xs text-slate-400">בחר קבוצה לניהול חברים</p>
              </div>
            )}
          </div>
        </div>
      </div>
    </AppShell>
  );
}
