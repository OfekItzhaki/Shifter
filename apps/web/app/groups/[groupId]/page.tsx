"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import AppShell from "@/components/shell/AppShell";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useAuthStore } from "@/lib/store/authStore";
import { useRouter } from "next/navigation";
import {
  getGroups, getGroupMembers, addGroupMemberByEmail, removeGroupMember,
  updateGroupSettings, renameGroup, softDeleteGroup, restoreGroup,
  getDeletedGroups, initiateOwnershipTransfer, cancelOwnershipTransfer,
  GroupWithMemberCountDto, GroupMemberDto, DeletedGroupDto,
} from "@/lib/api/groups";
import { getAvatarColor, getAvatarLetter } from "@/lib/utils/groupAvatar";
import { getTaskTypes, getTaskSlots, createTaskType, createTaskSlot, TaskTypeDto, TaskSlotDto } from "@/lib/api/tasks";
import { getConstraints, createConstraint, ConstraintDto } from "@/lib/api/constraints";
import { apiClient } from "@/lib/api/client";

type ActiveTab = "schedule" | "members" | "tasks" | "constraints" | "settings";

const ADMIN_ONLY_TABS: ActiveTab[] = ["tasks", "constraints", "settings"];

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
  const router = useRouter();

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
  const [newGroupName, setNewGroupName] = useState("");
  const [renameSaving, setRenameSaving] = useState(false);
  const [renameError, setRenameError] = useState<string | null>(null);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteSaving, setDeleteSaving] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [deletedGroups, setDeletedGroups] = useState<DeletedGroupDto[]>([]);
  const [deletedGroupsLoading, setDeletedGroupsLoading] = useState(false);
  const [transferPersonId, setTransferPersonId] = useState("");
  const [transferSaving, setTransferSaving] = useState(false);
  const [transferError, setTransferError] = useState<string | null>(null);
  const [hasPendingTransfer, setHasPendingTransfer] = useState(false);
  const [cancelTransferSaving, setCancelTransferSaving] = useState(false);

  // Task type create form state
  const [showTaskTypeForm, setShowTaskTypeForm] = useState(false);
  const [newTaskTypeName, setNewTaskTypeName] = useState("");
  const [newTaskTypeBurden, setNewTaskTypeBurden] = useState("Neutral");
  const [newTaskTypePriority, setNewTaskTypePriority] = useState(5);
  const [newTaskTypeOverlap, setNewTaskTypeOverlap] = useState(false);
  const [taskTypeSaving, setTaskTypeSaving] = useState(false);
  const [taskTypeError, setTaskTypeError] = useState<string | null>(null);

  // Task slot create form state
  const [showSlotForm, setShowSlotForm] = useState(false);
  const [newSlotTypeId, setNewSlotTypeId] = useState("");
  const [newSlotStart, setNewSlotStart] = useState("");
  const [newSlotEnd, setNewSlotEnd] = useState("");
  const [newSlotHeadcount, setNewSlotHeadcount] = useState(1);
  const [slotSaving, setSlotSaving] = useState(false);
  const [slotError, setSlotError] = useState<string | null>(null);

  // Constraint create form state
  const [showConstraintForm, setShowConstraintForm] = useState(false);
  const [newConstraintScope, setNewConstraintScope] = useState("group");
  const [newConstraintSeverity, setNewConstraintSeverity] = useState("hard");
  const [newConstraintRuleType, setNewConstraintRuleType] = useState("min_rest_hours");
  const [newConstraintPayload, setNewConstraintPayload] = useState('{"hours": 8}');
  const [constraintSaving, setConstraintSaving] = useState(false);
  const [constraintError, setConstraintError] = useState<string | null>(null);

  // Fetch group on mount
  useEffect(() => {
    if (!currentSpaceId) { setLoading(false); return; }
    getGroups(currentSpaceId)
      .then((groups) => {
        const found = groups.find((g) => g.id === groupId) ?? null;
        if (found) {
          setGroup(found);
          setSolverHorizon(found.solverHorizonDays);
          setNewGroupName(found.name);
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
    if (activeTab !== "members" || !currentSpaceId) return;
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

  // Fetch deleted groups when settings tab opens
  useEffect(() => {
    if (activeTab !== "settings" || !currentSpaceId || adminGroupId !== groupId) return;
    setDeletedGroupsLoading(true);
    getDeletedGroups(currentSpaceId)
      .then(setDeletedGroups)
      .finally(() => setDeletedGroupsLoading(false));
  }, [activeTab, currentSpaceId, adminGroupId, groupId]);

  async function handleRenameGroup() {
    if (!currentSpaceId || !newGroupName.trim()) return;
    setRenameSaving(true); setRenameError(null);
    try {
      await renameGroup(currentSpaceId, groupId, newGroupName.trim());
      setGroup(prev => prev ? { ...prev, name: newGroupName.trim() } : prev);
    } catch (err: any) {
      setRenameError(err?.response?.data?.message ?? "שגיאה בשינוי השם");
    } finally { setRenameSaving(false); }
  }

  async function handleDeleteGroup() {
    if (!currentSpaceId) return;
    setDeleteSaving(true); setDeleteError(null);
    try {
      await softDeleteGroup(currentSpaceId, groupId);
      router.push("/groups");
    } catch (err: any) {
      setDeleteError(err?.response?.data?.message ?? "שגיאה במחיקת הקבוצה");
      setDeleteSaving(false);
    }
  }

  async function handleRestoreGroup(deletedGroupId: string) {
    if (!currentSpaceId) return;
    try {
      await restoreGroup(currentSpaceId, deletedGroupId);
      const updated = await getDeletedGroups(currentSpaceId);
      setDeletedGroups(updated);
    } catch (err: any) {
      alert(err?.response?.data?.message ?? "שגיאה בשחזור הקבוצה");
    }
  }

  async function handleInitiateTransfer() {
    if (!currentSpaceId || !transferPersonId) return;
    setTransferSaving(true); setTransferError(null);
    try {
      await initiateOwnershipTransfer(currentSpaceId, groupId, transferPersonId);
      setHasPendingTransfer(true);
    } catch (err: any) {
      setTransferError(err?.response?.data?.message ?? "שגיאה בהעברת הבעלות");
    } finally { setTransferSaving(false); }
  }

  async function handleCancelTransfer() {
    if (!currentSpaceId) return;
    setCancelTransferSaving(true);
    try {
      await cancelOwnershipTransfer(currentSpaceId, groupId);
      setHasPendingTransfer(false);
    } catch (err: any) {
      alert(err?.response?.data?.message ?? "שגיאה בביטול ההעברה");
    } finally { setCancelTransferSaving(false); }
  }

  async function handleCreateTaskType(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId || !newTaskTypeName.trim()) return;
    setTaskTypeSaving(true); setTaskTypeError(null);
    try {
      await createTaskType(currentSpaceId, newTaskTypeName.trim(), null, newTaskTypeBurden, newTaskTypePriority, newTaskTypeOverlap);
      const updated = await getTaskTypes(currentSpaceId);
      setTaskTypes(updated);
      setNewTaskTypeName(""); setShowTaskTypeForm(false);
    } catch (err: any) {
      setTaskTypeError(err?.response?.data?.message ?? "שגיאה");
    } finally { setTaskTypeSaving(false); }
  }

  async function handleCreateSlot(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId || !newSlotTypeId || !newSlotStart || !newSlotEnd) return;
    setSlotSaving(true); setSlotError(null);
    try {
      await createTaskSlot(currentSpaceId, newSlotTypeId, new Date(newSlotStart).toISOString(), new Date(newSlotEnd).toISOString(), newSlotHeadcount, 5, null);
      const updated = await getTaskSlots(currentSpaceId);
      setTaskSlots(updated);
      setNewSlotTypeId(""); setNewSlotStart(""); setNewSlotEnd(""); setNewSlotHeadcount(1); setShowSlotForm(false);
    } catch (err: any) {
      setSlotError(err?.response?.data?.message ?? "שגיאה");
    } finally { setSlotSaving(false); }
  }

  async function handleCreateConstraint(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId) return;
    setConstraintSaving(true); setConstraintError(null);
    try {
      await createConstraint(currentSpaceId, newConstraintScope, null, newConstraintSeverity, newConstraintRuleType, newConstraintPayload, null, null);
      const updated = await getConstraints(currentSpaceId);
      setConstraints(updated);
      setShowConstraintForm(false);
    } catch (err: any) {
      setConstraintError(err?.response?.data?.message ?? "שגיאה");
    } finally { setConstraintSaving(false); }
  }

  const isAdmin = adminGroupId === groupId;

  const baseTabs: { value: ActiveTab; label: string }[] = [
    { value: "schedule", label: "סידור" },
    { value: "members", label: "חברים" },
  ];
  const adminTabs: { value: ActiveTab; label: string }[] = [
    { value: "tasks", label: "משימות" },
    { value: "constraints", label: "אילוצים" },
    { value: "settings", label: "הגדרות" },
  ];
  const visibleTabs = isAdmin ? [...baseTabs, ...adminTabs] : baseTabs;

  function renderTabPanel() {
    switch (activeTab) {
      case "schedule":
        return renderSchedulePanel();
      case "members":
        return isAdmin ? renderMembersEdit() : renderMembersReadOnly();
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
                  {m.isOwner && (
                    <span className="text-xs text-amber-600 bg-amber-50 border border-amber-200 px-2 py-0.5 rounded-full">בעלים</span>
                  )}
                </div>
                <div className="flex items-center gap-2">
                  {removeErrors[m.personId] && (
                    <span className="text-xs text-red-600">{removeErrors[m.personId]}</span>
                  )}
                  {!m.isOwner && (
                    <button
                      onClick={() => handleRemoveMember(m.personId)}
                      className="text-xs text-red-500 hover:text-red-700 border border-red-200 hover:border-red-400 px-2.5 py-1 rounded-lg transition-colors">
                      הסר
                    </button>
                  )}
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

    const inp = "border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500";

    return (
      <div className="space-y-4">
        {/* Sub-tabs + add button */}
        <div className="flex items-center justify-between border-b border-slate-200">
          <div className="flex gap-1">
            {(["types", "slots"] as const).map(t => (
              <button key={t} onClick={() => setTasksSubTab(t)}
                className={`px-4 py-2.5 text-sm font-medium border-b-2 -mb-px transition-colors ${
                  tasksSubTab === t ? "border-blue-500 text-blue-600" : "border-transparent text-slate-500 hover:text-slate-700"
                }`}>
                {t === "types" ? "סוגי משימות" : "חלונות זמן"}
              </button>
            ))}
          </div>
          {isAdmin && tasksSubTab === "types" && (
            <button onClick={() => { setShowTaskTypeForm(v => !v); setShowSlotForm(false); }}
              className="flex items-center gap-2 bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-3.5 py-2 rounded-xl mb-1 transition-colors">
              <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
              </svg>
              + סוג משימה
            </button>
          )}
          {isAdmin && tasksSubTab === "slots" && (
            <button onClick={() => { setShowSlotForm(v => !v); setShowTaskTypeForm(false); }}
              className="flex items-center gap-2 bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-3.5 py-2 rounded-xl mb-1 transition-colors">
              <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
              </svg>
              + חלון זמן
            </button>
          )}
        </div>

        {/* Task type create form */}
        {isAdmin && tasksSubTab === "types" && showTaskTypeForm && (
          <form onSubmit={handleCreateTaskType} className="bg-white border border-slate-200 rounded-2xl p-5 space-y-4 shadow-sm">
            <h2 className="text-sm font-semibold text-slate-900">סוג משימה חדש</h2>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">שם *</label>
                <input value={newTaskTypeName} onChange={e => setNewTaskTypeName(e.target.value)} required
                  className={`w-full ${inp}`} placeholder="לדוגמה: עמדה 1" />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">עומס</label>
                <select value={newTaskTypeBurden} onChange={e => setNewTaskTypeBurden(e.target.value)} className={`w-full ${inp}`}>
                  {["Favorable", "Neutral", "Disliked", "Hated"].map(b => (
                    <option key={b} value={b}>{burdenLabels[b] ?? b}</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">עדיפות (1–10)</label>
                <input type="number" min={1} max={10} value={newTaskTypePriority}
                  onChange={e => setNewTaskTypePriority(Number(e.target.value))} className={`w-full ${inp}`} />
              </div>
            </div>
            <label className="flex items-center gap-2.5 text-sm text-slate-700 cursor-pointer">
              <input type="checkbox" checked={newTaskTypeOverlap} onChange={e => setNewTaskTypeOverlap(e.target.checked)} className="w-4 h-4 rounded" />
              מאפשר חפיפה
            </label>
            {taskTypeError && <p className="text-sm text-red-600">{taskTypeError}</p>}
            <div className="flex gap-2">
              <button type="submit" disabled={taskTypeSaving}
                className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
                {taskTypeSaving ? "שומר..." : "שמור"}
              </button>
              <button type="button" onClick={() => setShowTaskTypeForm(false)}
                className="text-sm text-slate-500 hover:text-slate-700 px-3">ביטול</button>
            </div>
          </form>
        )}

        {/* Task slot create form */}
        {isAdmin && tasksSubTab === "slots" && showSlotForm && (
          <form onSubmit={handleCreateSlot} className="bg-white border border-slate-200 rounded-2xl p-5 space-y-4 shadow-sm">
            <h2 className="text-sm font-semibold text-slate-900">חלון זמן חדש</h2>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">סוג משימה *</label>
                <select value={newSlotTypeId} onChange={e => setNewSlotTypeId(e.target.value)} required className={`w-full ${inp}`}>
                  <option value="">בחר סוג...</option>
                  {taskTypes.map(tt => <option key={tt.id} value={tt.id}>{tt.name}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">כוח אדם נדרש</label>
                <input type="number" min={1} value={newSlotHeadcount}
                  onChange={e => setNewSlotHeadcount(Number(e.target.value))} className={`w-full ${inp}`} />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">התחלה *</label>
                <input type="datetime-local" value={newSlotStart} onChange={e => setNewSlotStart(e.target.value)} required className={`w-full ${inp}`} />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">סיום *</label>
                <input type="datetime-local" value={newSlotEnd} onChange={e => setNewSlotEnd(e.target.value)} required className={`w-full ${inp}`} />
              </div>
            </div>
            {slotError && <p className="text-sm text-red-600">{slotError}</p>}
            <div className="flex gap-2">
              <button type="submit" disabled={slotSaving}
                className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
                {slotSaving ? "שומר..." : "שמור"}
              </button>
              <button type="button" onClick={() => setShowSlotForm(false)}
                className="text-sm text-slate-500 hover:text-slate-700 px-3">ביטול</button>
            </div>
          </form>
        )}

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

    const inp = "border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500";

    return (
      <div className="space-y-4">
        {isAdmin && (
          <div className="flex justify-end">
            <button onClick={() => setShowConstraintForm(v => !v)}
              className="flex items-center gap-2 bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-3.5 py-2 rounded-xl transition-colors">
              <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
              </svg>
              + אילוץ
            </button>
          </div>
        )}

        {isAdmin && showConstraintForm && (
          <form onSubmit={handleCreateConstraint} className="bg-white border border-slate-200 rounded-2xl p-5 space-y-4 shadow-sm">
            <h2 className="text-sm font-semibold text-slate-900">אילוץ חדש</h2>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">סוג היקף</label>
                <select value={newConstraintScope} onChange={e => setNewConstraintScope(e.target.value)} className={`w-full ${inp}`}>
                  {["space", "group", "person", "role", "task_type"].map(s => (
                    <option key={s} value={s}>{s}</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">חומרה</label>
                <select value={newConstraintSeverity} onChange={e => setNewConstraintSeverity(e.target.value)} className={`w-full ${inp}`}>
                  <option value="hard">קשיח</option>
                  <option value="soft">רך</option>
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">סוג כלל</label>
                <select value={newConstraintRuleType} onChange={e => {
                  setNewConstraintRuleType(e.target.value);
                  const defaults: Record<string, string> = {
                    min_rest_hours: '{"hours": 8}',
                    max_kitchen_per_week: '{"max": 2, "task_type_name": "kitchen"}',
                    no_consecutive_burden: '{"burden_level": "disliked"}',
                    min_base_headcount: '{"min": 3, "window_hours": 24}',
                    no_task_type_restriction: '{"task_type_id": ""}',
                  };
                  setNewConstraintPayload(defaults[e.target.value] ?? "{}");
                }} className={`w-full ${inp}`}>
                  <option value="min_rest_hours">min_rest_hours</option>
                  <option value="max_kitchen_per_week">max_kitchen_per_week</option>
                  <option value="no_consecutive_burden">no_consecutive_burden</option>
                  <option value="min_base_headcount">min_base_headcount</option>
                  <option value="no_task_type_restriction">no_task_type_restriction</option>
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">Payload (JSON)</label>
                <input value={newConstraintPayload} onChange={e => setNewConstraintPayload(e.target.value)}
                  className={`w-full font-mono text-xs ${inp}`} />
              </div>
            </div>
            {constraintError && <p className="text-sm text-red-600">{constraintError}</p>}
            <div className="flex gap-2">
              <button type="submit" disabled={constraintSaving}
                className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
                {constraintSaving ? "שומר..." : "שמור"}
              </button>
              <button type="button" onClick={() => setShowConstraintForm(false)}
                className="text-sm text-slate-500 hover:text-slate-700 px-3">ביטול</button>
            </div>
          </form>
        )}

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
      </div>
    );
  }

  function renderSettingsPanel() {
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
                onChange={e => setNewGroupName(e.target.value)}
                className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
              <button
                onClick={handleRenameGroup}
                disabled={renameSaving}
                className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50">
                {renameSaving ? "שומר..." : "שמור"}
              </button>
            </div>
            {renameError && <p className="text-sm text-red-600 mt-2">{renameError}</p>}
          </div>
        )}

        {/* Solver horizon */}
        <div className={isAdmin ? "border-b border-slate-200 pb-5" : ""}>
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
          <div className="flex items-center gap-3 mt-3">
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
                      onClick={() => handleRestoreGroup(dg.id)}
                      className="text-xs text-emerald-600 border border-emerald-200 hover:bg-emerald-50 px-3 py-1.5 rounded-lg transition-colors">
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
                  onClick={handleCancelTransfer}
                  disabled={cancelTransferSaving}
                  className="text-sm text-slate-500 hover:text-slate-700 border border-slate-200 hover:border-slate-400 px-3 py-2 rounded-xl transition-colors disabled:opacity-50">
                  {cancelTransferSaving ? "מבטל..." : "בטל העברה"}
                </button>
              </div>
            ) : (
              <div className="space-y-3">
                <div className="flex gap-2 max-w-sm">
                  <select
                    value={transferPersonId}
                    onChange={e => setTransferPersonId(e.target.value)}
                    className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
                    <option value="">בחר חבר</option>
                    {nonOwnerMembers.map(m => (
                      <option key={m.personId} value={m.personId}>
                        {m.displayName ?? m.fullName}
                      </option>
                    ))}
                  </select>
                  <button
                    onClick={handleInitiateTransfer}
                    disabled={transferSaving || !transferPersonId}
                    className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50">
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
              <button onClick={() => setShowDeleteConfirm(true)}
                className="text-sm text-red-600 border border-red-200 hover:bg-red-50 px-4 py-2.5 rounded-xl transition-colors">
                מחק קבוצה
              </button>
            ) : (
              <div className="bg-red-50 border border-red-200 rounded-xl p-4 space-y-3">
                <p className="text-sm text-red-700">האם אתה בטוח? ניתן לשחזר תוך 30 יום</p>
                <div className="flex gap-2">
                  <button onClick={handleDeleteGroup} disabled={deleteSaving}
                    className="bg-red-500 hover:bg-red-600 text-white text-sm font-medium px-4 py-2 rounded-xl disabled:opacity-50">
                    {deleteSaving ? "מוחק..." : "כן, מחק"}
                  </button>
                  <button onClick={() => setShowDeleteConfirm(false)}
                    className="text-sm text-slate-500 hover:text-slate-700 px-3">ביטול</button>
                </div>
                {deleteError && <p className="text-sm text-red-600">{deleteError}</p>}
              </div>
            )}
          </div>
        )}
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
            <div className="flex items-center gap-4">
              <div
                className="w-12 h-12 rounded-2xl flex items-center justify-center text-white text-xl font-bold flex-shrink-0"
                style={{ background: getAvatarColor(group.name) }}
              >
                {getAvatarLetter(group.name)}
              </div>
              <div>
                <div className="flex items-center gap-2 mb-1">
                  <Link href="/groups" className="text-sm text-slate-400 hover:text-slate-600">← קבוצות</Link>
                </div>
                <h1 className="text-2xl font-bold text-slate-900">{group.name}</h1>
                <p className="text-sm text-slate-500 mt-1">{group.memberCount} חברים</p>
              </div>
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
