"use client";

import React, { useEffect, useState, useRef } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import AppShell from "@/components/shell/AppShell";
import Modal from "@/components/Modal";
import DraftScheduleModal from "@/components/DraftScheduleModal";
import ScheduleTab from "./tabs/ScheduleTab";
import MembersTab, { MemberProfileModal } from "./tabs/MembersTab";
import AlertsTab from "./tabs/AlertsTab";
import MessagesTab from "./tabs/MessagesTab";
import TasksTab from "./tabs/TasksTab";
import ConstraintsTab from "./tabs/ConstraintsTab";
import SettingsTab from "./tabs/SettingsTab";
import { ActiveTab, ADMIN_ONLY_TABS, ScheduleAssignment } from "./types";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useAuthStore } from "@/lib/store/authStore";
import {
  getGroups, getGroupMembers, addGroupMemberByEmail, removeGroupMember,
  updateGroupSettings, renameGroup, softDeleteGroup, restoreGroup,
  getDeletedGroups, initiateOwnershipTransfer, cancelOwnershipTransfer,
  GroupWithMemberCountDto, GroupMemberDto, DeletedGroupDto,
  getGroupAlerts, createGroupAlert, deleteGroupAlert, updateGroupAlert, GroupAlertDto,
  updateGroupMessage, deleteGroupMessage, pinGroupMessage,
  updatePersonInfo,
} from "@/lib/api/groups";
import { getAvatarColor, getAvatarLetter } from "@/lib/utils/groupAvatar";
import { listGroupTasks, createGroupTask, updateGroupTask, deleteGroupTask, GroupTaskDto } from "@/lib/api/tasks";
import { getConstraints, createConstraint, updateConstraint, deleteConstraint, ConstraintDto } from "@/lib/api/constraints";
import { createPerson, invitePerson } from "@/lib/api/people";
import { apiClient } from "@/lib/api/client";

// ── Task form default ────────────────────────────────────────────────────────
const DEFAULT_TASK_FORM = {
  name: "",
  startsAt: "",
  endsAt: "",
  shiftDurationMinutes: 60,
  requiredHeadcount: 1,
  burdenLevel: "neutral",
  allowsDoubleShift: false,
  allowsOverlap: false,
};

// ── Tab labels ───────────────────────────────────────────────────────────────
const TAB_LABELS: Record<ActiveTab, string> = {
  schedule: "סידור",
  members: "חברים",
  alerts: "התראות",
  messages: "הודעות",
  tasks: "משימות",
  constraints: "אילוצים",
  settings: "הגדרות",
};

const ALL_TABS: ActiveTab[] = ["schedule", "members", "alerts", "messages", "tasks", "constraints", "settings"];

