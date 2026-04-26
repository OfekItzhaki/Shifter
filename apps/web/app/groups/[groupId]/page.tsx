"use client";

import React, { useEffect, useState, useRef } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import AppShell from "@/components/shell/AppShell";
import Modal from "@/components/Modal";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useAuthStore } from "@/lib/store/authStore";
import { useRouter } from "next/navigation";
import {
  getGroups, getGroupMembers, addGroupMemberByEmail, removeGroupMember,
  updateGroupSettings, renameGroup, softDeleteGroup, restoreGroup,
  getDeletedGroups, initiateOwnershipTransfer, cancelOwnershipTransfer,
  GroupWithMemberCountDto, GroupMemberDto, DeletedGroupDto,
  getGroupAlerts, createGroupAlert, deleteGroupAlert, updateGroupAlert, GroupAlertDto,
  updateGroupMessage, deleteGroupMessage, pinGroupMessage,
  updatePersonInfo,
} from "@/lib/api/groups";
import { getSeverityBadge } from "@/lib/utils/alertSeverity";
import { getAvatarColor, getAvatarLetter } from "@/lib/utils/groupAvatar";
import { listGroupTasks, createGroupTask, updateGroupTask, deleteGroupTask, GroupTaskDto } from "@/lib/api/tasks";
import { getConstraints, createConstraint, updateConstraint, deleteConstraint, ConstraintDto } from "@/lib/api/constraints";
import { searchPeople, createPerson, invitePerson, PersonSearchResultDto } from "@/lib/api/people";
import { apiClient } from "@/lib/api/client";

