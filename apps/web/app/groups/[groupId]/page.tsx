"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import AppShell from "@/components/shell/AppShell";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useAuthStore } from "@/lib/store/authStore";
import {
  getGroups,
  getGroupMembers,
  addGroupMemberByEmail,
  removeGroupMember,
  updateGroupSettings,
  GroupWithMemberCountDto,
  GroupMemberDto,
} from "@/lib/api/groups";
import { getTaskTypes, getTaskSlots, TaskTypeDto, TaskSlotDto } from "@/lib/api/tasks";
import { getConstraints, ConstraintDto } from "@/lib/api/constraints";
import { apiClient } from "@/lib/api/client";

type ActiveTab = "schedule" | "members-readonly" | "members-edit" | "tasks" | "constraints" | "settings";

const ADMIN_ONLY_TABS: ActiveTab[] = ["members-edit", "tasks", "constraints", "settings"];

interface ScheduleAssignment {
  personName: string;
  taskTypeName: string;
  startsAt: string;
  endsAt: string;
}

const burdenLabels: Record<string, string> = {
  Favorable: "נוח", Neutral: "ניטרלי", Disliked: "לא אהוב", Hated: "שנוא",
  favorable: "נוח", neutral: "ניטרלי", disliked: "לא אהוב", hated: "שנוא",
};
const burdenColors: Record<string, string> = {
  Favorable: "bg-emerald-50 text-emerald-700 border-emerald-200",
  Neutral: "bg-slate-100 text-slate-600 border-slate-200",
  Disliked: "bg-amber-50 text-amber-700 border-amber-200",
  Hated: "bg-red-50 text-red-700 border-red-200",
  favorable: "bg-emerald-50 text-emerald-700 border-emerald-200",
  neutral: "bg-slate-100 text-slate-600 border-slate-200",
  disliked: "bg-amber-50 text-amber-700 border-amber-200",
  hated: "bg-red-50 text-red-700 border-red-200",
};

const SEVERITY_STYLES: Record<string, string> = {
  hard: "bg-red-50 text-red-700 border-red-200",
  soft: "bg-blue-50 text-blue-700 border-blue-200",
};
const SEVERITY_DOTS: Record<string, string> = {
  hard: "bg-red-500",
  soft: "bg-blue-500",
};