// ── Main component ───────────────────────────────────────────────────────────
export default function GroupDetailPage() {
  const params = useParams();
  const router = useRouter();
  const groupId = params?.groupId as string;
  const { currentSpaceId } = useSpaceStore();
  const { userId, isAdminForGroup } = useAuthStore();

  // ── Group / header state ─────────────────────────────────────────────────
  const [group, setGroup] = useState<GroupWithMemberCountDto | null>(null);
  const [groupLoading, setGroupLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<ActiveTab>("schedule");
  const [isAdmin, setIsAdmin] = useState(false);

  // ── Schedule state ───────────────────────────────────────────────────────
  const [scheduleData, setScheduleData] = useState<ScheduleAssignment[] | null>(null);
  const [scheduleLoading, setScheduleLoading] = useState(false);
  const [scheduleError, setScheduleError] = useState<string | null>(null);
  const [draftVersion, setDraftVersion] = useState<{ id: string; status: string } | null>(null);
  const [showDraftModal, setShowDraftModal] = useState(false);
  const [publishSaving, setPublishSaving] = useState(false);
  const [discardSaving, setDiscardSaving] = useState(false);
  const [scheduleVersionError, setScheduleVersionError] = useState<string | null>(null);
  const [solverHorizonDays, setSolverHorizonDays] = useState(14);

  // ── Members state ────────────────────────────────────────────────────────
  const [members, setMembers] = useState<GroupMemberDto[]>([]);
  const [membersLoading, setMembersLoading] = useState(false);
  const [membersError, setMembersError] = useState<string | null>(null);
  const [membersSearch, setMembersSearch] = useState("");
  const [removeErrors, setRemoveErrors] = useState<Record<string, string>>({});
  const [selectedMember, setSelectedMember] = useState<GroupMemberDto | null>(null);
  const [memberEditForm, setMemberEditForm] = useState<{
    fullName: string; displayName: string; phoneNumber: string; profileImageUrl: string; birthday: string;
  } | null>(null);
  const [memberEditSaving, setMemberEditSaving] = useState(false);
  const [memberEditError, setMemberEditError] = useState<string | null>(null);

  // ── Add member modals ────────────────────────────────────────────────────
  const [showAddByEmail, setShowAddByEmail] = useState(false);
  const [addEmailInput, setAddEmailInput] = useState("");
  const [addEmailSaving, setAddEmailSaving] = useState(false);
  const [addEmailError, setAddEmailError] = useState<string | null>(null);
  const [showCreatePerson, setShowCreatePerson] = useState(false);
  const [createPersonName, setCreatePersonName] = useState("");
  const [createPersonPhone, setCreatePersonPhone] = useState("");
  const [createPersonSaving, setCreatePersonSaving] = useState(false);
  const [createPersonError, setCreatePersonError] = useState<string | null>(null);
  const [inviteError, setInviteError] = useState<string | null>(null);

  // ── Alerts state ─────────────────────────────────────────────────────────
  const [alerts, setAlerts] = useState<GroupAlertDto[]>([]);
  const [alertsLoading, setAlertsLoading] = useState(false);
  const [alertsError, setAlertsError] = useState<string | null>(null);
  const [alertDeleteErrors, setAlertDeleteErrors] = useState<Record<string, string>>({});
  const [showAlertForm, setShowAlertForm] = useState(false);
  const [newAlertTitle, setNewAlertTitle] = useState("");
  const [newAlertBody, setNewAlertBody] = useState("");
  const [newAlertSeverity, setNewAlertSeverity] = useState("info");
  const [alertSubmitting, setAlertSubmitting] = useState(false);
  const [alertSubmitError, setAlertSubmitError] = useState<string | null>(null);
  const [editingAlertId, setEditingAlertId] = useState<string | null>(null);
  const [editAlertTitle, setEditAlertTitle] = useState("");
  const [editAlertBody, setEditAlertBody] = useState("");
  const [editAlertSeverity, setEditAlertSeverity] = useState("info");
  const [editAlertSaving, setEditAlertSaving] = useState(false);
  const [editAlertError, setEditAlertError] = useState<string | null>(null);

  // ── Messages state ───────────────────────────────────────────────────────
  const [messages, setMessages] = useState<{ id: string; content: string; authorName: string; createdAt: string; isPinned: boolean }[]>([]);
  const [messagesLoading, setMessagesLoading] = useState(false);
  const [messagesError, setMessagesError] = useState<string | null>(null);
  const [newMessageContent, setNewMessageContent] = useState("");
  const [messageSending, setMessageSending] = useState(false);
  const [messageError, setMessageError] = useState<string | null>(null);
  const [messagePinErrors, setMessagePinErrors] = useState<Record<string, string>>({});
  const [editingMessageId, setEditingMessageId] = useState<string | null>(null);
  const [editMessageContent, setEditMessageContent] = useState("");
  const [editMessageSaving, setEditMessageSaving] = useState(false);
  const [editMessageError, setEditMessageError] = useState<string | null>(null);

  // ── Tasks state ──────────────────────────────────────────────────────────
  const [groupTasks, setGroupTasks] = useState<GroupTaskDto[]>([]);
  const [groupTasksLoading, setGroupTasksLoading] = useState(false);
  const [showTaskForm, setShowTaskForm] = useState(false);
  const [editingTask, setEditingTask] = useState<GroupTaskDto | null>(null);
  const [taskForm, setTaskForm] = useState(DEFAULT_TASK_FORM);
  const [taskSaving, setTaskSaving] = useState(false);
  const [taskError, setTaskError] = useState<string | null>(null);

  // ── Constraints state ────────────────────────────────────────────────────
  const [constraints, setConstraints] = useState<ConstraintDto[]>([]);
  const [constraintsLoading, setConstraintsLoading] = useState(false);
  const [constraintDeleteErrors, setConstraintDeleteErrors] = useState<Record<string, string>>({});
  const [showConstraintForm, setShowConstraintForm] = useState(false);
  const [newConstraintRuleType, setNewConstraintRuleType] = useState("min_rest_hours");
  const [newConstraintSeverity, setNewConstraintSeverity] = useState("hard");
  const [newConstraintPayload, setNewConstraintPayload] = useState('{"hours": 8}');
  const [constraintSaving, setConstraintSaving] = useState(false);
  const [constraintError, setConstraintError] = useState<string | null>(null);
  const [editingConstraintId, setEditingConstraintId] = useState<string | null>(null);
  const [editConstraintPayload, setEditConstraintPayload] = useState("");
  const [editConstraintFrom, setEditConstraintFrom] = useState("");
  const [editConstraintUntil, setEditConstraintUntil] = useState("");
  const [editConstraintSaving, setEditConstraintSaving] = useState(false);
  const [editConstraintError, setEditConstraintError] = useState<string | null>(null);

  // ── Settings state ───────────────────────────────────────────────────────
  const [newGroupName, setNewGroupName] = useState("");
  const [renameSaving, setRenameSaving] = useState(false);
  const [renameError, setRenameError] = useState<string | null>(null);
  const [solverHorizon, setSolverHorizon] = useState(14);
  const [savingSettings, setSavingSettings] = useState(false);
  const [settingsError, setSettingsError] = useState<string | null>(null);
  const [settingsSaved, setSettingsSaved] = useState(false);
  const [solverPolling, setSolverPolling] = useState(false);
  const [solverStatus, setSolverStatus] = useState<string | null>(null);
  const [solverError, setSolverError] = useState<string | null>(null);
  const [deletedGroups, setDeletedGroups] = useState<DeletedGroupDto[]>([]);
  const [deletedGroupsLoading, setDeletedGroupsLoading] = useState(false);
  const [transferPersonId, setTransferPersonId] = useState("");
  const [transferSaving, setTransferSaving] = useState(false);
  const [transferError, setTransferError] = useState<string | null>(null);
  const [hasPendingTransfer, setHasPendingTransfer] = useState(false);
  const [cancelTransferSaving, setCancelTransferSaving] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleteSaving, setDeleteSaving] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const pollingRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // ── Load group ───────────────────────────────────────────────────────────
  useEffect(() => {
    if (!currentSpaceId || !groupId) return;
    setGroupLoading(true);
    getGroups(currentSpaceId)
      .then(groups => {
        const found = groups.find(g => g.id === groupId);
        if (!found) { router.push("/groups"); return; }
        setGroup(found);
        setNewGroupName(found.name);
        setSolverHorizon(found.solverHorizonDays ?? 14);
        setSolverHorizonDays(found.solverHorizonDays ?? 14);
        setIsAdmin(found.ownerPersonId === userId || isAdminForGroup(groupId));
      })
      .catch(() => router.push("/groups"))
      .finally(() => setGroupLoading(false));
  }, [currentSpaceId, groupId, userId, isAdminForGroup, router]);

  // ── Load schedule ────────────────────────────────────────────────────────
  useEffect(() => {
    if (!currentSpaceId || !groupId || activeTab !== "schedule") return;
    setScheduleLoading(true);
    setScheduleError(null);
    apiClient.get<{ assignments: ScheduleAssignment[]; draftVersion?: { id: string; status: string } }>(
      `/spaces/${currentSpaceId}/groups/${groupId}/schedule/current`
    )
      .then(res => {
        setScheduleData(res.data.assignments ?? []);
        setDraftVersion(res.data.draftVersion ?? null);
      })
      .catch(() => setScheduleError("שגיאה בטעינת הסידור"))
      .finally(() => setScheduleLoading(false));
  }, [currentSpaceId, groupId, activeTab]);

  // ── Load members ─────────────────────────────────────────────────────────
  useEffect(() => {
    if (!currentSpaceId || !groupId || activeTab !== "members") return;
    setMembersLoading(true);
    setMembersError(null);
    getGroupMembers(currentSpaceId, groupId)
      .then(setMembers)
      .catch(() => setMembersError("שגיאה בטעינת חברים"))
      .finally(() => setMembersLoading(false));
  }, [currentSpaceId, groupId, activeTab]);

  // ── Load alerts ──────────────────────────────────────────────────────────
  useEffect(() => {
    if (!currentSpaceId || !groupId || activeTab !== "alerts") return;
    setAlertsLoading(true);
    setAlertsError(null);
    getGroupAlerts(currentSpaceId, groupId)
      .then(setAlerts)
      .catch(() => setAlertsError("שגיאה בטעינת התראות"))
      .finally(() => setAlertsLoading(false));
  }, [currentSpaceId, groupId, activeTab]);

  // ── Load messages ────────────────────────────────────────────────────────
  useEffect(() => {
    if (!currentSpaceId || !groupId || activeTab !== "messages") return;
    setMessagesLoading(true);
    setMessagesError(null);
    apiClient.get<{ messages: { id: string; content: string; authorName: string; createdAt: string; isPinned: boolean }[] }>(
      `/spaces/${currentSpaceId}/groups/${groupId}/messages`
    )
      .then(res => setMessages(res.data.messages ?? []))
      .catch(() => setMessagesError("שגיאה בטעינת הודעות"))
      .finally(() => setMessagesLoading(false));
  }, [currentSpaceId, groupId, activeTab]);

  // ── Load tasks ───────────────────────────────────────────────────────────
  useEffect(() => {
    if (!currentSpaceId || !groupId || activeTab !== "tasks") return;
    setGroupTasksLoading(true);
    listGroupTasks(currentSpaceId, groupId)
      .then(setGroupTasks)
      .catch(() => {})
      .finally(() => setGroupTasksLoading(false));
  }, [currentSpaceId, groupId, activeTab]);

  // ── Load constraints ─────────────────────────────────────────────────────
  useEffect(() => {
    if (!currentSpaceId || !groupId || activeTab !== "constraints") return;
    setConstraintsLoading(true);
    getConstraints(currentSpaceId)
      .then(setConstraints)
      .catch(() => {})
      .finally(() => setConstraintsLoading(false));
  }, [currentSpaceId, groupId, activeTab]);

  // ── Load settings data ───────────────────────────────────────────────────
  useEffect(() => {
    if (!currentSpaceId || !groupId || activeTab !== "settings") return;
    setDeletedGroupsLoading(true);
    getDeletedGroups(currentSpaceId)
      .then(setDeletedGroups)
      .catch(() => {})
      .finally(() => setDeletedGroupsLoading(false));
  }, [currentSpaceId, groupId, activeTab]);

  // ── Cleanup polling on unmount ───────────────────────────────────────────
  useEffect(() => () => { if (pollingRef.current) clearInterval(pollingRef.current); }, []);

  // ── Schedule handlers ────────────────────────────────────────────────────
  async function handlePublish() {
    if (!currentSpaceId || !draftVersion) return;
    setPublishSaving(true);
    setScheduleVersionError(null);
    try {
      await apiClient.post(`/spaces/${currentSpaceId}/groups/${groupId}/schedule/versions/${draftVersion.id}/publish`, {});
      setDraftVersion(null);
      setScheduleData(null);
      // Reload schedule
      const res = await apiClient.get<{ assignments: ScheduleAssignment[]; draftVersion?: { id: string; status: string } }>(
        `/spaces/${currentSpaceId}/groups/${groupId}/schedule/current`
      );
      setScheduleData(res.data.assignments ?? []);
      setDraftVersion(res.data.draftVersion ?? null);
    } catch {
      setScheduleVersionError("שגיאה בפרסום הסידור");
    } finally {
      setPublishSaving(false);
    }
  }

  async function handleDiscard() {
    if (!currentSpaceId || !draftVersion) return;
    setDiscardSaving(true);
    setScheduleVersionError(null);
    try {
      await apiClient.delete(`/spaces/${currentSpaceId}/groups/${groupId}/schedule/versions/${draftVersion.id}`);
      setDraftVersion(null);
    } catch {
      setScheduleVersionError("שגיאה בביטול הטיוטה");
    } finally {
      setDiscardSaving(false);
    }
  }

  // ── Member handlers ──────────────────────────────────────────────────────
  async function handleRemoveMember(personId: string) {
    if (!currentSpaceId) return;
    try {
      await removeGroupMember(currentSpaceId, groupId, personId);
      setMembers(prev => prev.filter(m => m.personId !== personId));
    } catch {
      setRemoveErrors(prev => ({ ...prev, [personId]: "שגיאה בהסרת חבר" }));
    }
  }

  async function handleAddByEmail(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId) return;
    setAddEmailSaving(true);
    setAddEmailError(null);
    try {
      await addGroupMemberByEmail(currentSpaceId, groupId, addEmailInput);
      setAddEmailInput("");
      setShowAddByEmail(false);
      const updated = await getGroupMembers(currentSpaceId, groupId);
      setMembers(updated);
    } catch {
      setAddEmailError("שגיאה בהוספת חבר");
    } finally {
      setAddEmailSaving(false);
    }
  }

  async function handleCreatePerson(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId) return;
    setCreatePersonSaving(true);
    setCreatePersonError(null);
    try {
      const person = await createPerson(currentSpaceId, createPersonName);
      await addGroupMemberByEmail(currentSpaceId, groupId, person.id);
      setCreatePersonName("");
      setCreatePersonPhone("");
      setShowCreatePerson(false);
      const updated = await getGroupMembers(currentSpaceId, groupId);
      setMembers(updated);
    } catch {
      setCreatePersonError("שגיאה ביצירת אדם");
    } finally {
      setCreatePersonSaving(false);
    }
  }

  async function handleInvite(personId: string) {
    if (!currentSpaceId) return;
    const member = members.find(m => m.personId === personId);
    const contact = member?.phoneNumber ?? "";
    if (!contact) { setInviteError("אין מספר טלפון לשליחת הזמנה"); return; }
    try {
      await invitePerson(currentSpaceId, personId, contact, "whatsapp");
    } catch {
      setInviteError("שגיאה בשליחת הזמנה");
    }
  }

  async function handleSaveMemberEdit(personId: string) {
    if (!currentSpaceId || !memberEditForm) return;
    setMemberEditSaving(true);
    setMemberEditError(null);
    try {
      await updatePersonInfo(currentSpaceId, personId, memberEditForm);
      const updated = await getGroupMembers(currentSpaceId, groupId);
      setMembers(updated);
      const updatedMember = updated.find(m => m.personId === personId);
      if (updatedMember) setSelectedMember(updatedMember);
      setMemberEditForm(null);
    } catch {
      setMemberEditError("שגיאה בשמירת פרטים");
    } finally {
      setMemberEditSaving(false);
    }
  }

  // ── Alert handlers ───────────────────────────────────────────────────────
  async function handleCreateAlert(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId) return;
    setAlertSubmitting(true);
    setAlertSubmitError(null);
    try {
      await createGroupAlert(currentSpaceId, groupId, {
        title: newAlertTitle, body: newAlertBody, severity: newAlertSeverity,
      });
      // Reload alerts to get full DTO
      const updated = await getGroupAlerts(currentSpaceId, groupId);
      setAlerts(updated);
      setNewAlertTitle(""); setNewAlertBody(""); setNewAlertSeverity("info");
      setShowAlertForm(false);
    } catch {
      setAlertSubmitError("שגיאה ביצירת התראה");
    } finally {
      setAlertSubmitting(false);
    }
  }

  async function handleDeleteAlert(id: string) {
    if (!currentSpaceId) return;
    try {
      await deleteGroupAlert(currentSpaceId, groupId, id);
      setAlerts(prev => prev.filter(a => a.id !== id));
    } catch {
      setAlertDeleteErrors(prev => ({ ...prev, [id]: "שגיאה במחיקת התראה" }));
    }
  }

  async function handleUpdateAlert(id: string) {
    if (!currentSpaceId) return;
    setEditAlertSaving(true);
    setEditAlertError(null);
    try {
      await updateGroupAlert(currentSpaceId, groupId, id, {
        title: editAlertTitle, body: editAlertBody, severity: editAlertSeverity,
      });
      setAlerts(prev => prev.map(a => a.id === id ? { ...a, title: editAlertTitle, body: editAlertBody, severity: editAlertSeverity as "info" | "warning" | "critical" } : a));
      setEditingAlertId(null);
    } catch {
      setEditAlertError("שגיאה בעדכון התראה");
    } finally {
      setEditAlertSaving(false);
    }
  }

  // ── Message handlers ─────────────────────────────────────────────────────
  async function handleSendMessage(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId || !newMessageContent.trim()) return;
    setMessageSending(true);
    setMessageError(null);
    try {
      const res = await apiClient.post<{ message: { id: string; content: string; authorName: string; createdAt: string; isPinned: boolean } }>(
        `/spaces/${currentSpaceId}/groups/${groupId}/messages`,
        { content: newMessageContent }
      );
      setMessages(prev => [...prev, res.data.message]);
      setNewMessageContent("");
    } catch {
      setMessageError("שגיאה בשליחת הודעה");
    } finally {
      setMessageSending(false);
    }
  }

  async function handlePinMessage(id: string, isPinned: boolean) {
    if (!currentSpaceId) return;
    try {
      await pinGroupMessage(currentSpaceId, groupId, id, isPinned);
      setMessages(prev => prev.map(m => m.id === id ? { ...m, isPinned } : m));
    } catch {
      setMessagePinErrors(prev => ({ ...prev, [id]: "שגיאה בנעיצת הודעה" }));
    }
  }

  async function handleUpdateMessage(id: string) {
    if (!currentSpaceId) return;
    setEditMessageSaving(true);
    setEditMessageError(null);
    try {
      await updateGroupMessage(currentSpaceId, groupId, id, editMessageContent);
      setMessages(prev => prev.map(m => m.id === id ? { ...m, content: editMessageContent } : m));
      setEditingMessageId(null);
    } catch {
      setEditMessageError("שגיאה בעדכון הודעה");
    } finally {
      setEditMessageSaving(false);
    }
  }

  async function handleDeleteMessage(id: string) {
    if (!currentSpaceId) return;
    try {
      await deleteGroupMessage(currentSpaceId, groupId, id);
      setMessages(prev => prev.filter(m => m.id !== id));
    } catch {}
  }

  // ── Task handlers ────────────────────────────────────────────────────────
  async function handleTaskSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId) return;
    setTaskSaving(true);
    setTaskError(null);
    try {
      if (editingTask) {
        await updateGroupTask(currentSpaceId, groupId, editingTask.id, taskForm);
        setGroupTasks(prev => prev.map(t => t.id === editingTask.id ? { ...t, ...taskForm } : t));
      } else {
        await createGroupTask(currentSpaceId, groupId, taskForm);
        // Reload to get full DTO with id
        const updated = await listGroupTasks(currentSpaceId, groupId);
        setGroupTasks(updated);
      }
      setShowTaskForm(false);
      setEditingTask(null);
      setTaskForm(DEFAULT_TASK_FORM);
    } catch {
      setTaskError("שגיאה בשמירת משימה");
    } finally {
      setTaskSaving(false);
    }
  }

  async function handleDeleteTask(id: string) {
    if (!currentSpaceId) return;
    try {
      await deleteGroupTask(currentSpaceId, groupId, id);
      setGroupTasks(prev => prev.filter(t => t.id !== id));
    } catch {}
  }

  // ── Constraint handlers ──────────────────────────────────────────────────
  async function handleCreateConstraint(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId) return;
    setConstraintSaving(true);
    setConstraintError(null);
    try {
      await createConstraint(
        currentSpaceId,
        "group",
        groupId,
        newConstraintSeverity,
        newConstraintRuleType,
        newConstraintPayload,
        null,
        null
      );
      // Reload to get full DTO
      const updated = await getConstraints(currentSpaceId);
      setConstraints(updated.filter(c => c.scopeId === groupId));
      setShowConstraintForm(false);
    } catch {
      setConstraintError("שגיאה ביצירת אילוץ");
    } finally {
      setConstraintSaving(false);
    }
  }

  async function handleDeleteConstraint(id: string) {
    if (!currentSpaceId) return;
    try {
      await deleteConstraint(currentSpaceId, id);
      setConstraints(prev => prev.filter(c => c.id !== id));
    } catch {
      setConstraintDeleteErrors(prev => ({ ...prev, [id]: "שגיאה במחיקת אילוץ" }));
    }
  }

  async function handleUpdateConstraint(id: string) {
    if (!currentSpaceId) return;
    setEditConstraintSaving(true);
    setEditConstraintError(null);
    try {
      await updateConstraint(currentSpaceId, id, {
        rulePayloadJson: editConstraintPayload,
        effectiveFrom: editConstraintFrom || null,
        effectiveUntil: editConstraintUntil || null,
      });
      setConstraints(prev => prev.map(c => c.id === id ? { ...c, rulePayloadJson: editConstraintPayload, effectiveFrom: editConstraintFrom || null, effectiveUntil: editConstraintUntil || null } : c));
      setEditingConstraintId(null);
    } catch {
      setEditConstraintError("שגיאה בעדכון אילוץ");
    } finally {
      setEditConstraintSaving(false);
    }
  }

  // ── Settings handlers ────────────────────────────────────────────────────
  async function handleRenameGroup() {
    if (!currentSpaceId || !newGroupName.trim()) return;
    setRenameSaving(true);
    setRenameError(null);
    try {
      await renameGroup(currentSpaceId, groupId, newGroupName);
      setGroup(prev => prev ? { ...prev, name: newGroupName } : prev);
    } catch {
      setRenameError("שגיאה בשינוי שם");
    } finally {
      setRenameSaving(false);
    }
  }

  async function handleSaveSettings() {
    if (!currentSpaceId) return;
    setSavingSettings(true);
    setSettingsError(null);
    setSettingsSaved(false);
    try {
      await updateGroupSettings(currentSpaceId, groupId, solverHorizon);
      setSolverHorizonDays(solverHorizon);
      setSettingsSaved(true);
      setTimeout(() => setSettingsSaved(false), 3000);
    } catch {
      setSettingsError("שגיאה בשמירת הגדרות");
    } finally {
      setSavingSettings(false);
    }
  }

  async function handleTriggerSolver() {
    if (!currentSpaceId) return;
    setSolverPolling(true);
    setSolverStatus(null);
    setSolverError(null);
    try {
      const res = await apiClient.post<{ runId: string }>(
        `/spaces/${currentSpaceId}/groups/${groupId}/schedule/runs`, {}
      );
      const runId = res.data.runId;
      pollingRef.current = setInterval(async () => {
        try {
          const statusRes = await apiClient.get<{ status: string }>(
            `/spaces/${currentSpaceId}/groups/${groupId}/schedule/runs/${runId}`
          );
          setSolverStatus(statusRes.data.status);
          if (statusRes.data.status === "Completed" || statusRes.data.status === "Failed") {
            if (pollingRef.current) clearInterval(pollingRef.current);
            setSolverPolling(false);
            if (statusRes.data.status === "Completed") {
              const schedRes = await apiClient.get<{ assignments: ScheduleAssignment[]; draftVersion?: { id: string; status: string } }>(
                `/spaces/${currentSpaceId}/groups/${groupId}/schedule/current`
              );
              setDraftVersion(schedRes.data.draftVersion ?? null);
            }
          }
        } catch {
          if (pollingRef.current) clearInterval(pollingRef.current);
          setSolverPolling(false);
          setSolverError("שגיאה בבדיקת סטטוס");
        }
      }, 3000);
    } catch {
      setSolverPolling(false);
      setSolverError("שגיאה בהפעלת הסולבר");
    }
  }

  async function handleRestoreGroup(id: string) {
    if (!currentSpaceId) return;
    try {
      await restoreGroup(currentSpaceId, id);
      setDeletedGroups(prev => prev.filter(g => g.id !== id));
    } catch {}
  }

  async function handleInitiateTransfer() {
    if (!currentSpaceId || !transferPersonId) return;
    setTransferSaving(true);
    setTransferError(null);
    try {
      await initiateOwnershipTransfer(currentSpaceId, groupId, transferPersonId);
      setHasPendingTransfer(true);
      setTransferPersonId("");
    } catch {
      setTransferError("שגיאה בהעברת בעלות");
    } finally {
      setTransferSaving(false);
    }
  }

  async function handleCancelTransfer() {
    if (!currentSpaceId) return;
    setCancelTransferSaving(true);
    try {
      await cancelOwnershipTransfer(currentSpaceId, groupId);
      setHasPendingTransfer(false);
    } catch {}
    finally { setCancelTransferSaving(false); }
  }

  async function handleDeleteGroup() {
    if (!currentSpaceId) return;
    setDeleteSaving(true);
    setDeleteError(null);
    try {
      await softDeleteGroup(currentSpaceId, groupId);
      router.push("/groups");
    } catch {
      setDeleteError("שגיאה במחיקת קבוצה");
    } finally {
      setDeleteSaving(false);
    }
  }

  // ── Render ───────────────────────────────────────────────────────────────
  if (groupLoading) {
    return (
      <AppShell>
        <div className="flex items-center justify-center min-h-[60vh]">
          <svg className="animate-spin h-8 w-8 text-blue-400" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
        </div>
      </AppShell>
    );
  }

  if (!group) return null;

  const visibleTabs = ALL_TABS.filter(t => isAdmin || !ADMIN_ONLY_TABS.includes(t));
  const avatarColor = getAvatarColor(group.name);
  const avatarLetter = getAvatarLetter(group.name);

  return (
    <AppShell>
      <div className="max-w-4xl mx-auto px-4 py-6 space-y-6" dir="rtl">
        {/* Header */}
        <div className="flex items-center gap-4">
          <Link href="/groups" className="text-slate-400 hover:text-slate-600 transition-colors">
            <svg width="20" height="20" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
            </svg>
          </Link>
          <div
            className="w-10 h-10 rounded-xl flex items-center justify-center text-white text-lg font-bold flex-shrink-0"
            style={{ background: avatarColor }}
          >
            {avatarLetter}
          </div>
          <div>
            <h1 className="text-xl font-bold text-slate-900">{group.name}</h1>
            <p className="text-sm text-slate-400">{group.memberCount ?? 0} חברים</p>
          </div>
        </div>

        {/* Tabs */}
        <div className="flex gap-1 bg-slate-100 p-1 rounded-xl overflow-x-auto">
          {visibleTabs.map(tab => (
            <button
              key={tab}
              onClick={() => setActiveTab(tab)}
              className={`px-3 py-1.5 rounded-lg text-sm font-medium whitespace-nowrap transition-all ${
                activeTab === tab
                  ? "bg-white text-slate-900 shadow-sm"
                  : "text-slate-500 hover:text-slate-700"
              }`}
            >
              {TAB_LABELS[tab]}
            </button>
          ))}
        </div>

        {/* Tab content */}
        <div>
          {activeTab === "schedule" && (
            <ScheduleTab
              groupId={groupId}
              solverHorizonDays={solverHorizonDays}
              scheduleData={scheduleData}
              scheduleLoading={scheduleLoading}
              scheduleError={scheduleError}
              draftVersion={draftVersion}
              isAdmin={isAdmin}
              publishSaving={publishSaving}
              discardSaving={discardSaving}
              scheduleVersionError={scheduleVersionError}
              onOpenDraftModal={() => setShowDraftModal(true)}
              onPublish={handlePublish}
              onDiscard={handleDiscard}
            />
          )}

          {activeTab === "members" && (
            <MembersTab
              isAdmin={isAdmin}
              members={members}
              membersLoading={membersLoading}
              membersError={membersError}
              membersSearch={membersSearch}
              removeErrors={removeErrors}
              onSearchChange={setMembersSearch}
              onSelectMember={m => { setSelectedMember(m); setMemberEditForm(null); }}
              onRemoveMember={handleRemoveMember}
              onOpenAddByEmail={() => setShowAddByEmail(true)}
              onOpenCreatePerson={() => setShowCreatePerson(true)}
              onOpenInvite={handleInvite}
            />
          )}

          {activeTab === "alerts" && (
            <AlertsTab
              isAdmin={isAdmin}
              alerts={alerts}
              alertsLoading={alertsLoading}
              alertsError={alertsError}
              alertDeleteErrors={alertDeleteErrors}
              showAlertForm={showAlertForm}
              newAlertTitle={newAlertTitle}
              newAlertBody={newAlertBody}
              newAlertSeverity={newAlertSeverity}
              alertSubmitting={alertSubmitting}
              alertSubmitError={alertSubmitError}
              editingAlertId={editingAlertId}
              editAlertTitle={editAlertTitle}
              editAlertBody={editAlertBody}
              editAlertSeverity={editAlertSeverity}
              editAlertSaving={editAlertSaving}
              editAlertError={editAlertError}
              onOpenCreateForm={() => setShowAlertForm(true)}
              onCloseCreateForm={() => setShowAlertForm(false)}
              onCreateTitleChange={setNewAlertTitle}
              onCreateBodyChange={setNewAlertBody}
              onCreateSeverityChange={setNewAlertSeverity}
              onCreateSubmit={handleCreateAlert}
              onDeleteAlert={handleDeleteAlert}
              onStartEdit={a => { setEditingAlertId(a.id); setEditAlertTitle(a.title); setEditAlertBody(a.body); setEditAlertSeverity(a.severity); }}
              onCloseEdit={() => setEditingAlertId(null)}
              onEditTitleChange={setEditAlertTitle}
              onEditBodyChange={setEditAlertBody}
              onEditSeverityChange={setEditAlertSeverity}
              onUpdateAlert={handleUpdateAlert}
            />
          )}

          {activeTab === "messages" && (
            <MessagesTab
              isAdmin={isAdmin}
              messages={messages}
              messagesLoading={messagesLoading}
              messagesError={messagesError}
              newMessageContent={newMessageContent}
              messageSending={messageSending}
              messageError={messageError}
              messagePinErrors={messagePinErrors}
              editingMessageId={editingMessageId}
              editMessageContent={editMessageContent}
              editMessageSaving={editMessageSaving}
              editMessageError={editMessageError}
              onNewMessageChange={setNewMessageContent}
              onSendMessage={handleSendMessage}
              onPinMessage={handlePinMessage}
              onStartEditMessage={(id, content) => { setEditingMessageId(id); setEditMessageContent(content); }}
              onCloseEditMessage={() => setEditingMessageId(null)}
              onEditMessageContentChange={setEditMessageContent}
              onUpdateMessage={handleUpdateMessage}
              onDeleteMessage={handleDeleteMessage}
            />
          )}

          {activeTab === "tasks" && (
            <TasksTab
              isAdmin={isAdmin}
              groupTasks={groupTasks}
              groupTasksLoading={groupTasksLoading}
              showTaskForm={showTaskForm}
              editingTask={editingTask}
              taskForm={taskForm}
              taskSaving={taskSaving}
              taskError={taskError}
              onOpenCreate={() => { setEditingTask(null); setTaskForm(DEFAULT_TASK_FORM); setShowTaskForm(true); }}
              onCloseForm={() => { setShowTaskForm(false); setEditingTask(null); }}
              onFormChange={setTaskForm}
              onFormSubmit={handleTaskSubmit}
              onEditTask={t => { setEditingTask(t); setTaskForm({ name: t.name, startsAt: t.startsAt?.slice(0, 16) ?? "", endsAt: t.endsAt?.slice(0, 16) ?? "", shiftDurationMinutes: t.shiftDurationMinutes, requiredHeadcount: t.requiredHeadcount, burdenLevel: t.burdenLevel, allowsDoubleShift: t.allowsDoubleShift, allowsOverlap: t.allowsOverlap }); setShowTaskForm(true); }}
              onDeleteTask={handleDeleteTask}
            />
          )}

          {activeTab === "constraints" && (
            <ConstraintsTab
              isAdmin={isAdmin}
              constraints={constraints}
              constraintsLoading={constraintsLoading}
              constraintDeleteErrors={constraintDeleteErrors}
              groupTasks={groupTasks}
              showConstraintForm={showConstraintForm}
              newConstraintRuleType={newConstraintRuleType}
              newConstraintSeverity={newConstraintSeverity}
              newConstraintPayload={newConstraintPayload}
              constraintSaving={constraintSaving}
              constraintError={constraintError}
              editingConstraintId={editingConstraintId}
              editConstraintPayload={editConstraintPayload}
              editConstraintFrom={editConstraintFrom}
              editConstraintUntil={editConstraintUntil}
              editConstraintSaving={editConstraintSaving}
              editConstraintError={editConstraintError}
              onOpenCreate={() => setShowConstraintForm(true)}
              onCloseCreate={() => setShowConstraintForm(false)}
              onRuleTypeChange={rt => { setNewConstraintRuleType(rt); const defaults: Record<string, string> = { min_rest_hours: '{"hours": 8}', max_kitchen_per_week: '{"max": 2, "task_type_name": "kitchen"}', no_consecutive_burden: '{"burden_level": "disliked"}', min_base_headcount: '{"min": 3, "window_hours": 24}', no_task_type_restriction: '{"task_type_id": ""}' }; setNewConstraintPayload(defaults[rt] ?? "{}"); }}
              onSeverityChange={setNewConstraintSeverity}
              onPayloadChange={setNewConstraintPayload}
              onCreateSubmit={handleCreateConstraint}
              onDeleteConstraint={handleDeleteConstraint}
              onStartEdit={c => { setEditingConstraintId(c.id); setEditConstraintPayload(c.rulePayloadJson); setEditConstraintFrom(c.effectiveFrom?.slice(0, 10) ?? ""); setEditConstraintUntil(c.effectiveUntil?.slice(0, 10) ?? ""); }}
              onCloseEdit={() => setEditingConstraintId(null)}
              onEditPayloadChange={setEditConstraintPayload}
              onEditFromChange={setEditConstraintFrom}
              onEditUntilChange={setEditConstraintUntil}
              onUpdateConstraint={handleUpdateConstraint}
            />
          )}

          {activeTab === "settings" && (
            <SettingsTab
              isAdmin={isAdmin}
              groupId={groupId}
              newGroupName={newGroupName}
              renameSaving={renameSaving}
              renameError={renameError}
              solverHorizon={solverHorizon}
              savingSettings={savingSettings}
              settingsError={settingsError}
              settingsSaved={settingsSaved}
              solverPolling={solverPolling}
              solverStatus={solverStatus}
              solverError={solverError}
              draftVersion={draftVersion}
              deletedGroups={deletedGroups}
              deletedGroupsLoading={deletedGroupsLoading}
              members={members}
              transferPersonId={transferPersonId}
              transferSaving={transferSaving}
              transferError={transferError}
              hasPendingTransfer={hasPendingTransfer}
              cancelTransferSaving={cancelTransferSaving}
              showDeleteConfirm={showDeleteConfirm}
              deleteSaving={deleteSaving}
              deleteError={deleteError}
              onGroupNameChange={setNewGroupName}
              onRenameGroup={handleRenameGroup}
              onSolverHorizonChange={setSolverHorizon}
              onSaveSettings={handleSaveSettings}
              onTriggerSolver={handleTriggerSolver}
              onOpenDraftModal={() => setShowDraftModal(true)}
              onRestoreGroup={handleRestoreGroup}
              onTransferPersonChange={setTransferPersonId}
              onInitiateTransfer={handleInitiateTransfer}
              onCancelTransfer={handleCancelTransfer}
              onShowDeleteConfirm={setShowDeleteConfirm}
              onDeleteGroup={handleDeleteGroup}
            />
          )}
        </div>
      </div>

      {/* Draft schedule modal */}
      {showDraftModal && draftVersion && currentSpaceId && (
        <DraftScheduleModal
          open={showDraftModal}
          onClose={() => setShowDraftModal(false)}
          spaceId={currentSpaceId}
          draftVersionId={draftVersion.id}
          isAdmin={isAdmin}
          onPublish={async () => { await handlePublish(); setShowDraftModal(false); }}
          onDiscard={async () => { await handleDiscard(); setShowDraftModal(false); }}
          onRunAgain={() => { setShowDraftModal(false); setActiveTab("settings"); }}
        />
      )}

      {/* Add by email modal */}
      <Modal title="הוסף חבר לפי אימייל/טלפון" open={showAddByEmail} onClose={() => setShowAddByEmail(false)}>
        <form onSubmit={handleAddByEmail} className="space-y-4">
          <input
            type="text"
            value={addEmailInput}
            onChange={e => setAddEmailInput(e.target.value)}
            placeholder="אימייל או מספר טלפון"
            required
            className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          {addEmailError && <p className="text-sm text-red-600">{addEmailError}</p>}
          <button type="submit" disabled={addEmailSaving}
            className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
            {addEmailSaving ? "מוסיף..." : "הוסף"}
          </button>
        </form>
      </Modal>

      {/* Create person modal */}
      <Modal title="הוסף אדם לפי שם" open={showCreatePerson} onClose={() => setShowCreatePerson(false)}>
        <form onSubmit={handleCreatePerson} className="space-y-4">
          <input
            type="text"
            value={createPersonName}
            onChange={e => setCreatePersonName(e.target.value)}
            placeholder="שם מלא *"
            required
            className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          <input
            type="tel"
            value={createPersonPhone}
            onChange={e => setCreatePersonPhone(e.target.value)}
            placeholder="מספר טלפון (אופציונלי)"
            className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          {createPersonError && <p className="text-sm text-red-600">{createPersonError}</p>}
          <button type="submit" disabled={createPersonSaving}
            className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
            {createPersonSaving ? "יוצר..." : "צור"}
          </button>
        </form>
      </Modal>

      {/* Member profile modal */}
      {selectedMember && (
        <MemberProfileModal
          member={selectedMember}
          isAdmin={isAdmin}
          editForm={memberEditForm}
          saving={memberEditSaving}
          error={memberEditError}
          onClose={() => { setSelectedMember(null); setMemberEditForm(null); }}
          onStartEdit={() => setMemberEditForm({
            fullName: selectedMember.fullName,
            displayName: selectedMember.displayName ?? "",
            phoneNumber: selectedMember.phoneNumber ?? "",
            profileImageUrl: selectedMember.profileImageUrl ?? "",
            birthday: "",
          })}
          onCancelEdit={() => setMemberEditForm(null)}
          onChangeForm={setMemberEditForm}
          onSave={handleSaveMemberEdit}
        />
      )}

      {inviteError && (
        <div className="fixed bottom-4 right-4 bg-red-50 border border-red-200 text-red-700 text-sm px-4 py-3 rounded-xl shadow-lg">
          {inviteError}
        </div>
      )}
    </AppShell>
  );
}
