"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import AppShell from "@/components/shell/AppShell";
import { apiClient } from "@/lib/api/client";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useAuthStore } from "@/lib/store/authStore";
import Link from "next/link";
import ScheduleTable from "@/components/schedule/ScheduleTable";

interface GroupDto { id: string; name: string; memberCount: number; solverHorizonDays: number; }
interface MemberDto { personId: string; fullName: string; displayName: string | null; }
interface AssignmentDto {
  id: string; taskSlotId: string; personId: string; personName: string; taskTypeName: string;
  slotStartsAt: string; slotEndsAt: string; source: string;
}

type Tab = "members" | "schedule" | "settings";

export default function GroupsPage() {
  const t = useTranslations("admin");
  const { currentSpaceId } = useSpaceStore();
  const { isAdminMode } = useAuthStore();

  const [groups, setGroups] = useState<GroupDto[]>([]);
  const [selectedGroup, setSelectedGroup] = useState<GroupDto | null>(null);
  const [tab, setTab] = useState<Tab>("members");
  const [loading, setLoading] = useState(true);

  // Create group
  const [newGroupName, setNewGroupName] = useState("");
  const [savingGroup, setSavingGroup] = useState(false);

  // Add member by email
  const [emailInput, setEmailInput] = useState("");
  const [addingPerson, setAddingPerson] = useState(false);
  const [members, setMembers] = useState<MemberDto[]>([]);
  const [membersLoading, setMembersLoading] = useState(false);

  // Schedule tab
  const [scheduleAssignments, setScheduleAssignments] = useState<AssignmentDto[]>([]);
  const [scheduleLoading, setScheduleLoading] = useState(false);

  // Settings tab
  const [horizonDays, setHorizonDays] = useState(7);
  const [savingSettings, setSavingSettings] = useState(false);

  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  async function loadGroups() {
    if (!currentSpaceId) return;
    const { data } = await apiClient.get(`/spaces/${currentSpaceId}/groups`);
    setGroups(data);
  }

  useEffect(() => {
    if (!currentSpaceId) { setLoading(false); return; }
    loadGroups().finally(() => setLoading(false));
  }, [currentSpaceId]);

  async function selectGroup(g: GroupDto) {
    setSelectedGroup(g);
    setHorizonDays(g.solverHorizonDays);
    setTab("members");
    setError(null);
    setSuccess(null);
    loadMembers(g.id);
  }

  async function loadMembers(groupId: string) {
    if (!currentSpaceId) return;
    setMembersLoading(true);
    try {
      const { data } = await apiClient.get(`/spaces/${currentSpaceId}/groups/${groupId}/members`);
      setMembers(data);
    } finally { setMembersLoading(false); }
  }

  async function loadSchedule(groupId: string) {
    if (!currentSpaceId) return;
    setScheduleLoading(true);
    try {
      const { data } = await apiClient.get(`/spaces/${currentSpaceId}/groups/${groupId}/schedule`);
      setScheduleAssignments(data);
    } finally { setScheduleLoading(false); }
  }

  function handleTabChange(t: Tab) {
    setTab(t);
    if (t === "schedule" && selectedGroup) loadSchedule(selectedGroup.id);
  }

  async function handleCreateGroup(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId) { setError("טוען מרחב... נסה שוב בעוד שנייה"); return; }
    if (!newGroupName.trim()) return;
    setSavingGroup(true); setError(null);
    try {
      await apiClient.post(`/spaces/${currentSpaceId}/groups`, { name: newGroupName.trim(), description: null });
      await loadGroups();
      setNewGroupName("");
    } catch (err: any) {
      setError(err?.response?.data?.error || "שגיאה ביצירת קבוצה");
    } finally { setSavingGroup(false); }
  }

  async function handleAddByEmail(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId || !selectedGroup || !emailInput.trim()) return;
    setAddingPerson(true); setError(null);
    try {
      await apiClient.post(
        `/spaces/${currentSpaceId}/groups/${selectedGroup.id}/members/by-email`,
        { email: emailInput.trim() }
      );
      setEmailInput("");
      setSuccess("האדם נוסף לקבוצה בהצלחה");
      await loadMembers(selectedGroup.id);
      await loadGroups();
    } catch (err: any) {
      setError(err?.response?.data?.error || "שגיאה בהוספת אדם");
    } finally { setAddingPerson(false); }
  }

  async function handleRemoveMember(personId: string) {
    if (!currentSpaceId || !selectedGroup) return;
    try {
      await apiClient.delete(`/spaces/${currentSpaceId}/groups/${selectedGroup.id}/members/${personId}`);
      setSuccess("האדם הוסר מהקבוצה");
      await loadMembers(selectedGroup.id);
      await loadGroups();
    } catch { setError("שגיאה בהסרת אדם"); }
  }

  async function handleSaveSettings(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId || !selectedGroup) return;
    setSavingSettings(true); setError(null);
    try {
      await apiClient.patch(
        `/spaces/${currentSpaceId}/groups/${selectedGroup.id}/settings`,
        { solverHorizonDays: horizonDays }
      );
      setSuccess("ההגדרות נשמרו");
      await loadGroups();
      setSelectedGroup(prev => prev ? { ...prev, solverHorizonDays: horizonDays } : prev);
    } catch { setError("שגיאה בשמירת הגדרות"); }
    finally { setSavingSettings(false); }
  }

  if (!isAdminMode) {
    return <AppShell><p className="text-slate-500 text-sm p-8">{t("adminRequired")}</p></AppShell>;
  }

  const inp = "border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent";

  const complexityNote = (days: number) => {
    if (days <= 3) return { text: "מורכבות נמוכה", color: "text-emerald-600" };
    if (days <= 7) return { text: "מורכבות בינונית", color: "text-amber-600" };
    if (days <= 14) return { text: "מורכבות גבוהה", color: "text-orange-600" };
    return { text: "מורכבות גבוהה מאוד — עלול להיות איטי", color: "text-red-600" };
  };

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
        {success && (
          <div className="bg-emerald-50 border border-emerald-200 rounded-xl px-4 py-3 text-sm text-emerald-700">{success}</div>
        )}

        <div className="grid grid-cols-3 gap-6">
          {/* Left: groups list */}
          <div className="col-span-1 space-y-4">
            <form onSubmit={handleCreateGroup} className="flex gap-2">
              <input value={newGroupName} onChange={e => setNewGroupName(e.target.value)}
                placeholder="שם קבוצה חדשה" className={`flex-1 ${inp}`} />
              <button type="submit" disabled={savingGroup}
                className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-3 py-2.5 rounded-xl disabled:opacity-50 whitespace-nowrap">
                {savingGroup ? "..." : "+ הוסף"}
              </button>
            </form>

            {loading ? <p className="text-slate-400 text-sm py-4">טוען...</p> : (
              <div className="space-y-1.5">
                {groups.map(g => (
                  <button key={g.id} onClick={() => selectGroup(g)}
                    className={`w-full text-start px-3.5 py-3 rounded-xl border text-sm transition-all ${
                      selectedGroup?.id === g.id
                        ? "border-blue-300 bg-blue-50 shadow-sm"
                        : "border-slate-200 bg-white hover:border-slate-300 hover:bg-slate-50"
                    }`}>
                    <div className="font-semibold text-slate-900">{g.name}</div>
                    <div className="text-xs text-slate-400 mt-0.5">{g.memberCount} חברים</div>
                  </button>
                ))}
                {groups.length === 0 && (
                  <p className="text-xs text-slate-400 text-center py-6">{t("noData")}</p>
                )}
              </div>
            )}
          </div>

          {/* Right: group detail panel */}
          <div className="col-span-2">
            {selectedGroup ? (
              <div className="bg-white border border-slate-200 rounded-2xl shadow-sm overflow-hidden">
                {/* Group header */}
                <div className="px-5 pt-5 pb-0">
                  <h2 className="text-base font-semibold text-slate-900">{selectedGroup.name}</h2>
                  <p className="text-xs text-slate-400 mt-0.5">{selectedGroup.memberCount} חברים · אופק: {selectedGroup.solverHorizonDays} ימים</p>
                </div>

                {/* Tabs */}
                <div className="flex gap-0 border-b border-slate-200 mt-4 px-5">
                  {(["members", "schedule", "settings"] as Tab[]).map(tabKey => (
                    <button key={tabKey} onClick={() => handleTabChange(tabKey)}
                      className={`px-4 py-2.5 text-sm font-medium border-b-2 -mb-px transition-colors ${
                        tab === tabKey
                          ? "border-blue-500 text-blue-600"
                          : "border-transparent text-slate-500 hover:text-slate-700"
                      }`}>
                      {tabKey === "members" ? "חברים" : tabKey === "schedule" ? "סידור" : "הגדרות"}
                    </button>
                  ))}
                </div>

                <div className="p-5">
                  {/* Members tab */}
                  {tab === "members" && (
                    <div className="space-y-4">
                      <form onSubmit={handleAddByEmail} className="flex gap-2">
                        <input value={emailInput} onChange={e => setEmailInput(e.target.value)}
                          placeholder="הוסף לפי אימייל..." type="email" required
                          className={`flex-1 ${inp}`} />
                        <button type="submit" disabled={addingPerson}
                          className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 whitespace-nowrap">
                          {addingPerson ? "..." : "+ הוסף"}
                        </button>
                      </form>
                      <p className="text-xs text-slate-400">
                        אם האדם לא רשום עדיין, ייווצר עבורו פרופיל אוטומטי. הוא יקבל התראה עם אפשרות לעזוב.
                      </p>

                      {membersLoading ? <p className="text-slate-400 text-sm py-4">טוען...</p> : (
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
                                className="text-xs text-blue-500 hover:text-blue-700 shrink-0">פרטים</Link>
                              <button onClick={() => handleRemoveMember(m.personId)}
                                className="text-xs text-red-400 hover:text-red-600 shrink-0 px-1">הסר</button>
                            </div>
                          ))}
                          {members.length === 0 && (
                            <p className="text-xs text-slate-400 text-center py-6">אין חברים עדיין.</p>
                          )}
                        </div>
                      )}
                    </div>
                  )}

                  {/* Schedule tab */}
                  {tab === "schedule" && (
                    <div>
                      {scheduleLoading ? (
                        <p className="text-slate-400 text-sm py-4">טוען סידור...</p>
                      ) : scheduleAssignments.length === 0 ? (
                        <div className="flex flex-col items-center justify-center py-12 text-center">
                          <p className="text-slate-400 text-sm">אין סידור פורסם עדיין לקבוצה זו.</p>
                        </div>
                      ) : (
                        <ScheduleTable assignments={scheduleAssignments} />
                      )}
                    </div>
                  )}

                  {/* Settings tab */}
                  {tab === "settings" && (
                    <form onSubmit={handleSaveSettings} className="space-y-5 max-w-sm">
                      <div>
                        <label className="block text-sm font-medium text-slate-700 mb-2">
                          אופק תכנון הסולבר
                        </label>
                        <div className="flex items-center gap-4">
                          <input type="range" min={1} max={30} value={horizonDays}
                            onChange={e => setHorizonDays(Number(e.target.value))}
                            className="flex-1" />
                          <span className="text-sm font-semibold text-slate-900 w-16 text-center">
                            {horizonDays} ימים
                          </span>
                        </div>
                        <p className={`text-xs mt-1.5 font-medium ${complexityNote(horizonDays).color}`}>
                          {complexityNote(horizonDays).text}
                        </p>
                        <p className="text-xs text-slate-400 mt-1">
                          ככל שמוסיפים ימים, מורכבות החישוב עולה. מומלץ להתחיל עם 7 ימים.
                        </p>
                      </div>
                      <button type="submit" disabled={savingSettings}
                        className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50">
                        {savingSettings ? "שומר..." : "שמור הגדרות"}
                      </button>
                    </form>
                  )}
                </div>
              </div>
            ) : (
              <div className="flex flex-col items-center justify-center py-16 text-center bg-white border border-slate-200 rounded-2xl">
                <svg width="32" height="32" fill="none" viewBox="0 0 24 24" stroke="#e2e8f0" strokeWidth={1.5} style={{ marginBottom: 8 }}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
                </svg>
                <p className="text-xs text-slate-400">בחר קבוצה לניהול</p>
              </div>
            )}
          </div>
        </div>
      </div>
    </AppShell>
  );
}