export default function GroupDetailPage() {
  const params = useParams();
  const groupId = params.groupId as string;

  const { currentSpaceId } = useSpaceStore();
  const { adminGroupId, enterAdminMode, exitAdminMode } = useAuthStore();

  // Cleanup admin mode on unmount
  useEffect(() => {
    return () => { exitAdminMode(); };
  }, []);

  // Reset to schedule tab when admin mode exits and we're on an admin-only tab
  useEffect(() => {
    if (adminGroupId !== groupId && ADMIN_ONLY_TABS.includes(activeTab)) {
      setActiveTab("schedule");
    }
  }, [adminGroupId]);

  const [group, setGroup] = useState<GroupWithMemberCountDto | null>(null);
  const [notFound, setNotFound] = useState(false);
  const [members, setMembers] = useState<GroupMemberDto[]>([]);
  const [activeTab, setActiveTab] = useState<ActiveTab>("schedule");
  const [loading, setLoading] = useState(true);
  const [membersLoading, setMembersLoading] = useState(false);
  const [addEmail, setAddEmail] = useState("");
  const [addError, setAddError] = useState<string | null>(null);
  const [settingsError, setSettingsError] = useState<string | null>(null);
  const [settingsSaved, setSettingsSaved] = useState(false);
  const [solverHorizon, setSolverHorizon] = useState(14);
  const [savingSettings, setSavingSettings] = useState(false);
  const [scheduleData, setScheduleData] = useState<ScheduleAssignment[] | null>(null);
  const [scheduleLoading, setScheduleLoading] = useState(false);
  const [scheduleError, setScheduleError] = useState<string | null>(null);
  const [membersError, setMembersError] = useState<string | null>(null);
  const [taskTypes, setTaskTypes] = useState<TaskTypeDto[]>([]);
  const [taskSlots, setTaskSlots] = useState<TaskSlotDto[]>([]);
  const [tasksLoading, setTasksLoading] = useState(false);
  const [constraintsLoading, setConstraintsLoading] = useState(false);
  const [constraints, setConstraints] = useState<ConstraintDto[]>([]);
  const [tasksSubTab, setTasksSubTab] = useState<"types" | "slots">("types");
  const [removeErrors, setRemoveErrors] = useState<Record<string, string>>({});

  // Fetch group on mount
  useEffect(() => {
    if (!currentSpaceId) { setLoading(false); return; }
    getGroups(currentSpaceId)
      .then((groups) => {
        const found = groups.find((g) => g.id === groupId) ?? null;
        if (found) {
          setGroup(found);
          setSolverHorizon(found.solverHorizonDays);
        } else {
          setNotFound(true);
        }
      })
      .catch(() => setNotFound(true))
      .finally(() => setLoading(false));
  }, [currentSpaceId]);

  // Fetch schedule when schedule tab is active
  useEffect(() => {
    if (activeTab !== "schedule" || !currentSpaceId) return;
    setScheduleLoading(true);
    setScheduleError(null);
    apiClient.get(`/spaces/${currentSpaceId}/groups/${groupId}/schedule`)
      .then(r => setScheduleData(r.data))
      .catch(() => setScheduleError("שגיאה בטעינת הסידור"))
      .finally(() => setScheduleLoading(false));
  }, [activeTab, currentSpaceId]);

  // Fetch members when members tab is active
  useEffect(() => {
    if ((activeTab !== "members-readonly" && activeTab !== "members-edit") || !currentSpaceId) return;
    fetchMembers();
  }, [activeTab, currentSpaceId]);

  // Fetch tasks when tasks tab is active
  useEffect(() => {
    if (activeTab !== "tasks" || !currentSpaceId) return;
    setTasksLoading(true);
    Promise.all([getTaskTypes(currentSpaceId), getTaskSlots(currentSpaceId)])
      .then(([types, slots]) => { setTaskTypes(types); setTaskSlots(slots); })
      .finally(() => setTasksLoading(false));
  }, [activeTab, currentSpaceId]);

  // Fetch constraints when constraints tab is active
  useEffect(() => {
    if (activeTab !== "constraints" || !currentSpaceId) return;
    setConstraintsLoading(true);
    getConstraints(currentSpaceId)
      .then(setConstraints)
      .finally(() => setConstraintsLoading(false));
  }, [activeTab, currentSpaceId]);

  async function fetchMembers() {
    if (!currentSpaceId) return;
    setMembersLoading(true);
    setMembersError(null);
    try {
      const data = await getGroupMembers(currentSpaceId, groupId);
      setMembers(data);
    } catch {
      setMembersError("שגיאה בטעינת החברים");
    } finally {
      setMembersLoading(false);
    }
  }

  async function handleAddMember(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId || !addEmail.trim()) return;
    setAddError(null);
    try {
      await addGroupMemberByEmail(currentSpaceId, groupId, addEmail.trim());
      await fetchMembers();
      setAddEmail("");
    } catch (err: any) {
      setAddError(err?.response?.data?.message ?? "שגיאה");
    }
  }

  async function handleRemoveMember(personId: string) {
    if (!currentSpaceId) return;
    setRemoveErrors(prev => { const n = { ...prev }; delete n[personId]; return n; });
    try {
      await removeGroupMember(currentSpaceId, groupId, personId);
      await fetchMembers();
    } catch (err: any) {
      setRemoveErrors(prev => ({ ...prev, [personId]: err?.response?.data?.message ?? "שגיאה" }));
    }
  }

  async function handleSaveSettings() {
    if (!currentSpaceId) return;
    setSavingSettings(true);
    setSettingsError(null);
    setSettingsSaved(false);
    try {
      await updateGroupSettings(currentSpaceId, groupId, solverHorizon);
      setSettingsSaved(true);
      setTimeout(() => setSettingsSaved(false), 3000);
    } catch (err: any) {
      setSettingsError(err?.response?.data?.message ?? "שגיאה בשמירת ההגדרות");
    } finally {
      setSavingSettings(false);
    }
  }

  const isAdmin = adminGroupId === groupId;

  const baseTabs: { value: ActiveTab; label: string }[] = [
    { value: "schedule", label: "סידור" },
    { value: "members-readonly", label: "חברים" },
  ];
  const adminTabs: { value: ActiveTab; label: string }[] = [
    { value: "members-edit", label: "חברים ✎" },
    { value: "tasks", label: "משימות" },
    { value: "constraints", label: "אילוצים" },
    { value: "settings", label: "הגדרות" },
  ];
  const visibleTabs = isAdmin ? [...baseTabs, ...adminTabs] : baseTabs;

  function renderTabPanel() {
    switch (activeTab) {
      case "schedule":
        return renderSchedulePanel();
      case "members-readonly":
        return renderMembersReadOnly();
      case "members-edit":
        return renderMembersEdit();
      case "tasks":
        return renderTasksPanel();
      case "constraints":
        return renderConstraintsPanel();
      case "settings":
        return renderSettingsPanel();
      default:
        return null;
    }
  }

  function renderSchedulePanel() {
    if (scheduleLoading) {
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
    if (scheduleError) {
      return <p className="text-sm text-red-600 py-4">{scheduleError}</p>;
    }
    if (!scheduleData || scheduleData.length === 0) {
      return <p className="text-sm text-slate-400 py-8 text-center">אין סידור פורסם לקבוצה זו</p>;
    }
    return (
      <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-slate-100 bg-slate-50/80">
              <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">שם</th>
              <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">סוג משימה</th>
              <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">התחלה</th>
              <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">סיום</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {scheduleData.map((a: ScheduleAssignment, i: number) => (
              <tr key={i} className="hover:bg-slate-50/60">
                <td className="px-4 py-3.5 font-medium text-slate-900">{a.personName}</td>
                <td className="px-4 py-3.5 text-slate-600">{a.taskTypeName}</td>
                <td className="px-4 py-3.5 text-slate-500 text-xs">{new Date(a.startsAt).toLocaleString("he-IL")}</td>
                <td className="px-4 py-3.5 text-slate-500 text-xs">{new Date(a.endsAt).toLocaleString("he-IL")}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  }

  function renderMembersReadOnly() {
    if (membersLoading) {
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
    if (membersError) return <p className="text-sm text-red-600 py-4">{membersError}</p>;
    if (members.length === 0) return <p className="text-sm text-slate-400 py-8 text-center">אין חברים בקבוצה זו</p>;
    return (
      <div className="space-y-2">
        {members.map(m => (
          <div key={m.personId} className="flex items-center gap-3 bg-white border border-slate-200 rounded-xl px-4 py-3">
            <div className="w-8 h-8 rounded-full bg-blue-50 flex items-center justify-center text-blue-600 text-sm font-semibold">
              {(m.displayName ?? m.fullName).charAt(0)}
            </div>
            <span className="text-sm font-medium text-slate-900">{m.displayName ?? m.fullName}</span>
          </div>
        ))}
      </div>
    );
  }

  function renderMembersEdit() {
    return (
      <div className="space-y-4">
        {/* Add member form */}
        <form onSubmit={handleAddMember} className="flex gap-2 max-w-sm">
          <input
            type="email"
            value={addEmail}
            onChange={e => setAddEmail(e.target.value)}
            placeholder="הוסף לפי אימייל"
            className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          <button type="submit"
            className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl whitespace-nowrap transition-colors">
            הוסף
          </button>
        </form>
        {addError && <p className="text-sm text-red-600">{addError}</p>}

        {membersLoading ? (
          <div className="flex items-center gap-3 text-slate-400 text-sm py-4">
            <svg className="animate-spin h-5 w-5 text-blue-400" fill="none" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
            </svg>
            טוען...
          </div>
        ) : membersError ? (
          <p className="text-sm text-red-600">{membersError}</p>
        ) : members.length === 0 ? (
          <p className="text-sm text-slate-400 py-4 text-center">אין חברים בקבוצה זו</p>
        ) : (
          <div className="space-y-2">
            {members.map(m => (
              <div key={m.personId} className="flex items-center justify-between bg-white border border-slate-200 rounded-xl px-4 py-3">
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 rounded-full bg-blue-50 flex items-center justify-center text-blue-600 text-sm font-semibold">
                    {(m.displayName ?? m.fullName).charAt(0)}
                  </div>
                  <span className="text-sm font-medium text-slate-900">{m.displayName ?? m.fullName}</span>
                </div>
                <div className="flex items-center gap-2">
                  {removeErrors[m.personId] && (
                    <span className="text-xs text-red-600">{removeErrors[m.personId]}</span>
                  )}
                  <button
                    onClick={() => handleRemoveMember(m.personId)}
                    className="text-xs text-red-500 hover:text-red-700 border border-red-200 hover:border-red-400 px-2.5 py-1 rounded-lg transition-colors">
                    הסר
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    );
  }

  function renderTasksPanel() {
    if (tasksLoading) {
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
    return (
      <div className="space-y-4">
        {/* Sub-tabs */}
        <div className="flex gap-1 border-b border-slate-200">
          {(["types", "slots"] as const).map(t => (
            <button key={t} onClick={() => setTasksSubTab(t)}
              className={`px-4 py-2.5 text-sm font-medium border-b-2 -mb-px transition-colors ${
                tasksSubTab === t ? "border-blue-500 text-blue-600" : "border-transparent text-slate-500 hover:text-slate-700"
              }`}>
              {t === "types" ? "סוגי משימות" : "חלונות זמן"}
            </button>
          ))}
        </div>

        {tasksSubTab === "types" && (
          <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-100 bg-slate-50/80">
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">שם</th>
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">עומס</th>
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">עדיפות</th>
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">חפיפה</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {taskTypes.map(tt => (
                  <tr key={tt.id} className="hover:bg-slate-50/60">
                    <td className="px-4 py-3.5 font-medium text-slate-900">{tt.name}</td>
                    <td className="px-4 py-3.5">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border ${burdenColors[tt.burdenLevel] ?? "bg-slate-100 text-slate-600 border-slate-200"}`}>
                        {burdenLabels[tt.burdenLevel] ?? tt.burdenLevel}
                      </span>
                    </td>
                    <td className="px-4 py-3.5 text-slate-500">{tt.defaultPriority}</td>
                    <td className="px-4 py-3.5 text-slate-500">{tt.allowsOverlap ? "כן" : "לא"}</td>
                  </tr>
                ))}
                {taskTypes.length === 0 && (
                  <tr><td colSpan={4} className="px-4 py-12 text-center text-slate-400 text-sm">אין נתונים</td></tr>
                )}
              </tbody>
            </table>
          </div>
        )}

        {tasksSubTab === "slots" && (
          <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-100 bg-slate-50/80">
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">סוג משימה</th>
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">התחלה</th>
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">סיום</th>
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">כוח אדם</th>
                  <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">סטטוס</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {taskSlots.map(s => (
                  <tr key={s.id} className="hover:bg-slate-50/60">
                    <td className="px-4 py-3.5 font-medium text-slate-900">{s.taskTypeName}</td>
                    <td className="px-4 py-3.5 text-slate-500 text-xs">{new Date(s.startsAt).toLocaleString("he-IL")}</td>
                    <td className="px-4 py-3.5 text-slate-500 text-xs">{new Date(s.endsAt).toLocaleString("he-IL")}</td>
                    <td className="px-4 py-3.5 text-slate-500">{s.requiredHeadcount}</td>
                    <td className="px-4 py-3.5 text-slate-500">{s.status}</td>
                  </tr>
                ))}
                {taskSlots.length === 0 && (
                  <tr><td colSpan={5} className="px-4 py-12 text-center text-slate-400 text-sm">אין נתונים</td></tr>
                )}
              </tbody>
            </table>
          </div>
        )}
      </div>
    );
  }

  function renderConstraintsPanel() {
    if (constraintsLoading) {
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
    return (
      <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-slate-100 bg-slate-50/80">
              <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">סוג כלל</th>
              <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">היקף</th>
              <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">חומרה</th>
              <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">Payload</th>
              <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">פעיל</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {constraints.map(c => (
              <tr key={c.id} className="hover:bg-slate-50/60">
                <td className="px-4 py-3.5 font-mono text-xs text-slate-700">{c.ruleType}</td>
                <td className="px-4 py-3.5 text-slate-500">{c.scopeType}</td>
                <td className="px-4 py-3.5">
                  <span className={`inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-xs font-medium border ${SEVERITY_STYLES[c.severity] ?? "bg-slate-100 text-slate-600 border-slate-200"}`}>
                    <span className={`w-1.5 h-1.5 rounded-full ${SEVERITY_DOTS[c.severity] ?? "bg-slate-400"}`} />
                    {c.severity}
                  </span>
                </td>
                <td className="px-4 py-3.5 font-mono text-xs text-slate-500 max-w-xs truncate">{c.rulePayloadJson}</td>
                <td className="px-4 py-3.5">
                  <span className={`text-xs font-medium ${c.isActive ? "text-emerald-600" : "text-slate-400"}`}>
                    {c.isActive ? "כן" : "לא"}
                  </span>
                </td>
              </tr>
            ))}
            {constraints.length === 0 && (
              <tr><td colSpan={5} className="px-4 py-12 text-center text-slate-400 text-sm">אין אילוצים</td></tr>
            )}
          </tbody>
        </table>
      </div>
    );
  }

  function renderSettingsPanel() {
    return (
      <div className="max-w-sm space-y-5">
        <div>
          <label className="block text-sm font-medium text-slate-700 mb-2">
            אופק תכנון הסולבר
          </label>
          <div className="flex items-center gap-4">
            <input
              type="range"
              min={1}
              max={90}
              value={solverHorizon}
              onChange={e => setSolverHorizon(Number(e.target.value))}
              className="flex-1"
            />
            <span className="text-sm font-semibold text-slate-900 w-20 text-center">
              {solverHorizon} ימים
            </span>
          </div>
          {solverHorizon > 30 && (
            <p className="mt-2 text-sm text-amber-700 bg-amber-50 border border-amber-200 rounded-xl px-3 py-2">
              ⚠️ אופק זמן ארוך מגדיל משמעותית את זמן החישוב. מעל 14 ימים — מומלץ להשתמש בזהירות.
            </p>
          )}
        </div>
        <div className="flex items-center gap-3">
          <button
            onClick={handleSaveSettings}
            disabled={savingSettings}
            className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
            {savingSettings ? "שומר..." : "שמור"}
          </button>
          {settingsSaved && (
            <span className="text-sm text-emerald-600 font-medium">נשמר בהצלחה</span>
          )}
        </div>
        {settingsError && <p className="text-sm text-red-600">{settingsError}</p>}
      </div>
    );
  }

  return (
    <AppShell>
      {loading ? (
        <div className="flex items-center gap-3 text-slate-400 text-sm py-8">
          <svg className="animate-spin h-5 w-5 text-blue-400" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
          טוען...
        </div>
      ) : notFound ? (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <h1 className="text-2xl font-bold text-slate-900 mb-4">קבוצה לא נמצאה</h1>
          <Link href="/groups" className="text-sm text-blue-500 hover:text-blue-700">
            ← חזרה לקבוצות
          </Link>
        </div>
      ) : group ? (
        <div className="max-w-4xl space-y-6">
          {/* Header */}
          <div className="flex items-center justify-between">
            <div>
              <div className="flex items-center gap-2 mb-1">
                <Link href="/groups" className="text-sm text-slate-400 hover:text-slate-600">
                  ← קבוצות
                </Link>
              </div>
              <h1 className="text-2xl font-bold text-slate-900">{group.name}</h1>
              <p className="text-sm text-slate-500 mt-1">{group.memberCount} חברים</p>
            </div>
            <button
              onClick={() => isAdmin ? exitAdminMode() : enterAdminMode(groupId)}
              className={`flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-medium border transition-colors ${
                isAdmin
                  ? "bg-amber-50 border-amber-200 text-amber-800 hover:bg-amber-100"
                  : "bg-white border-slate-200 text-slate-600 hover:bg-slate-50"
              }`}
            >
              <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
              </svg>
              {isAdmin ? "יציאה ממצב מנהל" : "כניסה למצב מנהל"}
            </button>
          </div>

          {/* Tab bar */}
          <div className="flex gap-1 border-b border-slate-200">
            {visibleTabs.map(tab => (
              <button
                key={tab.value}
                onClick={() => setActiveTab(tab.value)}
                className={`px-4 py-2.5 text-sm font-medium border-b-2 -mb-px transition-colors ${
                  activeTab === tab.value
                    ? "border-blue-500 text-blue-600"
                    : "border-transparent text-slate-500 hover:text-slate-700"
                }`}
              >
                {tab.label}
              </button>
            ))}
          </div>

          {/* Tab panel */}
          <div>{renderTabPanel()}</div>
        </div>
      ) : null}
    </AppShell>
  );
}