type ActiveTab = "schedule" | "members" | "alerts" | "messages" | "tasks" | "constraints" | "settings";

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
  const [showAddMemberModal, setShowAddMemberModal] = useState(false);
  // Members search + name-first creation
  const [membersSearch, setMembersSearch] = useState("");
  const [showCreatePersonForm, setShowCreatePersonForm] = useState(false);
  const [newPersonName, setNewPersonName] = useState("");
  const [newPersonDisplayName, setNewPersonDisplayName] = useState("");
  const [createPersonSaving, setCreatePersonSaving] = useState(false);
  const [createPersonError, setCreatePersonError] = useState<string | null>(null);
  const [invitingPersonId, setInvitingPersonId] = useState<string | null>(null);
  const [inviteContact, setInviteContact] = useState("");
  const [inviteChannel, setInviteChannel] = useState<"email" | "whatsapp">("whatsapp");
  const [inviteSaving, setInviteSaving] = useState(false);
  const [inviteError, setInviteError] = useState<string | null>(null);
  const [inviteSuccess, setInviteSuccess] = useState<string | null>(null);
  // People search (global)
  const [peopleSearchQuery, setPeopleSearchQuery] = useState("");
  const [peopleSearchResults, setPeopleSearchResults] = useState<PersonSearchResultDto[]>([]);
  const [peopleSearchLoading, setPeopleSearchLoading] = useState(false);
  // Schedule person filter
  const [schedulePersonFilter, setSchedulePersonFilter] = useState("");
  const [settingsError, setSettingsError] = useState<string | null>(null);
  const [settingsSaved, setSettingsSaved] = useState(false);
  const [solverHorizon, setSolverHorizon] = useState(14);
  const [savingSettings, setSavingSettings] = useState(false);
  const [scheduleData, setScheduleData] = useState<ScheduleAssignment[] | null>(null);
  const [scheduleLoading, setScheduleLoading] = useState(false);
  const [scheduleError, setScheduleError] = useState<string | null>(null);
  const [scheduleDate, setScheduleDate] = useState<string>(new Date().toISOString().split("T")[0]);
  const [scheduleView, setScheduleView] = useState<"day" | "week">("day");
  const [membersError, setMembersError] = useState<string | null>(null);
  const [groupTasks, setGroupTasks] = useState<GroupTaskDto[]>([]);
  const [groupTasksLoading, setGroupTasksLoading] = useState(false);
  const [showTaskForm, setShowTaskForm] = useState(false);
  const [editingTask, setEditingTask] = useState<GroupTaskDto | null>(null);
  const [taskForm, setTaskForm] = useState({
    name: "", startsAt: "", endsAt: "", durationHours: 1,
    requiredHeadcount: 1, burdenLevel: "neutral",
    allowsDoubleShift: false, allowsOverlap: false,
  });
  const [taskSaving, setTaskSaving] = useState(false);
  const [taskError, setTaskError] = useState<string | null>(null);
  const [tasksLoading, setTasksLoading] = useState(false);
  const [constraintsLoading, setConstraintsLoading] = useState(false);
  const [constraints, setConstraints] = useState<ConstraintDto[]>([]);
  const [alerts, setAlerts] = useState<GroupAlertDto[]>([]);
  const [alertsLoading, setAlertsLoading] = useState(false);
  const [alertsError, setAlertsError] = useState<string | null>(null);
  const [newAlertTitle, setNewAlertTitle] = useState("");
  const [newAlertBody, setNewAlertBody] = useState("");
  const [newAlertSeverity, setNewAlertSeverity] = useState("info");
  const [alertSubmitting, setAlertSubmitting] = useState(false);
  const [alertSubmitError, setAlertSubmitError] = useState<string | null>(null);
  const [alertDeleteErrors, setAlertDeleteErrors] = useState<Record<string, string>>({});
  const [showAlertForm, setShowAlertForm] = useState(false);
  // Alert edit state
  const [editingAlertId, setEditingAlertId] = useState<string | null>(null);
  const [editAlertTitle, setEditAlertTitle] = useState("");
  const [editAlertBody, setEditAlertBody] = useState("");
  const [editAlertSeverity, setEditAlertSeverity] = useState("info");
  const [editAlertSaving, setEditAlertSaving] = useState(false);
  const [editAlertError, setEditAlertError] = useState<string | null>(null);
  // Messages state
  const [messages, setMessages] = useState<{id: string; content: string; authorName: string; createdAt: string; isPinned: boolean}[]>([]);
  const [messagesLoading, setMessagesLoading] = useState(false);
  const [messagesError, setMessagesError] = useState<string | null>(null);
  const [newMessageContent, setNewMessageContent] = useState("");
  const [messageSending, setMessageSending] = useState(false);
  const [messageError, setMessageError] = useState<string | null>(null);
  // Message edit/pin state
  const [editingMessageId, setEditingMessageId] = useState<string | null>(null);
  const [editMessageContent, setEditMessageContent] = useState("");
  const [editMessageSaving, setEditMessageSaving] = useState(false);
  const [editMessageError, setEditMessageError] = useState<string | null>(null);
  const [messagePinErrors, setMessagePinErrors] = useState<Record<string, string>>({});
  // Constraint edit state
  const [editingConstraintId, setEditingConstraintId] = useState<string | null>(null);
  const [editConstraintPayload, setEditConstraintPayload] = useState("");
  const [editConstraintFrom, setEditConstraintFrom] = useState("");
  const [editConstraintUntil, setEditConstraintUntil] = useState("");
  const [editConstraintSaving, setEditConstraintSaving] = useState(false);
  const [editConstraintError, setEditConstraintError] = useState<string | null>(null);
  const [constraintDeleteErrors, setConstraintDeleteErrors] = useState<Record<string, string>>({});
  // Solver / schedule state
  const [solverRunId, setSolverRunId] = useState<string | null>(null);
  const [solverPolling, setSolverPolling] = useState(false);
  const [solverStatus, setSolverStatus] = useState<string | null>(null);
  const [solverError, setSolverError] = useState<string | null>(null);
  const [scheduleNeedsRefresh, setScheduleNeedsRefresh] = useState(false);
  const solverPollRef = useRef<ReturnType<typeof setInterval> | null>(null);
  // Draft version state
  const [draftVersion, setDraftVersion] = useState<{ id: string; status: string } | null>(null);
  const [publishSaving, setPublishSaving] = useState(false);
  const [discardSaving, setDiscardSaving] = useState(false);
  const [scheduleVersionError, setScheduleVersionError] = useState<string | null>(null);
  const [showDiscardConfirm, setShowDiscardConfirm] = useState(false);
  // tasksSubTab removed — tasks panel now shows unified group tasks list
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

  // Constraint create form state
  const [showConstraintForm, setShowConstraintForm] = useState(false);
  const [newConstraintScope, setNewConstraintScope] = useState("group");
  const [newConstraintSeverity, setNewConstraintSeverity] = useState("hard");

  // Member profile modal state
  const [selectedMember, setSelectedMember] = useState<GroupMemberDto | null>(null);
  const [editingMemberForm, setEditingMemberForm] = useState<{fullName: string; displayName: string; phoneNumber: string; profileImageUrl: string; birthday: string} | null>(null);
  const [memberEditSaving, setMemberEditSaving] = useState(false);
  const [memberEditError, setMemberEditError] = useState<string | null>(null);
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
    // Also fetch draft version
    fetchDraftVersion();
  }, [activeTab, currentSpaceId]);

  // Re-fetch draft when scheduleNeedsRefresh is set
  useEffect(() => {
    if (!scheduleNeedsRefresh || !currentSpaceId) return;
    setScheduleNeedsRefresh(false);
    fetchDraftVersion();
    setScheduleLoading(true);
    setScheduleError(null);
    apiClient.get(`/spaces/${currentSpaceId}/groups/${groupId}/schedule`)
      .then(r => setScheduleData(r.data))
      .catch(() => setScheduleError("שגיאה בטעינת הסידור"))
      .finally(() => setScheduleLoading(false));
  }, [scheduleNeedsRefresh, currentSpaceId]);

  // Fetch members when members tab is active
  useEffect(() => {
    if (activeTab !== "members" || !currentSpaceId) return;
    fetchMembers();
  }, [activeTab, currentSpaceId]);

  // Fetch tasks when tasks tab is active
  useEffect(() => {
    if (activeTab !== "tasks" || !currentSpaceId) return;
    setGroupTasksLoading(true);
    listGroupTasks(currentSpaceId, groupId)
      .then(setGroupTasks)
      .finally(() => setGroupTasksLoading(false));
  }, [activeTab, currentSpaceId]);

  // Fetch constraints when constraints tab is active
  useEffect(() => {
    if (activeTab !== "constraints" || !currentSpaceId) return;
    setConstraintsLoading(true);
    getConstraints(currentSpaceId)
      .then(setConstraints)
      .finally(() => setConstraintsLoading(false));
  }, [activeTab, currentSpaceId]);

  // Fetch alerts when alerts tab is active
  useEffect(() => {
    if (activeTab !== "alerts" || !currentSpaceId) return;
    fetchAlerts();
  }, [activeTab, currentSpaceId]);

  // Fetch messages when messages tab is active
  useEffect(() => {
    if (activeTab !== "messages" || !currentSpaceId) return;
    fetchMessages();
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

  async function fetchAlerts() {
    if (!currentSpaceId) return;
    setAlertsLoading(true);
    setAlertsError(null);
    try {
      const data = await getGroupAlerts(currentSpaceId, groupId);
      setAlerts(data);
    } catch {
      setAlertsError("שגיאה בטעינת ההתראות");
    } finally {
      setAlertsLoading(false);
    }
  }

  async function fetchDraftVersion() {
    if (!currentSpaceId) return;
    try {
      const r = await apiClient.get(`/spaces/${currentSpaceId}/schedule-versions?status=draft`);
      const drafts: Array<{ id: string; status: string }> = r.data;
      setDraftVersion(drafts.length > 0 ? drafts[0] : null);
    } catch {
      setDraftVersion(null);
    }
  }

  async function fetchGroupTasks() {
    if (!currentSpaceId) return;
    setGroupTasksLoading(true);
    try {
      const data = await listGroupTasks(currentSpaceId, groupId);
      setGroupTasks(data);
    } finally {
      setGroupTasksLoading(false);
    }
  }

  async function fetchMessages() {
    if (!currentSpaceId) return;
    setMessagesLoading(true);
    setMessagesError(null);
    try {
      const r = await apiClient.get(`/spaces/${currentSpaceId}/groups/${groupId}/messages`);
      setMessages(r.data);
    } catch {
      setMessagesError("שגיאה בטעינת ההודעות");
    } finally {
      setMessagesLoading(false);
    }
  }

  async function handleCreateAlert(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId || !newAlertTitle.trim() || !newAlertBody.trim()) return;
    setAlertSubmitting(true);
    setAlertSubmitError(null);
    try {
      await createGroupAlert(currentSpaceId, groupId, {
        title: newAlertTitle.trim(),
        body: newAlertBody.trim(),
        severity: newAlertSeverity,
      });
      setNewAlertTitle("");
      setNewAlertBody("");
      setNewAlertSeverity("info");
      setShowAlertForm(false);
      await fetchAlerts();
    } catch (err: any) {
      setAlertSubmitError(err?.response?.data?.message ?? "שגיאה ביצירת ההתראה");
    } finally {
      setAlertSubmitting(false);
    }
  }

  async function handleDeleteAlert(alertId: string) {
    if (!currentSpaceId) return;
    setAlertDeleteErrors(prev => { const n = { ...prev }; delete n[alertId]; return n; });
    try {
      await deleteGroupAlert(currentSpaceId, groupId, alertId);
      await fetchAlerts();
    } catch (err: any) {
      setAlertDeleteErrors(prev => ({ ...prev, [alertId]: err?.response?.data?.message ?? "שגיאה" }));
    }
  }

  async function handleUpdateAlert(alertId: string) {
    if (!currentSpaceId) return;
    setEditAlertSaving(true); setEditAlertError(null);
    try {
      await updateGroupAlert(currentSpaceId, groupId, alertId, {
        title: editAlertTitle.trim(),
        body: editAlertBody.trim(),
        severity: editAlertSeverity,
      });
      setEditingAlertId(null);
      await fetchAlerts();
    } catch (err: any) {
      setEditAlertError(err?.response?.data?.message ?? "שגיאה בעדכון ההתראה");
    } finally { setEditAlertSaving(false); }
  }

  async function handleUpdateMessage(messageId: string) {
    if (!currentSpaceId) return;
    setEditMessageSaving(true); setEditMessageError(null);
    try {
      await updateGroupMessage(currentSpaceId, groupId, messageId, editMessageContent.trim());
      setEditingMessageId(null);
      await fetchMessages();
    } catch (err: any) {
      setEditMessageError(err?.response?.data?.message ?? "שגיאה בעדכון ההודעה");
    } finally { setEditMessageSaving(false); }
  }

  async function handleDeleteMessage(messageId: string) {
    if (!currentSpaceId) return;
    try {
      await deleteGroupMessage(currentSpaceId, groupId, messageId);
      setMessages(prev => prev.filter(m => m.id !== messageId));
    } catch (err: any) {
      setMessagePinErrors(prev => ({ ...prev, [messageId]: err?.response?.data?.message ?? "שגיאה" }));
    }
  }

  async function handlePinMessage(messageId: string, isPinned: boolean) {
    if (!currentSpaceId) return;
    setMessagePinErrors(prev => { const n = { ...prev }; delete n[messageId]; return n; });
    try {
      await pinGroupMessage(currentSpaceId, groupId, messageId, isPinned);
      setMessages(prev => prev.map(m => m.id === messageId ? { ...m, isPinned } : m));
    } catch (err: any) {
      setMessagePinErrors(prev => ({ ...prev, [messageId]: err?.response?.data?.message ?? "שגיאה" }));
    }
  }

  async function handleTaskFormSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId) return;
    setTaskSaving(true); setTaskError(null);
    const payload = {
      name: taskForm.name.trim(),
      startsAt: new Date(taskForm.startsAt).toISOString(),
      endsAt: new Date(taskForm.endsAt).toISOString(),
      durationHours: taskForm.durationHours,
      requiredHeadcount: taskForm.requiredHeadcount,
      burdenLevel: taskForm.burdenLevel,
      allowsDoubleShift: taskForm.allowsDoubleShift,
      allowsOverlap: taskForm.allowsOverlap,
    };
    try {
      if (editingTask) {
        await updateGroupTask(currentSpaceId, groupId, editingTask.id, payload);
      } else {
        await createGroupTask(currentSpaceId, groupId, payload);
      }
      setShowTaskForm(false);
      setEditingTask(null);
      setTaskForm({ name: "", startsAt: "", endsAt: "", durationHours: 1, requiredHeadcount: 1, burdenLevel: "neutral", allowsDoubleShift: false, allowsOverlap: false });
      await fetchGroupTasks();
    } catch (err: any) {
      setTaskError(err?.response?.data?.message ?? "שגיאה בשמירת המשימה");
    } finally { setTaskSaving(false); }
  }

  async function handleDeleteTask(taskId: string) {
    if (!currentSpaceId) return;
    if (!confirm("האם אתה בטוח שברצונך למחוק משימה זו?")) return;
    try {
      await deleteGroupTask(currentSpaceId, groupId, taskId);
      await fetchGroupTasks();
    } catch (err: any) {
      alert(err?.response?.data?.message ?? "שגיאה במחיקת המשימה");
    }
  }

  async function handleUpdateConstraint(constraintId: string) {
    if (!currentSpaceId) return;
    try { JSON.parse(editConstraintPayload); } catch {
      setEditConstraintError("Payload חייב להיות JSON תקין");
      return;
    }
    setEditConstraintSaving(true); setEditConstraintError(null);
    try {
      await updateConstraint(currentSpaceId, constraintId, {
        rulePayloadJson: editConstraintPayload,
        effectiveFrom: editConstraintFrom || null,
        effectiveUntil: editConstraintUntil || null,
      });
      setEditingConstraintId(null);
      const updated = await getConstraints(currentSpaceId);
      setConstraints(updated);
    } catch (err: any) {
      setEditConstraintError(err?.response?.data?.message ?? "שגיאה בעדכון האילוץ");
    } finally { setEditConstraintSaving(false); }
  }

  async function handleDeleteConstraint(constraintId: string) {
    if (!currentSpaceId) return;
    if (!confirm("האם אתה בטוח שברצונך למחוק אילוץ זה?")) return;
    setConstraintDeleteErrors(prev => { const n = { ...prev }; delete n[constraintId]; return n; });
    try {
      await deleteConstraint(currentSpaceId, constraintId);
      setConstraints(prev => prev.filter(c => c.id !== constraintId));
    } catch (err: any) {
      setConstraintDeleteErrors(prev => ({ ...prev, [constraintId]: err?.response?.data?.message ?? "שגיאה" }));
    }
  }

  async function handleTriggerSolver() {
    if (!currentSpaceId) return;
    setSolverError(null); setSolverStatus(null);
    try {
      const r = await apiClient.post(`/spaces/${currentSpaceId}/schedule-runs/trigger`, { triggerMode: "standard" });
      const runId: string = r.data.runId ?? r.data.id;
      setSolverRunId(runId);
      setSolverPolling(true);
      solverPollRef.current = setInterval(async () => {
        try {
          const poll = await apiClient.get(`/spaces/${currentSpaceId}/schedule-runs/${runId}`);
          const status: string = poll.data.status;
          if (status === "Completed") {
            clearInterval(solverPollRef.current!);
            setSolverPolling(false);
            setSolverStatus("Completed");
            setScheduleNeedsRefresh(true);
          } else if (status === "Failed" || status === "TimedOut") {
            clearInterval(solverPollRef.current!);
            setSolverPolling(false);
            setSolverStatus(status);
            setSolverError(status === "Failed" ? "הסידור נכשל. נסה שוב מאוחר יותר." : "הסידור פג זמן. נסה שוב.");
          }
        } catch (pollErr: any) {
          if (pollErr?.response?.status === 404) {
            clearInterval(solverPollRef.current!);
            setSolverPolling(false);
            setSolverError("לא נמצא מידע על ריצת הסידור.");
          }
        }
      }, 3000);
    } catch (err: any) {
      setSolverError(err?.response?.data?.message ?? "שגיאה בהפעלת הסידור");
    }
  }

  async function handlePublishVersion() {
    if (!currentSpaceId || !draftVersion) return;
    setPublishSaving(true); setScheduleVersionError(null);
    try {
      await apiClient.post(`/spaces/${currentSpaceId}/schedule-versions/${draftVersion.id}/publish`);
      setDraftVersion(null);
      setScheduleNeedsRefresh(true);
    } catch (err: any) {
      setScheduleVersionError(err?.response?.data?.message ?? "שגיאה בפרסום הסידור");
    } finally { setPublishSaving(false); }
  }

  async function handleDiscardVersion() {
    if (!currentSpaceId || !draftVersion) return;
    setDiscardSaving(true); setScheduleVersionError(null);
    try {
      await apiClient.delete(`/spaces/${currentSpaceId}/schedule-versions/${draftVersion.id}`);
      setDraftVersion(null);
      setShowDiscardConfirm(false);
      setScheduleNeedsRefresh(true);
    } catch (err: any) {
      setScheduleVersionError(err?.response?.data?.message ?? "שגיאה בביטול הטיוטה");
    } finally { setDiscardSaving(false); }
  }

  async function handleAddMember(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId) return;
    if (!addEmail.trim()) {
      setAddError("יש להזין אימייל או מספר טלפון");
      return;
    }
    setAddError(null);
    const input = addEmail.trim();
    const isPhone = /^[+\d][\d\s\-()\+]{6,}$/.test(input);
    try {
      if (isPhone) {
        await apiClient.post(`/spaces/${currentSpaceId}/groups/${groupId}/members/by-phone`, { phoneNumber: input });
      } else {
        await addGroupMemberByEmail(currentSpaceId, groupId, input);
      }
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

  async function handleCreatePerson(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId || !newPersonName.trim()) return;
    setCreatePersonSaving(true); setCreatePersonError(null);
    try {
      const result = await createPerson(currentSpaceId, newPersonName.trim(), newPersonDisplayName.trim() || undefined);
      await apiClient.post(`/spaces/${currentSpaceId}/groups/${groupId}/members`, { personId: result.id });
      setNewPersonName(""); setNewPersonDisplayName(""); setShowCreatePersonForm(false);
      await fetchMembers();
    } catch (err: any) {
      setCreatePersonError(err?.response?.data?.message ?? "שגיאה ביצירת האדם");
    } finally { setCreatePersonSaving(false); }
  }

  async function handleSaveMemberEdit(personId: string) {
    if (!currentSpaceId || !editingMemberForm) return;
    setMemberEditSaving(true); setMemberEditError(null);
    try {
      await updatePersonInfo(currentSpaceId, personId, {
        fullName: editingMemberForm.fullName || undefined,
        displayName: editingMemberForm.displayName || undefined,
        phoneNumber: editingMemberForm.phoneNumber || undefined,
        profileImageUrl: editingMemberForm.profileImageUrl || undefined,
        birthday: editingMemberForm.birthday || undefined,
      });
      await fetchMembers();
      setSelectedMember(null);
      setEditingMemberForm(null);
    } catch (err: any) {
      setMemberEditError(err?.response?.data?.message ?? "שגיאה בשמירת הפרטים");
    } finally { setMemberEditSaving(false); }
  }

  async function handleInvitePerson(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId || !invitingPersonId || !inviteContact.trim()) return;
    setInviteSaving(true); setInviteError(null); setInviteSuccess(null);
    try {
      await invitePerson(currentSpaceId, invitingPersonId, inviteContact.trim(), inviteChannel);
      setInviteSuccess("ההזמנה נשלחה בהצלחה!");
      setInviteContact("");
      setTimeout(() => { setInvitingPersonId(null); setInviteSuccess(null); }, 2000);
    } catch (err: any) {
      setInviteError(err?.response?.data?.message ?? "שגיאה בשליחת ההזמנה");
    } finally { setInviteSaving(false); }
  }

  async function handlePeopleSearch(q: string) {
    setPeopleSearchQuery(q);
    if (!currentSpaceId || q.trim().length < 2) { setPeopleSearchResults([]); return; }
    setPeopleSearchLoading(true);
    try {
      const results = await searchPeople(currentSpaceId, q);
      setPeopleSearchResults(results);
    } catch { setPeopleSearchResults([]); }
    finally { setPeopleSearchLoading(false); }
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

  async function handleCreateConstraint(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId) return;
    // Validate JSON payload
    try { JSON.parse(newConstraintPayload); } catch {
      setConstraintError("Payload חייב להיות JSON תקין");
      return;
    }
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
    { value: "alerts", label: "התראות" },
    { value: "messages", label: "הודעות" },
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
      case "alerts":
        return renderAlertsPanel();
      case "messages":
        return renderMessagesPanel();
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
    const today = new Date().toISOString().split("T")[0];
    const minDate = new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString().split("T")[0];
    const maxDate = new Date(Date.now() + (group?.solverHorizonDays ?? 7) * 24 * 60 * 60 * 1000).toISOString().split("T")[0];

    function prevDay() {
      const d = new Date(scheduleDate + "T00:00:00");
      d.setDate(d.getDate() - 1);
      const next = d.toISOString().split("T")[0];
      if (next >= minDate) setScheduleDate(next);
    }

    function nextDay() {
      const d = new Date(scheduleDate + "T00:00:00");
      d.setDate(d.getDate() + 1);
      const next = d.toISOString().split("T")[0];
      if (next <= maxDate) setScheduleDate(next);
    }

    function getWeekDates(): string[] {
      const dates: string[] = [];
      const start = new Date(scheduleDate + "T00:00:00");
      start.setDate(start.getDate() - start.getDay());
      for (let i = 0; i < 7; i++) {
        const d = new Date(start);
        d.setDate(start.getDate() + i);
        dates.push(d.toISOString().split("T")[0]);
      }
      return dates;
    }

    const formatDateLabel = (dateStr: string) => {
      const d = new Date(dateStr + "T00:00:00");
      const todayStr = new Date().toISOString().split("T")[0];
      const yesterdayStr = new Date(Date.now() - 86400000).toISOString().split("T")[0];
      const tomorrowStr = new Date(Date.now() + 86400000).toISOString().split("T")[0];
      if (dateStr === todayStr) return "היום";
      if (dateStr === yesterdayStr) return "אתמול";
      if (dateStr === tomorrowStr) return "מחר";
      return d.toLocaleDateString("he-IL", { weekday: "short", day: "numeric", month: "short" });
    };

    const dayAssignments = (scheduleData ?? [])
      .filter(a => a.startsAt?.startsWith(scheduleDate))
      .filter(a => !schedulePersonFilter || a.personName.toLowerCase().includes(schedulePersonFilter.toLowerCase()));

    const weekDates = getWeekDates();
    const weekAssignments = weekDates.reduce<Record<string, ScheduleAssignment[]>>((acc, d) => {
      acc[d] = (scheduleData ?? [])
        .filter(a => a.startsAt?.startsWith(d))
        .filter(a => !schedulePersonFilter || a.personName.toLowerCase().includes(schedulePersonFilter.toLowerCase()));
      return acc;
    }, {});

    return (
      <div className="space-y-4">
        {/* Draft version banner */}
        {draftVersion && (          <div className="bg-amber-50 border border-amber-200 rounded-2xl p-4">
            <div className="flex items-center justify-between gap-3">
              <div className="flex items-center gap-2">
                <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-amber-100 text-amber-800 border border-amber-300">
                  טיוטה
                </span>
                <span className="text-sm text-amber-800">סידור טיוטה מוכן לעיון ופרסום</span>
              </div>
              {isAdmin && (
                <div className="flex items-center gap-2 flex-shrink-0">
                  <button
                    onClick={handlePublishVersion}
                    disabled={publishSaving || discardSaving}
                    className="bg-emerald-500 hover:bg-emerald-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors"
                  >
                    {publishSaving ? "מפרסם..." : "פרסם סידור"}
                  </button>
                  <button
                    onClick={() => setShowDiscardConfirm(true)}
                    disabled={publishSaving || discardSaving}
                    className="text-xs text-red-600 border border-red-200 hover:bg-red-50 px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors"
                  >
                    בטל טיוטה
                  </button>
                </div>
              )}
            </div>
            {scheduleVersionError && (
              <p className="text-xs text-red-600 mt-2">{scheduleVersionError}</p>
            )}
            {/* Discard confirmation */}
            {showDiscardConfirm && (
              <div className="mt-3 bg-red-50 border border-red-200 rounded-xl p-3 space-y-2">
                <p className="text-sm text-red-700">האם אתה בטוח שברצונך לבטל את הטיוטה? פעולה זו אינה הפיכה.</p>
                <div className="flex gap-2">
                  <button
                    onClick={handleDiscardVersion}
                    disabled={discardSaving}
                    className="bg-red-500 hover:bg-red-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors"
                  >
                    {discardSaving ? "מבטל..." : "כן, בטל טיוטה"}
                  </button>
                  <button
                    onClick={() => setShowDiscardConfirm(false)}
                    className="text-xs text-slate-500 hover:text-slate-700 px-2"
                  >
                    ביטול
                  </button>
                </div>
              </div>
            )}
          </div>
        )}

        {/* Person filter search */}
        <div className="relative max-w-xs">
          <input
            type="text"
            value={schedulePersonFilter}
            onChange={e => setSchedulePersonFilter(e.target.value)}
            placeholder="סנן לפי שם..."
            className="w-full border border-slate-200 rounded-xl px-3.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 pr-9"
          />
          <svg className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>

        {/* Date navigation */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <button
              onClick={prevDay}
              disabled={scheduleDate <= minDate}
              className="p-2 rounded-lg border border-slate-200 hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            >
              <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
              </svg>
            </button>
            <button
              onClick={() => setScheduleDate(today)}
              className={`px-3 py-1.5 rounded-lg text-sm font-medium border transition-colors ${
                scheduleDate === today
                  ? "bg-blue-500 text-white border-blue-500"
                  : "border-slate-200 text-slate-600 hover:bg-slate-50"
              }`}
            >
              היום
            </button>
            <button
              onClick={nextDay}
              disabled={scheduleDate >= maxDate}
              className="p-2 rounded-lg border border-slate-200 hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            >
              <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
              </svg>
            </button>
            <span className="text-sm font-medium text-slate-700 mr-2">{formatDateLabel(scheduleDate)}</span>
          </div>

          {/* View toggle */}
          <div className="flex gap-1 bg-slate-100 p-1 rounded-lg">
            <button
              onClick={() => setScheduleView("day")}
              className={`px-3 py-1 rounded-md text-xs font-medium transition-all ${
                scheduleView === "day" ? "bg-white text-slate-900 shadow-sm" : "text-slate-500"
              }`}
            >יום</button>
            <button
              onClick={() => setScheduleView("week")}
              className={`px-3 py-1 rounded-md text-xs font-medium transition-all ${
                scheduleView === "week" ? "bg-white text-slate-900 shadow-sm" : "text-slate-500"
              }`}
            >שבוע</button>
          </div>
        </div>

        {/* Loading / error */}
        {scheduleLoading && (
          <div className="flex items-center gap-3 text-slate-400 text-sm py-8">
            <svg className="animate-spin h-5 w-5 text-blue-400" fill="none" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
            </svg>
            טוען...
          </div>
        )}
        {scheduleError && <p className="text-sm text-red-600 py-4">{scheduleError}</p>}

        {/* Day view */}
        {!scheduleLoading && !scheduleError && scheduleView === "day" && (
          dayAssignments.length === 0 ? (
            <p className="text-sm text-slate-400 py-8 text-center">אין משימות ב{formatDateLabel(scheduleDate)}</p>
          ) : (
            <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-slate-100 bg-slate-50/80">
                    <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">שם</th>
                    <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">סוג משימה</th>
                    <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">שעות</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {dayAssignments.map((a, i) => (
                    <tr key={i} className="hover:bg-slate-50/60">
                      <td className="px-4 py-3.5 font-medium text-slate-900">{a.personName}</td>
                      <td className="px-4 py-3.5 text-slate-600">{a.taskTypeName}</td>
                      <td className="px-4 py-3.5 text-slate-500 text-xs tabular-nums">
                        {new Date(a.startsAt).toLocaleTimeString("he-IL", { hour: "2-digit", minute: "2-digit" })}
                        <span className="mx-1 text-slate-300">–</span>
                        {new Date(a.endsAt).toLocaleTimeString("he-IL", { hour: "2-digit", minute: "2-digit" })}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        )}

        {/* Week view */}
        {!scheduleLoading && !scheduleError && scheduleView === "week" && (
          <div className="space-y-4">
            {weekDates.map(d => {
              const items = weekAssignments[d] ?? [];
              const isToday = d === today;
              return (
                <div key={d}>
                  <h3 className={`text-xs font-semibold uppercase tracking-wider mb-2 ${isToday ? "text-blue-600" : "text-slate-500"}`}>
                    {formatDateLabel(d)}
                    {isToday && <span className="mr-2 text-blue-500 normal-case font-normal">• היום</span>}
                  </h3>
                  {items.length === 0 ? (
                    <p className="text-xs text-slate-400 py-2 pr-2">אין משימות</p>
                  ) : (
                    <div className="space-y-1.5">
                      {items.map((a, i) => (
                        <div key={i} className="flex items-center gap-3 bg-white border border-slate-200 rounded-xl px-4 py-2.5">
                          <div className="text-xs tabular-nums text-slate-500 w-20 shrink-0">
                            {new Date(a.startsAt).toLocaleTimeString("he-IL", { hour: "2-digit", minute: "2-digit" })}
                            <span className="mx-1 text-slate-300">–</span>
                            {new Date(a.endsAt).toLocaleTimeString("he-IL", { hour: "2-digit", minute: "2-digit" })}
                          </div>
                          <div className="flex-1 min-w-0">
                            <p className="text-sm font-medium text-slate-900 truncate">{a.personName}</p>
                            <p className="text-xs text-slate-400 truncate">{a.taskTypeName}</p>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>
    );
  }

  function renderMembersReadOnly() {
    const filteredReadOnly = membersSearch.trim()
      ? members.filter(m =>
          (m.displayName ?? m.fullName).toLowerCase().includes(membersSearch.toLowerCase()) ||
          (m.phoneNumber ?? "").includes(membersSearch))
      : members;

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
      <div className="space-y-3">
        {/* Search box */}
        <div className="relative max-w-sm">
          <input
            type="text"
            value={membersSearch}
            onChange={e => setMembersSearch(e.target.value)}
            placeholder="חפש לפי שם או טלפון..."
            className="w-full border border-slate-200 rounded-xl px-3.5 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 pr-9"
          />
          <svg className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>
        <div className="space-y-2">
          {filteredReadOnly.length === 0 ? (
            <p className="text-sm text-slate-400 py-4 text-center">לא נמצאו חברים</p>
          ) : filteredReadOnly.map(m => (
            <div key={m.personId} className="flex items-center gap-3 bg-white border border-slate-200 rounded-xl px-4 py-3 cursor-pointer hover:bg-slate-50 transition-colors" onClick={() => setSelectedMember(m)}>
              <div
                className="w-8 h-8 rounded-full flex items-center justify-center text-white text-sm font-semibold flex-shrink-0"
                style={{ background: m.profileImageUrl ? "transparent" : "linear-gradient(135deg, #3b82f6, #6366f1)" }}
              >
                {m.profileImageUrl
                  ? <img src={m.profileImageUrl} alt="" style={{ width: 32, height: 32, borderRadius: "50%", objectFit: "cover" }} />
                  : (m.displayName ?? m.fullName).charAt(0)}
              </div>
              <span className="text-sm font-medium text-slate-900">{m.displayName ?? m.fullName}</span>
              {m.phoneNumber && (
                <span className="text-xs text-slate-400 mr-2">{m.phoneNumber}</span>
              )}
            </div>
          ))}
        </div>
      </div>
    );
  }

  function renderMembersEdit() {
    const inp = "border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500";
    const filteredMembers = membersSearch.trim()
      ? members.filter(m =>
          (m.displayName ?? m.fullName).toLowerCase().includes(membersSearch.toLowerCase()) ||
          (m.phoneNumber ?? "").includes(membersSearch))
      : members;

    return (
      <div className="space-y-4">
        {/* Search box */}
        <div className="relative max-w-sm">
          <input
            type="text"
            value={membersSearch}
            onChange={e => setMembersSearch(e.target.value)}
            placeholder="חפש לפי שם או טלפון..."
            className={`w-full ${inp} pr-9`}
          />
          <svg className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>

        {/* Add member buttons */}
        <div className="flex gap-2">
          <button
            onClick={() => { setShowAddMemberModal(true); setAddEmail(""); setAddError(null); }}
            className="flex items-center gap-2 bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-3.5 py-2 rounded-xl transition-colors"
          >
            <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
            </svg>
            הוסף לפי אימייל/טלפון
          </button>
          <button
            onClick={() => { setShowCreatePersonForm(true); setNewPersonName(""); setNewPersonDisplayName(""); setCreatePersonError(null); }}
            className="flex items-center gap-2 text-sm text-blue-600 hover:text-blue-800 border border-blue-200 hover:border-blue-400 px-3.5 py-2 rounded-xl transition-colors"
          >
            <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
            </svg>
            הוסף לפי שם בלבד
          </button>
        </div>

        {/* Members list */}
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
        ) : filteredMembers.length === 0 ? (
          <p className="text-sm text-slate-400 py-4 text-center">
            {membersSearch ? "לא נמצאו חברים התואמים לחיפוש" : "אין חברים בקבוצה זו"}
          </p>
        ) : (
          <div className="space-y-2">
            {filteredMembers.map(m => (
              <div key={m.personId}>
                <div className="flex items-center justify-between bg-white border border-slate-200 rounded-xl px-4 py-3">
                  <div className="flex items-center gap-3">
                    <div
                      className="w-8 h-8 rounded-full flex items-center justify-center text-white text-sm font-semibold cursor-pointer flex-shrink-0"
                      style={{ background: m.profileImageUrl ? "transparent" : "linear-gradient(135deg, #3b82f6, #6366f1)" }}
                      onClick={() => setSelectedMember(m)}
                    >
                      {m.profileImageUrl
                        ? <img src={m.profileImageUrl} alt="" style={{ width: 32, height: 32, borderRadius: "50%", objectFit: "cover" }} />
                        : (m.displayName ?? m.fullName).charAt(0)}
                    </div>
                    <div>
                      <span
                        className="text-sm font-medium text-slate-900 cursor-pointer hover:text-blue-600 transition-colors"
                        onClick={() => setSelectedMember(m)}
                      >{m.displayName ?? m.fullName}</span>
                      {m.phoneNumber && (
                        <span className="text-xs text-slate-400 mr-2">{m.phoneNumber}</span>
                      )}
                    </div>
                    {m.isOwner && (
                      <span className="text-xs text-amber-600 bg-amber-50 border border-amber-200 px-2 py-0.5 rounded-full">בעלים</span>
                    )}
                  </div>
                  <div className="flex items-center gap-2">
                    {removeErrors[m.personId] && (
                      <span className="text-xs text-red-600">{removeErrors[m.personId]}</span>
                    )}
                    {/* Invite button — only for pending members */}
                    {m.invitationStatus !== "accepted" && (
                      <button
                        onClick={() => { setInvitingPersonId(m.personId); setInviteContact(""); setInviteError(null); setInviteSuccess(null); }}
                        className="text-xs text-blue-500 hover:text-blue-700 border border-blue-200 hover:border-blue-400 px-2.5 py-1 rounded-lg transition-colors"
                      >
                        הזמן
                      </button>
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
                {/* Inline invite form removed — handled via Modal */}
              </div>
            ))}
          </div>
        )}
      </div>
    );
  }

  function renderTasksPanel() {
    if (groupTasksLoading) {
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

    const burdenOptions = [
      { value: "favorable", label: "נוח" },
      { value: "neutral", label: "ניטרלי" },
      { value: "disliked", label: "לא אהוב" },
      { value: "hated", label: "שנוא" },
    ];

    return (
      <div className="space-y-4">
        {isAdmin && (
          <div className="flex justify-end">
            <button
              onClick={() => {
                setEditingTask(null);
                const today = new Date().toISOString().split("T")[0];
                setTaskForm({ name: "", startsAt: `${today}T00:00`, endsAt: `${today}T23:59`, durationHours: 24, requiredHeadcount: 1, burdenLevel: "neutral", allowsDoubleShift: false, allowsOverlap: false });
                setTaskError(null);
                setShowTaskForm(true);
              }}
              className="flex items-center gap-2 bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-3.5 py-2 rounded-xl transition-colors"
            >
              <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
              </svg>
              הוסף משימה
            </button>
          </div>
        )}

        <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-100 bg-slate-50/80">
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">שם</th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">חלון זמן</th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">משך</th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">כוח אדם</th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">עומס</th>
                {isAdmin && <th className="px-4 py-3" />}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {groupTasks.map(t => (
                <tr key={t.id} className="hover:bg-slate-50/60">
                  <td className="px-4 py-3.5 font-medium text-slate-900">{t.name}</td>
                  <td className="px-4 py-3.5 text-slate-500 text-xs tabular-nums">
                    {new Date(t.startsAt).toLocaleString("he-IL", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" })}
                    <span className="mx-1 text-slate-300">–</span>
                    {new Date(t.endsAt).toLocaleString("he-IL", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" })}
                  </td>
                  <td className="px-4 py-3.5 text-slate-500">{t.durationHours}ש'</td>
                  <td className="px-4 py-3.5 text-slate-500">{t.requiredHeadcount}</td>
                  <td className="px-4 py-3.5">
                    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border ${burdenColors[t.burdenLevel] ?? "bg-slate-100 text-slate-600 border-slate-200"}`}>
                      {burdenLabels[t.burdenLevel] ?? t.burdenLevel}
                    </span>
                  </td>
                  {isAdmin && (
                    <td className="px-4 py-3.5">
                      <div className="flex items-center gap-2">
                        <button
                          onClick={() => {
                            setEditingTask(t);
                            setTaskForm({
                              name: t.name,
                              startsAt: t.startsAt.slice(0, 16),
                              endsAt: t.endsAt.slice(0, 16),
                              durationHours: t.durationHours,
                              requiredHeadcount: t.requiredHeadcount,
                              burdenLevel: t.burdenLevel,
                              allowsDoubleShift: t.allowsDoubleShift,
                              allowsOverlap: t.allowsOverlap,
                            });
                            setTaskError(null);
                            setShowTaskForm(true);
                          }}
                          className="text-xs text-blue-500 hover:text-blue-700 border border-blue-200 hover:border-blue-400 px-2.5 py-1 rounded-lg transition-colors"
                        >
                          ערוך
                        </button>
                        <button
                          onClick={() => handleDeleteTask(t.id)}
                          className="text-xs text-red-500 hover:text-red-700 border border-red-200 hover:border-red-400 px-2.5 py-1 rounded-lg transition-colors"
                        >
                          מחק
                        </button>
                      </div>
                    </td>
                  )}
                </tr>
              ))}
              {groupTasks.length === 0 && (
                <tr><td colSpan={isAdmin ? 6 : 5} className="px-4 py-12 text-center text-slate-400 text-sm">אין משימות לקבוצה זו</td></tr>
              )}
            </tbody>
          </table>
        </div>
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
            <button onClick={() => setShowConstraintForm(true)}
              className="flex items-center gap-2 bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-3.5 py-2 rounded-xl transition-colors">
              <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
              </svg>
              + אילוץ
            </button>
          </div>
        )}

        <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-100 bg-slate-50/80">
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">כלל</th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">פרטים</th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">חומרה</th>
                <th className="px-4 py-3 text-start text-xs font-semibold text-slate-500 uppercase tracking-wider">פעיל</th>
                {isAdmin && <th className="px-4 py-3" />}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {constraints.map(c => {
                const ruleLabels: Record<string, string> = {
                  min_rest_hours: "מינימום מנוחה",
                  max_kitchen_per_week: "מקסימום מטבח בשבוע",
                  no_consecutive_burden: "ללא עומס רצוף",
                  min_base_headcount: "מינימום כוח אדם",
                  no_task_type_restriction: "הגבלת סוג משימה",
                };
                const formatPayload = (ruleType: string, json: string) => {
                  try {
                    const p = JSON.parse(json);
                    if (ruleType === "min_rest_hours") return `${p.hours} שעות`;
                    if (ruleType === "max_kitchen_per_week") return `מקסימום ${p.max}`;
                    if (ruleType === "no_consecutive_burden") return `עומס: ${p.burden_level}`;
                    if (ruleType === "min_base_headcount") return `${p.min} אנשים / ${p.window_hours}ש'`;
                    if (ruleType === "no_task_type_restriction") return `סוג: ${p.task_type_id || "—"}`;
                    return json;
                  } catch { return json; }
                };
                return (
                  <React.Fragment key={c.id}>
                    <tr className="hover:bg-slate-50/60">
                      <td className="px-4 py-3.5 font-medium text-slate-900">{ruleLabels[c.ruleType] ?? c.ruleType}</td>
                      <td className="px-4 py-3.5 text-slate-500 text-xs">{formatPayload(c.ruleType, c.rulePayloadJson)}</td>
                      <td className="px-4 py-3.5">
                        <span className={`inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-xs font-medium border ${SEVERITY_STYLES[c.severity] ?? "bg-slate-100 text-slate-600 border-slate-200"}`}>
                          <span className={`w-1.5 h-1.5 rounded-full ${SEVERITY_DOTS[c.severity] ?? "bg-slate-400"}`} />
                          {c.severity === "hard" ? "קשיח" : c.severity === "soft" ? "רך" : c.severity}
                        </span>
                      </td>
                      <td className="px-4 py-3.5">
                        <span className={`text-xs font-medium ${c.isActive ? "text-emerald-600" : "text-slate-400"}`}>
                          {c.isActive ? "כן" : "לא"}
                        </span>
                      </td>
                      {isAdmin && (
                        <td className="px-4 py-3.5">
                          <div className="flex items-center gap-2">
                            <button
                              onClick={() => {
                                setEditingConstraintId(c.id);
                                setEditConstraintPayload(c.rulePayloadJson);
                                setEditConstraintFrom(c.effectiveFrom ?? "");
                                setEditConstraintUntil(c.effectiveUntil ?? "");
                                setEditConstraintError(null);
                              }}
                              className="text-xs text-blue-500 hover:text-blue-700 border border-blue-200 hover:border-blue-400 px-2.5 py-1 rounded-lg transition-colors"
                            >
                              ערוך
                            </button>
                            <button
                              onClick={() => handleDeleteConstraint(c.id)}
                              className="text-xs text-red-500 hover:text-red-700 border border-red-200 hover:border-red-400 px-2.5 py-1 rounded-lg transition-colors"
                            >
                              מחק
                            </button>
                          </div>
                          {constraintDeleteErrors[c.id] && (
                            <p className="text-xs text-red-600 mt-1">{constraintDeleteErrors[c.id]}</p>
                          )}
                        </td>
                      )}
                    </tr>
                    {/* Inline edit row removed — editing handled via Modal */}
                  </React.Fragment>
                );
              })}
              {constraints.length === 0 && (
                <tr><td colSpan={isAdmin ? 5 : 4} className="px-4 py-12 text-center text-slate-400 text-sm">אין אילוצים</td></tr>
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

        {/* Auto-schedule horizon */}
        <div className={isAdmin ? "border-b border-slate-200 pb-5" : ""}>
          <div className="flex items-center gap-2 mb-1">
            <label className="block text-sm font-medium text-slate-700">
              אופק סידור אוטומטי
            </label>
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
              <div className="flex items-center gap-2 text-emerald-700 bg-emerald-50 border border-emerald-200 rounded-xl px-4 py-3 text-sm">
                <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
                </svg>
                הסידור הושלם! הטיוטה מוכנה לעיון.
              </div>
            ) : (
              <>
                <button
                  onClick={handleTriggerSolver}
                  disabled={solverPolling}
                  className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors"
                >
                  הפעל סידור
                </button>
                {solverError && (
                  <p className="text-sm text-red-600 mt-2">{solverError}</p>
                )}
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

  function renderMessagesPanel() {
    const inp = "border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500";

    async function handleSendMessage(e: React.FormEvent) {
      e.preventDefault();
      if (!currentSpaceId || !newMessageContent.trim()) return;
      setMessageSending(true); setMessageError(null);
      try {
        await apiClient.post(`/spaces/${currentSpaceId}/groups/${groupId}/messages`, {
          content: newMessageContent.trim(), isPinned: false
        });
        setNewMessageContent("");
        // Refresh messages
        const r = await apiClient.get(`/spaces/${currentSpaceId}/groups/${groupId}/messages`);
        setMessages(r.data);
      } catch (err: any) {
        setMessageError(err?.response?.data?.message ?? "שגיאה בשליחת ההודעה");
      } finally { setMessageSending(false); }
    }

    return (
      <div className="space-y-4">
        {/* Send message form — all members can send */}
        <form onSubmit={handleSendMessage} className="flex gap-2">
          <input
            value={newMessageContent}
            onChange={e => setNewMessageContent(e.target.value)}
            placeholder="כתוב הודעה לקבוצה..."
            className={`flex-1 ${inp}`}
          />
          <button type="submit" disabled={messageSending || !newMessageContent.trim()}
            className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors whitespace-nowrap">
            {messageSending ? "שולח..." : "שלח"}
          </button>
        </form>
        {messageError && <p className="text-sm text-red-600">{messageError}</p>}

        {/* Messages list */}
        {messagesLoading ? (
          <div className="flex items-center gap-3 text-slate-400 text-sm py-8">
            <svg className="animate-spin h-5 w-5 text-blue-400" fill="none" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
            </svg>
            טוען...
          </div>
        ) : messagesError ? (
          <p className="text-sm text-red-600 py-4">{messagesError}</p>
        ) : messages.length === 0 ? (
          <p className="text-sm text-slate-400 py-8 text-center">אין הודעות עדיין. היה הראשון לכתוב!</p>
        ) : (
          <div className="space-y-3">
            {[...messages].reverse().map(msg => (
              <div key={msg.id} className={`rounded-2xl border p-4 ${msg.isPinned ? "bg-amber-50 border-amber-200 shadow-sm" : "bg-white border-slate-200"}`}>
                <div className="flex items-start gap-3">
                  <div className="w-8 h-8 rounded-full bg-blue-50 flex items-center justify-center text-blue-600 text-sm font-semibold flex-shrink-0">
                    {msg.authorName?.charAt(0)?.toUpperCase() ?? "?"}
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center justify-between gap-2 mb-1">
                      <div className="flex items-center gap-2">
                        <span className="text-sm font-semibold text-slate-900">{msg.authorName}</span>
                        {msg.isPinned && (
                          <span className="text-xs text-amber-700 bg-amber-100 px-1.5 py-0.5 rounded-full">📌 נעוץ</span>
                        )}
                        <span className="text-xs text-slate-400">
                          {new Date(msg.createdAt).toLocaleString("he-IL", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" })}
                        </span>
                      </div>
                      {/* Admin action buttons */}
                      {isAdmin && (
                        <div className="flex items-center gap-1.5 flex-shrink-0">
                          <button
                            onClick={() => handlePinMessage(msg.id, !msg.isPinned)}
                            title={msg.isPinned ? "בטל נעיצה" : "נעץ הודעה"}
                            className={`text-xs border px-2 py-1 rounded-lg transition-colors ${
                              msg.isPinned
                                ? "text-amber-600 border-amber-200 hover:bg-amber-50"
                                : "text-slate-500 border-slate-200 hover:bg-slate-50"
                            }`}
                          >
                            {msg.isPinned ? "📌 בטל" : "📌 נעץ"}
                          </button>
                          <button
                            onClick={() => {
                              setEditingMessageId(msg.id);
                              setEditMessageContent(msg.content);
                              setEditMessageError(null);
                            }}
                            className="text-xs text-blue-500 hover:text-blue-700 border border-blue-200 hover:border-blue-400 px-2 py-1 rounded-lg transition-colors"
                          >
                            ערוך
                          </button>
                          <button
                            onClick={() => {
                              if (confirm("האם אתה בטוח שברצונך למחוק הודעה זו?")) {
                                handleDeleteMessage(msg.id);
                              }
                            }}
                            className="text-xs text-red-500 hover:text-red-700 border border-red-200 hover:border-red-400 px-2 py-1 rounded-lg transition-colors"
                          >
                            מחק
                          </button>
                        </div>
                      )}
                    </div>
                    {/* Inline edit form removed — editing handled via Modal */}
                    <p className="text-sm text-slate-700 whitespace-pre-wrap">{msg.content}</p>
                    {messagePinErrors[msg.id] && (
                      <p className="text-xs text-red-600 mt-1">{messagePinErrors[msg.id]}</p>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    );
  }

  function renderAlertsPanel() {
    const inp = "border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500";

    return (
      <div className="space-y-4">
        {/* Create alert button — admin only */}
        {isAdmin && (
          <div className="flex justify-end">
            <button
              onClick={() => setShowAlertForm(true)}
              className="flex items-center gap-2 bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-3.5 py-2 rounded-xl transition-colors"
            >
              <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
              </svg>
              התראה חדשה
            </button>
          </div>
        )}

        {/* Alerts list */}
        {alertsLoading ? (
          <div className="flex items-center gap-3 text-slate-400 text-sm py-8">
            <svg className="animate-spin h-5 w-5 text-blue-400" fill="none" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
            </svg>
            טוען...
          </div>
        ) : alertsError ? (
          <p className="text-sm text-red-600 py-4">{alertsError}</p>
        ) : alerts.length === 0 ? (
          <p className="text-sm text-slate-400 py-8 text-center">אין התראות לקבוצה זו</p>
        ) : (
          <div className="space-y-3">
            {alerts.map(alert => {
              const badge = getSeverityBadge(alert.severity);
              return (
                <div key={alert.id} className={`rounded-2xl border p-4 ${badge.bg} ${badge.border}`}>
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 mb-1.5">
                        <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border ${badge.bg} ${badge.text} ${badge.border}`}>
                          {badge.label}
                        </span>
                        <span className="text-xs text-slate-400">
                          {new Date(alert.createdAt).toLocaleString("he-IL", {
                            day: "numeric", month: "short", year: "numeric",
                            hour: "2-digit", minute: "2-digit"
                          })}
                        </span>
                      </div>
                      <h3 className={`text-sm font-semibold mb-1 ${badge.text}`}>{alert.title}</h3>
                      <p className="text-sm text-slate-700 whitespace-pre-wrap">{alert.body}</p>
                      <p className="text-xs text-slate-400 mt-2">פורסם על ידי: {alert.createdByDisplayName}</p>
                      {alertDeleteErrors[alert.id] && (
                        <p className="text-xs text-red-600 mt-1">{alertDeleteErrors[alert.id]}</p>
                      )}
                    </div>
                    {isAdmin && (
                      <div className="flex items-center gap-2 flex-shrink-0">
                        <button
                          onClick={() => {
                            setEditingAlertId(alert.id);
                            setEditAlertTitle(alert.title);
                            setEditAlertBody(alert.body);
                            setEditAlertSeverity(alert.severity);
                            setEditAlertError(null);
                          }}
                          className="text-xs text-blue-500 hover:text-blue-700 border border-blue-200 hover:border-blue-400 px-2.5 py-1 rounded-lg transition-colors"
                        >
                          ערוך
                        </button>
                        <button
                          onClick={() => handleDeleteAlert(alert.id)}
                          className="text-xs text-red-500 hover:text-red-700 border border-red-200 hover:border-red-400 px-2.5 py-1 rounded-lg transition-colors"
                        >
                          מחק
                        </button>
                      </div>
                    )}
                  </div>
                </div>
              );
            })}
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

      {/* ── Task modal ── */}
      <Modal
        title={editingTask ? "עריכת משימה" : "משימה חדשה"}
        open={showTaskForm}
        onClose={() => { setShowTaskForm(false); setEditingTask(null); setTaskError(null); }}
      >
        {(() => {
          const inp = "border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500";
          const burdenOptions = [
            { value: "favorable", label: "נוח" },
            { value: "neutral", label: "ניטרלי" },
            { value: "disliked", label: "לא אהוב" },
            { value: "hated", label: "שנוא" },
          ];
          return (
            <form onSubmit={handleTaskFormSubmit} className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="col-span-2">
                  <label className="block text-xs font-medium text-slate-500 mb-1.5">שם *</label>
                  <input value={taskForm.name} onChange={e => setTaskForm(f => ({ ...f, name: e.target.value }))}
                    required className={`w-full ${inp}`} placeholder="שם המשימה" />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-500 mb-1.5">התחלה *</label>
                  <input type="datetime-local" value={taskForm.startsAt}
                    onChange={e => setTaskForm(f => ({ ...f, startsAt: e.target.value }))}
                    required className={`w-full ${inp}`} />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-500 mb-1.5">סיום *</label>
                  <input type="datetime-local" value={taskForm.endsAt}
                    onChange={e => setTaskForm(f => ({ ...f, endsAt: e.target.value }))}
                    required className={`w-full ${inp}`} />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-500 mb-1.5">משך (שעות) *</label>
                  <input type="number" min={0.5} step={0.5} value={taskForm.durationHours}
                    onChange={e => setTaskForm(f => ({ ...f, durationHours: Number(e.target.value) }))}
                    required className={`w-full ${inp}`} />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-500 mb-1.5">כוח אדם נדרש *</label>
                  <input type="number" min={1} value={taskForm.requiredHeadcount}
                    onChange={e => setTaskForm(f => ({ ...f, requiredHeadcount: Number(e.target.value) }))}
                    required className={`w-full ${inp}`} />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-500 mb-1.5">רמת עומס</label>
                  <select value={taskForm.burdenLevel}
                    onChange={e => setTaskForm(f => ({ ...f, burdenLevel: e.target.value }))}
                    className={`w-full ${inp}`}>
                    {burdenOptions.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
                  </select>
                </div>
              </div>
              <div className="flex gap-6">
                <label className="flex items-center gap-2.5 text-sm text-slate-700 cursor-pointer">
                  <input type="checkbox" checked={taskForm.allowsDoubleShift}
                    onChange={e => setTaskForm(f => ({ ...f, allowsDoubleShift: e.target.checked }))}
                    className="w-4 h-4 rounded" />
                  מאפשר משמרת כפולה
                </label>
                <label className="flex items-center gap-2.5 text-sm text-slate-700 cursor-pointer">
                  <input type="checkbox" checked={taskForm.allowsOverlap}
                    onChange={e => setTaskForm(f => ({ ...f, allowsOverlap: e.target.checked }))}
                    className="w-4 h-4 rounded" />
                  מאפשר חפיפה
                </label>
              </div>
              {taskError && <p className="text-sm text-red-600">{taskError}</p>}
              <div className="flex gap-2">
                <button type="submit" disabled={taskSaving}
                  className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
                  {taskSaving ? "שומר..." : "שמור"}
                </button>
                <button type="button" onClick={() => { setShowTaskForm(false); setEditingTask(null); setTaskError(null); }}
                  className="text-sm text-slate-500 hover:text-slate-700 px-3">ביטול</button>
              </div>
            </form>
          );
        })()}
      </Modal>

      {/* ── New constraint modal ── */}
      <Modal
        title="אילוץ חדש"
        open={showConstraintForm}
        onClose={() => { setShowConstraintForm(false); setConstraintError(null); }}
        maxWidth={560}
      >
        {(() => {
          const inp = "border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500";
          return (
            <form onSubmit={handleCreateConstraint} className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-medium text-slate-500 mb-1.5">סוג כלל</label>
                  <select value={newConstraintRuleType} onChange={e => {
                    const rt = e.target.value;
                    setNewConstraintRuleType(rt);
                    const defaults: Record<string, string> = {
                      min_rest_hours: '{"hours": 8}',
                      max_kitchen_per_week: '{"max": 2, "task_type_name": "kitchen"}',
                      no_consecutive_burden: '{"burden_level": "disliked"}',
                      min_base_headcount: '{"min": 3, "window_hours": 24}',
                      no_task_type_restriction: '{"task_type_id": ""}',
                    };
                    setNewConstraintPayload(defaults[rt] ?? "{}");
                  }} className={`w-full ${inp}`}>
                    <option value="min_rest_hours">מינימום מנוחה בין משמרות</option>
                    <option value="max_kitchen_per_week">מקסימום משמרות מטבח בשבוע</option>
                    <option value="no_consecutive_burden">ללא עומס רצוף</option>
                    <option value="min_base_headcount">מינימום כוח אדם בסיסי</option>
                    <option value="no_task_type_restriction">הגבלת סוג משימה לאדם</option>
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-500 mb-1.5">חומרה</label>
                  <select value={newConstraintSeverity} onChange={e => setNewConstraintSeverity(e.target.value)} className={`w-full ${inp}`}>
                    <option value="hard">קשיח — חייב להתקיים</option>
                    <option value="soft">רך — עדיפות בלבד</option>
                  </select>
                </div>
              </div>
              {newConstraintRuleType === "min_rest_hours" && (
                <div>
                  <label className="block text-xs font-medium text-slate-500 mb-1.5">שעות מנוחה מינימליות בין משמרות</label>
                  <input type="number" min={1} max={48}
                    value={(() => { try { return JSON.parse(newConstraintPayload).hours ?? 8; } catch { return 8; } })()}
                    onChange={e => setNewConstraintPayload(JSON.stringify({ hours: Number(e.target.value) }))}
                    className={`w-32 ${inp}`} />
                  <p className="text-xs text-slate-400 mt-1">לדוגמה: 8 שעות מנוחה בין סיום משמרת לתחילת הבאה</p>
                </div>
              )}
              {newConstraintRuleType === "max_kitchen_per_week" && (
                <div>
                  <label className="block text-xs font-medium text-slate-500 mb-1.5">מקסימום משמרות מטבח בשבוע</label>
                  <input type="number" min={1} max={7}
                    value={(() => { try { return JSON.parse(newConstraintPayload).max ?? 2; } catch { return 2; } })()}
                    onChange={e => setNewConstraintPayload(JSON.stringify({ max: Number(e.target.value), task_type_name: "kitchen" }))}
                    className={`w-32 ${inp}`} />
                </div>
              )}
              {newConstraintRuleType === "no_consecutive_burden" && (
                <div>
                  <label className="block text-xs font-medium text-slate-500 mb-1.5">רמת עומס שאסור לחזור ברצף</label>
                  <select
                    value={(() => { try { return JSON.parse(newConstraintPayload).burden_level ?? "disliked"; } catch { return "disliked"; } })()}
                    onChange={e => setNewConstraintPayload(JSON.stringify({ burden_level: e.target.value }))}
                    className={`w-full max-w-xs ${inp}`}>
                    <option value="disliked">לא אהוב (Disliked)</option>
                    <option value="hated">שנוא (Hated)</option>
                    <option value="neutral">ניטרלי (Neutral)</option>
                  </select>
                </div>
              )}
              {newConstraintRuleType === "min_base_headcount" && (
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-slate-500 mb-1.5">מינימום אנשים</label>
                    <input type="number" min={1}
                      value={(() => { try { return JSON.parse(newConstraintPayload).min ?? 3; } catch { return 3; } })()}
                      onChange={e => {
                        const cur = (() => { try { return JSON.parse(newConstraintPayload); } catch { return { min: 3, window_hours: 24 }; } })();
                        setNewConstraintPayload(JSON.stringify({ ...cur, min: Number(e.target.value) }));
                      }}
                      className={`w-full ${inp}`} />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-slate-500 mb-1.5">חלון זמן (שעות)</label>
                    <input type="number" min={1}
                      value={(() => { try { return JSON.parse(newConstraintPayload).window_hours ?? 24; } catch { return 24; } })()}
                      onChange={e => {
                        const cur = (() => { try { return JSON.parse(newConstraintPayload); } catch { return { min: 3, window_hours: 24 }; } })();
                        setNewConstraintPayload(JSON.stringify({ ...cur, window_hours: Number(e.target.value) }));
                      }}
                      className={`w-full ${inp}`} />
                  </div>
                </div>
              )}
              {newConstraintRuleType === "no_task_type_restriction" && (
                <div>
                  <label className="block text-xs font-medium text-slate-500 mb-1.5">סוג משימה מוגבל</label>
                  <select
                    value={(() => { try { return JSON.parse(newConstraintPayload).task_type_id ?? ""; } catch { return ""; } })()}
                    onChange={e => setNewConstraintPayload(JSON.stringify({ task_type_id: e.target.value }))}
                    className={`w-full ${inp}`}>
                    <option value="">בחר סוג משימה...</option>
                    {groupTasks.map(tt => <option key={tt.id} value={tt.id}>{tt.name}</option>)}
                  </select>
                  <p className="text-xs text-slate-400 mt-1">האדם לא יוכל לבצע את סוג המשימה הזה</p>
                </div>
              )}
              {constraintError && <p className="text-sm text-red-600">{constraintError}</p>}
              <div className="flex gap-2">
                <button type="submit" disabled={constraintSaving}
                  className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
                  {constraintSaving ? "שומר..." : "שמור"}
                </button>
                <button type="button" onClick={() => { setShowConstraintForm(false); setConstraintError(null); }}
                  className="text-sm text-slate-500 hover:text-slate-700 px-3">ביטול</button>
              </div>
            </form>
          );
        })()}
      </Modal>

      {/* ── Edit constraint modal ── */}
      <Modal
        title="עריכת אילוץ"
        open={!!editingConstraintId}
        onClose={() => { setEditingConstraintId(null); setEditConstraintError(null); }}
      >
        <div className="space-y-3">
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1">Payload (JSON)</label>
            <textarea
              value={editConstraintPayload}
              onChange={e => setEditConstraintPayload(e.target.value)}
              rows={3}
              className="w-full border border-slate-200 rounded-xl px-3 py-2 text-xs font-mono focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
            />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-slate-500 mb-1">בתוקף מ</label>
              <input type="date" value={editConstraintFrom}
                onChange={e => setEditConstraintFrom(e.target.value)}
                className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-500 mb-1">בתוקף עד</label>
              <input type="date" value={editConstraintUntil}
                onChange={e => setEditConstraintUntil(e.target.value)}
                className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
          </div>
          {editConstraintError && <p className="text-xs text-red-600">{editConstraintError}</p>}
          <div className="flex gap-2">
            <button
              onClick={() => editingConstraintId && handleUpdateConstraint(editingConstraintId)}
              disabled={editConstraintSaving}
              className="bg-blue-500 hover:bg-blue-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors"
            >
              {editConstraintSaving ? "שומר..." : "שמור"}
            </button>
            <button
              onClick={() => { setEditingConstraintId(null); setEditConstraintError(null); }}
              className="text-xs text-slate-500 hover:text-slate-700 px-2"
            >
              ביטול
            </button>
          </div>
        </div>
      </Modal>

      {/* ── New alert modal ── */}
      <Modal
        title="התראה חדשה"
        open={showAlertForm}
        onClose={() => { setShowAlertForm(false); setAlertSubmitError(null); }}
      >
        {(() => {
          const inp = "border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500";
          return (
            <form onSubmit={handleCreateAlert} className="space-y-4">
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">כותרת *</label>
                <input value={newAlertTitle} onChange={e => setNewAlertTitle(e.target.value)}
                  required maxLength={200} placeholder="כותרת ההתראה" className={`w-full ${inp}`} />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">תוכן *</label>
                <textarea value={newAlertBody} onChange={e => setNewAlertBody(e.target.value)}
                  required maxLength={2000} rows={3} placeholder="תוכן ההתראה..."
                  className={`w-full ${inp} resize-none`} />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">רמת חומרה</label>
                <select value={newAlertSeverity} onChange={e => setNewAlertSeverity(e.target.value)}
                  className={`w-full max-w-xs ${inp}`}>
                  <option value="info">מידע</option>
                  <option value="warning">אזהרה</option>
                  <option value="critical">קריטי</option>
                </select>
              </div>
              {alertSubmitError && <p className="text-sm text-red-600">{alertSubmitError}</p>}
              <button type="submit" disabled={alertSubmitting}
                className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
                {alertSubmitting ? "שולח..." : "פרסם התראה"}
              </button>
            </form>
          );
        })()}
      </Modal>

      {/* ── Edit alert modal ── */}
      <Modal
        title="עריכת התראה"
        open={!!editingAlertId}
        onClose={() => { setEditingAlertId(null); setEditAlertError(null); }}
      >
        <div className="space-y-3">
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1">כותרת</label>
            <input value={editAlertTitle} onChange={e => setEditAlertTitle(e.target.value)}
              maxLength={200}
              className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1">תוכן</label>
            <textarea value={editAlertBody} onChange={e => setEditAlertBody(e.target.value)}
              maxLength={2000} rows={3}
              className="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none" />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1">רמת חומרה</label>
            <select value={editAlertSeverity} onChange={e => setEditAlertSeverity(e.target.value)}
              className="border border-slate-200 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
              <option value="info">מידע</option>
              <option value="warning">אזהרה</option>
              <option value="critical">קריטי</option>
            </select>
          </div>
          {editAlertError && <p className="text-xs text-red-600">{editAlertError}</p>}
          <div className="flex gap-2">
            <button onClick={() => editingAlertId && handleUpdateAlert(editingAlertId)}
              disabled={editAlertSaving}
              className="bg-blue-500 hover:bg-blue-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors">
              {editAlertSaving ? "שומר..." : "שמור"}
            </button>
            <button onClick={() => { setEditingAlertId(null); setEditAlertError(null); }}
              className="text-xs text-slate-500 hover:text-slate-700 px-2">ביטול</button>
          </div>
        </div>
      </Modal>

      {/* ── Edit message modal ── */}
      <Modal
        title="עריכת הודעה"
        open={!!editingMessageId}
        onClose={() => { setEditingMessageId(null); setEditMessageError(null); }}
      >
        <div className="space-y-3">
          <textarea
            value={editMessageContent}
            onChange={e => setEditMessageContent(e.target.value)}
            rows={4}
            className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
          />
          {editMessageError && <p className="text-xs text-red-600">{editMessageError}</p>}
          <div className="flex gap-2">
            <button onClick={() => editingMessageId && handleUpdateMessage(editingMessageId)}
              disabled={editMessageSaving}
              className="bg-blue-500 hover:bg-blue-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors">
              {editMessageSaving ? "שומר..." : "שמור"}
            </button>
            <button onClick={() => { setEditingMessageId(null); setEditMessageError(null); }}
              className="text-xs text-slate-500 hover:text-slate-700 px-2">ביטול</button>
          </div>
        </div>
      </Modal>

      {/* ── Add member by email/phone modal ── */}
      <Modal
        title="הוספת חבר"
        open={showAddMemberModal}
        onClose={() => { setShowAddMemberModal(false); setAddError(null); }}
      >
        {(() => {
          const inp = "border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500";
          return (
            <form onSubmit={async (e) => { await handleAddMember(e); if (!addError) setShowAddMemberModal(false); }} className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1.5">אימייל או מספר טלפון *</label>
                <input type="text" value={addEmail} onChange={e => setAddEmail(e.target.value)}
                  placeholder="הוסף לפי אימייל או מספר טלפון" className={`w-full ${inp}`} />
                <p className="text-xs text-slate-400 mt-1">ניתן להזין אימייל או מספר טלפון</p>
              </div>
              {addError && <p className="text-sm text-red-600">{addError}</p>}
              <div className="flex gap-2">
                <button type="submit"
                  className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl transition-colors">
                  הוסף
                </button>
                <button type="button" onClick={() => { setShowAddMemberModal(false); setAddError(null); }}
                  className="text-sm text-slate-500 hover:text-slate-700 px-3">ביטול</button>
              </div>
            </form>
          );
        })()}
      </Modal>

      {/* ── Add person by name modal ── */}
      <Modal
        title="הוספת אדם לפי שם"
        open={showCreatePersonForm}
        onClose={() => { setShowCreatePersonForm(false); setCreatePersonError(null); }}
      >
        {(() => {
          const inp = "border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500";
          return (
            <form onSubmit={handleCreatePerson} className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1">שם מלא *</label>
                <input value={newPersonName} onChange={e => setNewPersonName(e.target.value)}
                  required placeholder="לדוגמה: יוסי כהן" className={`w-full ${inp}`} />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-500 mb-1">שם תצוגה (אופציונלי)</label>
                <input value={newPersonDisplayName} onChange={e => setNewPersonDisplayName(e.target.value)}
                  placeholder="לדוגמה: יוסי" className={`w-full ${inp}`} />
              </div>
              {createPersonError && <p className="text-xs text-red-600">{createPersonError}</p>}
              <div className="flex gap-2">
                <button type="submit" disabled={createPersonSaving}
                  className="bg-blue-500 hover:bg-blue-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors">
                  {createPersonSaving ? "שומר..." : "הוסף"}
                </button>
                <button type="button" onClick={() => { setShowCreatePersonForm(false); setCreatePersonError(null); }}
                  className="text-xs text-slate-500 hover:text-slate-700 px-2">ביטול</button>
              </div>
            </form>
          );
        })()}
      </Modal>

      {/* ── Send invitation modal ── */}
      <Modal
        title="שליחת הזמנה"
        open={!!invitingPersonId}
        onClose={() => { setInvitingPersonId(null); setInviteError(null); setInviteSuccess(null); }}
      >
        <form onSubmit={handleInvitePerson} className="space-y-3">
          <div className="flex gap-2">
            <input
              value={inviteContact}
              onChange={e => setInviteContact(e.target.value)}
              placeholder={inviteChannel === "email" ? "כתובת אימייל" : "מספר טלפון"}
              required
              className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            <select value={inviteChannel} onChange={e => setInviteChannel(e.target.value as "email" | "whatsapp")}
              className="border border-slate-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none">
              <option value="whatsapp">WhatsApp</option>
              <option value="email">אימייל</option>
            </select>
          </div>
          {inviteError && <p className="text-xs text-red-600">{inviteError}</p>}
          {inviteSuccess && <p className="text-xs text-emerald-600">{inviteSuccess}</p>}
          <div className="flex gap-2">
            <button type="submit" disabled={inviteSaving}
              className="bg-blue-500 hover:bg-blue-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors">
              {inviteSaving ? "שולח..." : "שלח הזמנה"}
            </button>
            <button type="button" onClick={() => { setInvitingPersonId(null); setInviteError(null); setInviteSuccess(null); }}
              className="text-xs text-slate-500 hover:text-slate-700 px-2">ביטול</button>
          </div>
        </form>
      </Modal>

      {/* Member profile modal */}
      {selectedMember && (
        <div
          style={{
            position: "fixed", inset: 0, zIndex: 50,
            background: "rgba(0,0,0,0.45)",
            display: "flex", alignItems: "center", justifyContent: "center",
            padding: "1rem",
          }}
          onClick={() => { setSelectedMember(null); setEditingMemberForm(null); setMemberEditError(null); }}
        >
          <div
            style={{
              background: "white", borderRadius: 20,
              boxShadow: "0 20px 60px rgba(0,0,0,0.15)",
              width: "100%", maxWidth: 420,
              padding: "1.75rem",
              direction: "rtl",
              position: "relative",
            }}
            onClick={e => e.stopPropagation()}
          >
            {/* Close button */}
            <button
              onClick={() => { setSelectedMember(null); setEditingMemberForm(null); setMemberEditError(null); }}
              style={{
                position: "absolute", top: "1rem", left: "1rem",
                background: "none", border: "none", cursor: "pointer",
                color: "#94a3b8", padding: 4, display: "flex", alignItems: "center",
              }}
            >
              <svg width="18" height="18" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>

            {editingMemberForm ? (
              /* Edit mode */
              <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
                <h2 style={{ fontSize: "1rem", fontWeight: 600, color: "#0f172a", margin: 0 }}>עריכת פרטי חבר</h2>

                {[
                  { label: "שם מלא", key: "fullName", type: "text" },
                  { label: "שם תצוגה", key: "displayName", type: "text" },
                  { label: "מספר טלפון", key: "phoneNumber", type: "tel" },
                  { label: "תמונת פרופיל (URL)", key: "profileImageUrl", type: "url" },
                  { label: "תאריך לידה", key: "birthday", type: "date" },
                ].map(field => (
                  <div key={field.key}>
                    <label style={{ display: "block", fontSize: "0.75rem", fontWeight: 600, color: "#94a3b8", marginBottom: "0.25rem" }}>
                      {field.label}
                    </label>
                    <input
                      type={field.type}
                      value={(editingMemberForm as any)[field.key]}
                      onChange={e => setEditingMemberForm(f => f ? { ...f, [field.key]: e.target.value } : f)}
                      style={{
                        width: "100%", border: "1px solid #e2e8f0", borderRadius: 10,
                        padding: "0.625rem 0.875rem", fontSize: "0.875rem",
                        color: "#0f172a", outline: "none", boxSizing: "border-box",
                      }}
                    />
                  </div>
                ))}

                {memberEditError && (
                  <p style={{ fontSize: "0.875rem", color: "#dc2626", margin: 0 }}>{memberEditError}</p>
                )}

                <div style={{ display: "flex", gap: "0.75rem" }}>
                  <button
                    onClick={() => handleSaveMemberEdit(selectedMember.personId)}
                    disabled={memberEditSaving}
                    style={{
                      background: memberEditSaving ? "#93c5fd" : "#3b82f6",
                      color: "white", border: "none", borderRadius: 10,
                      padding: "0.625rem 1.25rem", fontSize: "0.875rem",
                      fontWeight: 600, cursor: memberEditSaving ? "not-allowed" : "pointer",
                    }}
                  >
                    {memberEditSaving ? "שומר..." : "שמור"}
                  </button>
                  <button
                    onClick={() => { setEditingMemberForm(null); setMemberEditError(null); }}
                    style={{
                      background: "none", border: "1px solid #e2e8f0", borderRadius: 10,
                      padding: "0.625rem 1.25rem", fontSize: "0.875rem",
                      color: "#64748b", cursor: "pointer",
                    }}
                  >
                    ביטול
                  </button>
                </div>
              </div>
            ) : (
              /* View mode */
              <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: "1rem", textAlign: "center" }}>
                {/* Avatar */}
                {selectedMember.profileImageUrl ? (
                  <img
                    src={selectedMember.profileImageUrl}
                    alt=""
                    style={{ width: 80, height: 80, borderRadius: "50%", objectFit: "cover" }}
                  />
                ) : (
                  <div style={{
                    width: 80, height: 80, borderRadius: "50%",
                    background: "linear-gradient(135deg, #3b82f6, #6366f1)",
                    display: "flex", alignItems: "center", justifyContent: "center",
                    color: "white", fontSize: "1.75rem", fontWeight: 700,
                  }}>
                    {(selectedMember.displayName ?? selectedMember.fullName).charAt(0)}
                  </div>
                )}

                {/* Name */}
                <div>
                  <h2 style={{ fontSize: "1.125rem", fontWeight: 700, color: "#0f172a", margin: "0 0 0.25rem" }}>
                    {selectedMember.fullName}
                  </h2>
                  {selectedMember.displayName && selectedMember.displayName !== selectedMember.fullName && (
                    <p style={{ fontSize: "0.875rem", color: "#64748b", margin: 0 }}>{selectedMember.displayName}</p>
                  )}
                </div>

                {/* Status badge */}
                <span style={{
                  display: "inline-flex", alignItems: "center", gap: "0.375rem",
                  padding: "0.25rem 0.75rem", borderRadius: 999, fontSize: "0.8125rem", fontWeight: 600,
                  ...(selectedMember.invitationStatus === "accepted"
                    ? { background: "#f0fdf4", color: "#16a34a", border: "1px solid #bbf7d0" }
                    : { background: "#fffbeb", color: "#d97706", border: "1px solid #fde68a" }),
                }}>
                  {selectedMember.invitationStatus === "accepted" ? "מאושר ✓" : "ממתין לאישור"}
                </span>

                {/* Phone */}
                {selectedMember.phoneNumber && (
                  <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", color: "#475569", fontSize: "0.875rem" }}>
                    <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z" />
                    </svg>
                    {selectedMember.phoneNumber}
                  </div>
                )}

                {/* Owner badge */}
                {selectedMember.isOwner && (
                  <span style={{
                    display: "inline-flex", padding: "0.25rem 0.75rem", borderRadius: 999,
                    fontSize: "0.8125rem", fontWeight: 600,
                    background: "#fffbeb", color: "#d97706", border: "1px solid #fde68a",
                  }}>
                    בעלים
                  </span>
                )}

                {/* Admin edit button */}
                {isAdmin && !selectedMember.isOwner && (
                  <button
                    onClick={() => setEditingMemberForm({
                      fullName: selectedMember.fullName,
                      displayName: selectedMember.displayName ?? "",
                      phoneNumber: selectedMember.phoneNumber ?? "",
                      profileImageUrl: selectedMember.profileImageUrl ?? "",
                      birthday: "",
                    })}
                    style={{
                      background: "#3b82f6", color: "white", border: "none",
                      borderRadius: 10, padding: "0.625rem 1.5rem",
                      fontSize: "0.875rem", fontWeight: 600, cursor: "pointer",
                      marginTop: "0.5rem",
                    }}
                  >
                    ערוך
                  </button>
                )}
              </div>
            )}
          </div>
        </div>
      )}
    </AppShell>
  );
}
