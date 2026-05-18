"use client";

import React, { useEffect, useRef, useState, lazy, Suspense } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { useTranslations } from "next-intl";
import AppShell from "@/components/shell/AppShell";
import Modal from "@/components/Modal";
import DraftScheduleModal from "@/components/DraftScheduleModal";
import SandboxView from "@/components/sandbox/SandboxView";
import ImportModal from "@/components/ImportModal";
import TrialBanner from "@/components/billing/TrialBanner";
import ReAuthDialog from "@/components/admin/ReAuthDialog";
import ScheduleTab from "./tabs/ScheduleTab";
import MembersTab, { MemberProfileModal } from "./tabs/MembersTab";

// Lazy-load less-frequently-used tabs for faster initial load
const AlertsTab = lazy(() => import("./tabs/AlertsTab"));
const MessagesTab = lazy(() => import("./tabs/MessagesTab"));
const TasksTab = lazy(() => import("./tabs/TasksTab"));
const ConstraintsTab = lazy(() => import("./tabs/ConstraintsTab"));
const SettingsTab = lazy(() => import("./tabs/SettingsTab"));
const StatsTab = lazy(() => import("./tabs/StatsTab"));
const QualificationsTab = lazy(() => import("./tabs/QualificationsTab"));
const RolesTab = lazy(() => import("./tabs/RolesTab"));
const LiveStatusPanel = lazy(() => import("@/components/schedule/LiveStatusPanel"));
const HomeLeaveScheduleTable = lazy(() => import("@/components/home-leave/HomeLeaveScheduleTable"));
import { ActiveTab, ADMIN_ONLY_TABS, ScheduleAssignment } from "./types";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useAuthStore } from "@/lib/store/authStore";
import { useAdminSessionStore } from "@/lib/store/adminSessionStore";
import { formatLocalDateTime } from "@/lib/utils/formatTime";
import { isWebAuthnSupported, listCredentials } from "@/lib/webauthn";
import { useRefetchNotifications } from "@/lib/query/hooks/useNotifications";
import {
  getGroups, getGroupMembers, addGroupMemberByEmail, removeGroupMember,
  updateGroupSettings, renameGroup, softDeleteGroup, restoreGroup,
  updateGroup,
  getDeletedGroups, initiateOwnershipTransfer, cancelOwnershipTransfer,
  GroupWithMemberCountDto, GroupMemberDto, DeletedGroupDto,
  getGroupAlerts, createGroupAlert, deleteGroupAlert, updateGroupAlert, GroupAlertDto,
  updateGroupMessage, deleteGroupMessage, pinGroupMessage,
  updatePersonInfo,
  getGroupRoles, createGroupRole, updateGroupRole, deactivateGroupRole,
  updateMemberRole,
  getGroupSchedule,
  getGroupQualifications, createGroupQualification, deactivateGroupQualification,
  getMemberQualifications, assignMemberQualification, removeMemberQualification,
  GroupQualificationDto, MemberQualificationDto,
} from "@/lib/api/groups";
import { getAvatarColor, getAvatarLetter } from "@/lib/utils/groupAvatar";
import { listGroupTasks, createGroupTask, updateGroupTask, deleteGroupTask, GroupTaskDto } from "@/lib/api/tasks";
import { getConstraints, createConstraint, updateConstraint, deleteConstraint, ConstraintDto } from "@/lib/api/constraints";
import { createPerson, invitePerson, searchPeople } from "@/lib/api/people";
import { addGroupMemberById } from "@/lib/api/groups";
import { apiClient } from "@/lib/api/client";
import { DEFAULT_TASK_HORIZON_DAYS, MS_PER_DAY } from "@/lib/utils/constants";
import type { TaskForm } from "./tabs/TasksTab";
import { useGroupPageState, DEFAULT_TASK_FORM } from "./useGroupPageState";
// ── Tab labels ───────────────────────────────────────────────────────────────
function getTabLabels(t: (key: string) => string): Record<ActiveTab, string> {
  return {
    schedule: t("tabs.schedule"),
    members: t("tabs.members"),
    qualifications: t("tabs.qualifications"),
    roles: t("tabs.roles"),
    alerts: t("tabs.alerts"),
    messages: t("tabs.messages"),
    tasks: t("tabs.tasks"),
    constraints: t("tabs.constraints"),
    settings: t("tabs.settings"),
    stats: t("tabs.stats"),
    "live-status": t("tabs.liveStatus"),
  };
}

const ALL_TABS: ActiveTab[] = ["schedule", "live-status", "members", "qualifications", "roles", "alerts", "messages", "tasks", "constraints", "stats", "settings"];

// ── Main component ───────────────────────────────────────────────────────────
export default function GroupDetailPage() {
  const params = useParams();
  const router = useRouter();
  const groupId = params?.groupId as string;
  const { currentSpaceId } = useSpaceStore();
  const { userId, displayName, isAdminForGroup, adminGroupId, enterAdminMode, exitAdminMode, timezoneId } = useAuthStore();
  const refetchNotifications = useRefetchNotifications(currentSpaceId);
  const tGroups = useTranslations("groups");
  const tErrors = useTranslations("errors");
  const tAdmin = useTranslations("admin");
  const tReAuth = useTranslations("reAuth");
  const TAB_LABELS = getTabLabels(tGroups);

  // ── All state via hook ───────────────────────────────────────────────────
  const s = useGroupPageState();
  // Destructure for convenience — keeps handler code readable
  const {
    group, setGroup, groupLoading, setGroupLoading, activeTab, setActiveTab, isAdmin, setIsAdmin,
    scheduleData, setScheduleData, scheduleLoading, setScheduleLoading,
    scheduleError, setScheduleError, scheduleIsOffline, setScheduleIsOffline,
    draftVersion, setDraftVersion, lastRunSummary, setLastRunSummary,
    showDraftModal, setShowDraftModal, publishSaving, setPublishSaving,
    discardSaving, setDiscardSaving, scheduleVersionError, setScheduleVersionError,
    solverHorizonDays, setSolverHorizonDays,
    members, setMembers, membersLoading, setMembersLoading,
    membersError, setMembersError, membersSearch, setMembersSearch,
    removeErrors, setRemoveErrors, selectedMember, setSelectedMember,
    memberEditForm, setMemberEditForm, memberEditSaving, setMemberEditSaving,
    memberEditError, setMemberEditError,
    showAddMember, setShowAddMember, addMemberName, setAddMemberName,
    addMemberPhone, setAddMemberPhone, addMemberEmail, setAddMemberEmail,
    addMemberSaving, setAddMemberSaving, addMemberError, setAddMemberError,
    alerts, setAlerts, alertsLoading, setAlertsLoading,
    alertsError, setAlertsError, alertDeleteErrors, setAlertDeleteErrors,
    showAlertForm, setShowAlertForm, newAlertTitle, setNewAlertTitle,
    newAlertBody, setNewAlertBody, newAlertSeverity, setNewAlertSeverity,
    alertSubmitting, setAlertSubmitting, alertSubmitError, setAlertSubmitError,
    editingAlertId, setEditingAlertId, editAlertTitle, setEditAlertTitle,
    editAlertBody, setEditAlertBody, editAlertSeverity, setEditAlertSeverity,
    editAlertSaving, setEditAlertSaving, editAlertError, setEditAlertError,
    messages, setMessages, messagesLoading, setMessagesLoading,
    messagesError, setMessagesError, newMessageContent, setNewMessageContent,
    messageSending, setMessageSending, messageError, setMessageError,
    messagePinErrors, setMessagePinErrors, editingMessageId, setEditingMessageId,
    editMessageContent, setEditMessageContent, editMessageSaving, setEditMessageSaving,
    editMessageError, setEditMessageError,
    showImportModal, setShowImportModal, importMode, setImportMode,
    groupTasks, setGroupTasks, groupTasksLoading, setGroupTasksLoading,
    showTaskForm, setShowTaskForm, editingTask, setEditingTask,
    taskForm, setTaskForm, taskSaving, setTaskSaving, taskError, setTaskError,
    constraints, setConstraints, constraintsLoading, setConstraintsLoading,
    constraintDeleteErrors, setConstraintDeleteErrors,
    showConstraintForm, setShowConstraintForm,
    newConstraintRuleType, setNewConstraintRuleType,
    newConstraintSeverity, setNewConstraintSeverity,
    newConstraintPayload, setNewConstraintPayload,
    newConstraintFrom, setNewConstraintFrom, newConstraintUntil, setNewConstraintUntil,
    constraintSaving, setConstraintSaving, constraintError, setConstraintError,
    editingConstraintId, setEditingConstraintId,
    editConstraintPayload, setEditConstraintPayload,
    editConstraintFrom, setEditConstraintFrom, editConstraintUntil, setEditConstraintUntil,
    editConstraintSeverity, setEditConstraintSeverity,
    editConstraintSaving, setEditConstraintSaving, editConstraintError, setEditConstraintError,
    newGroupName, setNewGroupName, renameSaving, setRenameSaving, renameError, setRenameError,
    solverHorizon, setSolverHorizon, savingSettings, setSavingSettings,
    settingsError, setSettingsError, settingsSaved, setSettingsSaved,
    solverStartDateTime, setSolverStartDateTime,
    autoPublish, setAutoPublish,
    isClosedBase, setIsClosedBase,
    minRestBetweenShiftsHours, setMinRestBetweenShiftsHours,
    managementTimeoutMinutes, setManagementTimeoutMinutes,
    solverPolling, setSolverPolling, solverStatus, setSolverStatus, solverError, setSolverError,
    deletedGroups, setDeletedGroups, deletedGroupsLoading, setDeletedGroupsLoading,
    transferPersonId, setTransferPersonId, transferSaving, setTransferSaving,
    transferError, setTransferError, hasPendingTransfer, setHasPendingTransfer,
    cancelTransferSaving, setCancelTransferSaving,
    showDeleteConfirm, setShowDeleteConfirm, deleteSaving, setDeleteSaving, deleteError, setDeleteError,
    groupRoles, setGroupRoles, groupRolesLoading, setGroupRolesLoading,
    constraintTaskOptions, setConstraintTaskOptions,
    groupQualifications, setGroupQualifications,
    memberQualifications, setMemberQualifications, qualificationsLoading, setQualificationsLoading,
    pollingRef,
  } = s;

  // ── Re-evaluate admin state when adminGroupId changes ───────────────────
  useEffect(() => {
    setIsAdmin(adminGroupId === groupId);
  }, [adminGroupId, groupId]);

  // ── Re-authentication dialog state for management mode entry ────────────
  const [showReAuthDialog, setShowReAuthDialog] = useState(false);
  const [hasCredentials, setHasCredentials] = useState<boolean | null>(null); // null = loading
  const { enterElevatedMode } = useAdminSessionStore();

  // Check if user has credentials configured (password or WebAuthn)
  useEffect(() => {
    let cancelled = false;

    async function checkCredentials() {
      // All registered users have a password in this system
      let hasWebAuthn = false;
      if (isWebAuthnSupported()) {
        try {
          const creds = await listCredentials();
          hasWebAuthn = creds.length > 0;
        } catch {
          hasWebAuthn = false;
        }
      }
      // User always has a password (system invariant), so credentials are always available
      if (!cancelled) {
        setHasCredentials(true || hasWebAuthn);
      }
    }

    checkCredentials();
    return () => { cancelled = true; };
  }, []);

  // Handle management mode toggle with re-authentication
  const handleAdminModeToggle = () => {
    if (isAdmin) {
      // Exiting admin mode — no re-auth needed
      exitAdminMode();
      setIsAdmin(false);
    } else {
      // Entering admin mode — require re-authentication
      if (hasCredentials === false) return; // Button should be disabled, but guard anyway
      setShowReAuthDialog(true);
    }
  };

  const handleReAuthSuccess = () => {
    setShowReAuthDialog(false);
    const timeoutMinutes = group?.managementTimeoutMinutes ?? 15;
    enterAdminMode(groupId);
    enterElevatedMode("management", groupId, timeoutMinutes);
    setIsAdmin(true);
  };

  const handleReAuthCancel = () => {
    setShowReAuthDialog(false);
    // Remain in standard view — do nothing
  };

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
        setAutoPublish(found.autoPublish ?? false);
        setIsClosedBase(found.isClosedBase ?? false);
        setMinRestBetweenShiftsHours(found.minRestBetweenShiftsHours ?? 8);
        setManagementTimeoutMinutes(found.managementTimeoutMinutes ?? 15);
        // Initialise the configured auto-scheduler start date from the API
        // The API stores UTC; convert to local time for the datetime-local input
        setSolverStartDateTime(found.solverStartDateTime
          ? (() => {
              const d = new Date(found.solverStartDateTime!);
              const pad = (n: number) => String(n).padStart(2, "0");
              return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
            })()
          : null);
        // Use adminGroupId directly — isAdminForGroup is a derived function that
        // changes reference on every render and would cause an infinite loop in deps.
        setIsAdmin(adminGroupId === groupId);
      })
      .catch(() => router.push("/groups"))
      .finally(() => setGroupLoading(false));
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentSpaceId, groupId, userId, adminGroupId]);

  // ── Load schedule ────────────────────────────────────────────────────────
  useEffect(() => {
    if (!currentSpaceId || !groupId || activeTab !== "schedule") return;
    setScheduleLoading(true);
    setScheduleError(null);
    setScheduleIsOffline(false);

    const cacheKey = `schedule:${currentSpaceId}:${groupId}`;

    // Try to fetch fresh data first. Only fall back to cache if offline/network error.
    // Capture the error so we can distinguish server errors (5xx) from network failures.
    let scheduleError: unknown = null;
    Promise.all([
      getGroupSchedule(currentSpaceId, groupId).catch(e => { scheduleError = e; return null; }),
      apiClient.get<Array<{ id: string; status: string; summaryJson?: string | null; sourceRunId?: string | null }>>(
        `/spaces/${currentSpaceId}/schedule-versions?status=draft`
      ).catch(() => ({ data: [] as Array<{ id: string; status: string; summaryJson?: string | null; sourceRunId?: string | null }> })),
      apiClient.get<Array<{ id: string; status: string; summaryJson?: string | null }>>(
        `/spaces/${currentSpaceId}/schedule-versions?status=discarded`
      ).catch(() => ({ data: [] as Array<{ id: string; status: string; summaryJson?: string | null }> })),
    ]).then(([groupAssignments, draftRes, discardedRes]) => {
      if (groupAssignments !== null) {
        // Fresh data — update display and cache
        setScheduleData(groupAssignments);
        setScheduleError(null);
        try {
          localStorage.setItem(cacheKey, JSON.stringify({
            assignments: groupAssignments,
            cachedAt: new Date().toISOString(),
          }));
        } catch { /* storage full */ }
      } else {
        // Check if this was a server error (has HTTP status) vs a real network failure
        const httpStatus = (scheduleError as { response?: { status?: number } })?.response?.status;
        const isServerError = httpStatus !== undefined;

        if (isServerError) {
          // Server returned an error — don't show "no internet", show a real error message
          setScheduleError(tErrors("errorLoadSchedule"));
        } else {
          // Network failed — try cache
          try {
            const cached = localStorage.getItem(cacheKey);
            if (cached) {
              const { assignments: cachedAssignments, cachedAt } = JSON.parse(cached);
              if (Array.isArray(cachedAssignments)) {
                setScheduleData(cachedAssignments);
                const cachedDate = cachedAt
                  ? formatLocalDateTime(cachedAt, timezoneId)
                  : "";
                setScheduleIsOffline(true);
                setScheduleError(tErrors("offlineWithCache", { date: cachedDate }));
              } else {
                setScheduleIsOffline(true);
                setScheduleError(tErrors("offlineNoCache"));
              }
            } else {
              setScheduleIsOffline(true);
              setScheduleError(tErrors("offlineNoCache"));
            }
          } catch {
            setScheduleError(tErrors("errorLoadSchedule"));
          }
        }
      }

      const drafts = Array.isArray(draftRes?.data) ? draftRes.data : [];
      setDraftVersion(drafts.length > 0 ? drafts[0] : null);
      if (drafts.length === 0) {
        const discarded = Array.isArray(discardedRes?.data) ? discardedRes.data : [];
        setLastRunSummary(discarded[0]?.summaryJson ?? null);
      } else {
        setLastRunSummary(null);
      }
    })
    .catch(() => {
      // Full failure — try cache
      try {
        const cached = localStorage.getItem(cacheKey);
        if (cached) {
          const { assignments: cachedAssignments, cachedAt } = JSON.parse(cached);
          if (Array.isArray(cachedAssignments)) {
            setScheduleData(cachedAssignments);
            const cachedDate = cachedAt
              ? formatLocalDateTime(cachedAt, timezoneId)
              : "";
            setScheduleIsOffline(true);
            setScheduleError(tErrors("offlineWithCache", { date: cachedDate }));
          } else {
            setScheduleError(tErrors("errorLoadSchedule"));
          }
        } else {
          setScheduleError(tErrors("errorLoadSchedule"));
        }
      } catch {
        setScheduleError(tErrors("errorLoadSchedule"));
      }
    })
    .finally(() => setScheduleLoading(false));
  }, [currentSpaceId, groupId, activeTab]);

  // ── Load members ─────────────────────────────────────────────────────────
  useEffect(() => {
    if (!currentSpaceId || !groupId) return;
    // Load members eagerly — needed for schedule tab filtering AND members tab
    if (activeTab !== "members" && activeTab !== "schedule" && members.length > 0) return;
    setMembersLoading(true);
    setMembersError(null);
    getGroupMembers(currentSpaceId, groupId)
      .then(setMembers)
      .catch(() => setMembersError(tErrors("errorLoadMembers")))
      .finally(() => setMembersLoading(false));
  }, [currentSpaceId, groupId, activeTab]);

  // ── Load group roles when members tab opens (needed for role dropdown) ───
  useEffect(() => {
    if (!currentSpaceId || !groupId || activeTab !== "members") return;
    if (groupRoles.length > 0) return; // already loaded
    getGroupRoles(currentSpaceId, groupId)
      .then(setGroupRoles)
      .catch(() => {});
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentSpaceId, groupId, activeTab]);
  // ── Load alerts ──────────────────────────────────────────────────────────
  useEffect(() => {
    if (!currentSpaceId || !groupId || activeTab !== "alerts") return;
    setAlertsLoading(true);
    setAlertsError(null);
    getGroupAlerts(currentSpaceId, groupId)
      .then(setAlerts)
      .catch(() => setAlertsError(tErrors("errorLoadAlerts")))
      .finally(() => setAlertsLoading(false));
  }, [currentSpaceId, groupId, activeTab]);

  // ── Load messages ────────────────────────────────────────────────────────
  useEffect(() => {
    if (!currentSpaceId || !groupId || activeTab !== "messages") return;
    setMessagesLoading(true);
    setMessagesError(null);
    apiClient.get<{ id: string; content: string; authorName: string; createdAt: string; isPinned: boolean }[]>(
      `/spaces/${currentSpaceId}/groups/${groupId}/messages`
    )
      .then(res => {
        // API returns array directly
        const data = Array.isArray(res.data) ? res.data : [];
        setMessages(data);
      })
      .catch(() => setMessagesError(tErrors("errorLoadMessages")))
      .finally(() => setMessagesLoading(false));
  }, [currentSpaceId, groupId, activeTab]);

  // ── Load tasks ───────────────────────────────────────────────────────────
  useEffect(() => {
    if (!currentSpaceId || !groupId || activeTab !== "tasks") return;
    setGroupTasksLoading(true);
    Promise.all([
      listGroupTasks(currentSpaceId, groupId),
      groupQualifications.length === 0
        ? getGroupQualifications(currentSpaceId, groupId)
        : Promise.resolve(groupQualifications),
    ])
      .then(([tasks, quals]) => {
        setGroupTasks(tasks);
        setGroupQualifications(quals);
      })
      .catch(() => {})
      .finally(() => setGroupTasksLoading(false));
  }, [currentSpaceId, groupId, activeTab]);

  // ── Load constraints (+ roles + tasks if not yet loaded) ────────────────
  useEffect(() => {
    if (!currentSpaceId || !groupId || activeTab !== "constraints") return;
    setConstraintsLoading(true);
    Promise.all([
      getConstraints(currentSpaceId),
      groupRoles.length === 0
        ? getGroupRoles(currentSpaceId, groupId)
        : Promise.resolve(groupRoles),
      listGroupTasks(currentSpaceId, groupId),
    ])
      .then(([c, r, tasks]) => {
        setConstraints(c);
        setGroupRoles(r);
        // Build task options for the no_task_type_restriction dropdown
        setConstraintTaskOptions(tasks.map(t => ({ id: t.id, name: t.name })));
      })
      .catch(() => {})
      .finally(() => setConstraintsLoading(false));
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentSpaceId, groupId, activeTab]);

  // ── Load settings data ───────────────────────────────────────────────────
  useEffect(() => {
    if (!currentSpaceId || !groupId || activeTab !== "settings") return;
    setDeletedGroupsLoading(true);
    getDeletedGroups(currentSpaceId)
      .then(setDeletedGroups)
      .catch(() => {})
      .finally(() => setDeletedGroupsLoading(false));
    // Also load group roles when settings tab opens
    setGroupRolesLoading(true);
    getGroupRoles(currentSpaceId, groupId)
      .then(setGroupRoles)
      .catch(() => {})
      .finally(() => setGroupRolesLoading(false));
  }, [currentSpaceId, groupId, activeTab]);

  // ── Load qualifications ──────────────────────────────────────────────────
  useEffect(() => {
    if (!currentSpaceId || !groupId || activeTab !== "qualifications") return;
    setQualificationsLoading(true);
    Promise.all([
      getGroupQualifications(currentSpaceId, groupId),
      getMemberQualifications(currentSpaceId, groupId),
    ])
      .then(([quals, memberQuals]) => {
        setGroupQualifications(quals);
        setMemberQualifications(memberQuals);
      })
      .catch(() => {})
      .finally(() => setQualificationsLoading(false));
  }, [currentSpaceId, groupId, activeTab]);

  // ── Load roles tab ───────────────────────────────────────────────────────
  useEffect(() => {
    if (!currentSpaceId || !groupId || activeTab !== "roles") return;
    setGroupRolesLoading(true);
    Promise.all([
      getGroupRoles(currentSpaceId, groupId),
      members.length === 0 ? getGroupMembers(currentSpaceId, groupId) : Promise.resolve(members),
    ])
      .then(([roles, m]) => {
        setGroupRoles(roles);
        if (members.length === 0) setMembers(m);
      })
      .catch(() => {})
      .finally(() => setGroupRolesLoading(false));
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentSpaceId, groupId, activeTab]);

  // ── Cleanup polling on unmount ───────────────────────────────────────────
  useEffect(() => {
    return () => {
      exitAdminMode();
      if (pollingRef.current) clearInterval(pollingRef.current);
    };
  }, []);

  // ── Schedule handlers ────────────────────────────────────────────────────
  async function handlePublish() {
    // Read from store directly at call time — avoids stale closure if spaceId
    // wasn't set yet during the initial render (Zustand hydration lag).
    const spaceId = useSpaceStore.getState().currentSpaceId ?? currentSpaceId;
    if (!spaceId || !draftVersion) {
      const msg = "לא ניתן לפרסם — נסה לרענן את הדף";
      setScheduleVersionError(msg);
      throw new Error(msg);
    }
    // Double-submit guard — if already saving, do nothing
    if (publishSaving) {
      return;
    }
    setPublishSaving(true);
    setScheduleVersionError(null);
    try {
      await apiClient.post(`/spaces/${spaceId}/schedule-versions/${draftVersion.id}/publish`, {});
      setDraftVersion(null);
      // Small delay to ensure DB consistency after publish
      await new Promise(r => setTimeout(r, 500));
      // Reload schedule using the same function the schedule tab uses
      const [groupAssignments, draftRes] = await Promise.all([
        getGroupSchedule(spaceId, groupId).catch(() => [] as ScheduleAssignment[]),
        apiClient.get<Array<{ id: string; status: string }>>(
          `/spaces/${spaceId}/schedule-versions?status=draft`
        ).catch(() => ({ data: [] as Array<{ id: string; status: string }> })),
      ]);
      setScheduleData(groupAssignments.length > 0 ? groupAssignments : scheduleData);
      const drafts = Array.isArray(draftRes?.data) ? draftRes.data : [];
      setDraftVersion(drafts.length > 0 ? drafts[0] : null);
    } catch (err) {
      setScheduleVersionError(tErrors("errorPublish"));
      throw err; // re-throw so DraftScheduleModal can show the error too
    } finally {
      setPublishSaving(false);
    }
  }

  async function handleDiscard() {
    if (!currentSpaceId || !draftVersion) return;
    setDiscardSaving(true);
    setScheduleVersionError(null);
    try {
      await apiClient.delete(`/spaces/${currentSpaceId}/schedule-versions/${draftVersion.id}`);
      setDraftVersion(null);
    } catch {
      setScheduleVersionError(tErrors("errorDiscard"));
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
      setRemoveErrors(prev => ({ ...prev, [personId]: tErrors("errorRemoveMember") }));
    }
  }

  async function handleAddMember(e: React.FormEvent) {
    e.preventDefault();
    if (!currentSpaceId || !addMemberName.trim()) return;
    setAddMemberSaving(true);
    setAddMemberError(null);
    try {
      let personId: string;

      // First, search for an existing person with this name to avoid a 409
      const existingResults = await searchPeople(currentSpaceId, addMemberName.trim());
      const existingMatch = existingResults.find(p =>
        p.fullName.toLowerCase() === addMemberName.trim().toLowerCase()
      );

      if (existingMatch) {
        // Person already exists in this space — use their ID directly
        personId = existingMatch.id;
      } else {
        // Person doesn't exist yet — create them
        const person = await createPerson(currentSpaceId, addMemberName.trim());
        personId = person.id;
      }

      // Add to group by ID (idempotent on the backend)
      await addGroupMemberById(currentSpaceId, groupId, personId);
      // If phone or email provided, send invitation
      if (addMemberPhone.trim()) {
        try { await invitePerson(currentSpaceId, personId, addMemberPhone.trim(), "whatsapp"); } catch { /* non-fatal */ }
      } else if (addMemberEmail.trim()) {
        try { await invitePerson(currentSpaceId, personId, addMemberEmail.trim(), "email"); } catch { /* non-fatal */ }
      }
      setAddMemberName(""); setAddMemberPhone(""); setAddMemberEmail("");
      setShowAddMember(false);
      const updated = await getGroupMembers(currentSpaceId, groupId);
      setMembers(updated);
    } catch {
      setAddMemberError(tErrors("errorAddMember"));
    } finally {
      setAddMemberSaving(false);
    }
  }

  async function handleInvite(personId: string) {
    if (!currentSpaceId) return;
    const member = members.find(m => m.personId === personId);
    const contact = member?.phoneNumber ?? "";
    if (!contact) return;
    try {
      await invitePerson(currentSpaceId, personId, contact, "whatsapp");
    } catch { /* non-fatal */ }
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
      setMemberEditError(tErrors("errorSaveDetails"));
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
      setAlertSubmitError(tErrors("errorCreateAlert"));
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
      setAlertDeleteErrors(prev => ({ ...prev, [id]: tErrors("errorDeleteAlert") }));
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
      setEditAlertError(tErrors("errorUpdateAlert"));
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
      await apiClient.post(
        `/spaces/${currentSpaceId}/groups/${groupId}/messages`,
        { content: newMessageContent }
      );
      setNewMessageContent("");
      // Reload messages to get the full DTO with authorName etc.
      const res = await apiClient.get<{ id: string; content: string; authorName: string; createdAt: string; isPinned: boolean }[]>(
        `/spaces/${currentSpaceId}/groups/${groupId}/messages`
      );
      setMessages(Array.isArray(res.data) ? res.data : []);
    } catch {
      setMessageError("Error sending message");
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
      setMessagePinErrors(prev => ({ ...prev, [id]: "Error pinning message" }));
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
      setEditMessageError("Error updating message");
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
      // Default startsAt to today if empty
      const now = new Date();
      const startsAt = taskForm.startsAt
        ? new Date(taskForm.startsAt).toISOString()
        : now.toISOString();
      // Default endsAt to startsAt + 90 days if empty (open-ended recurring task)
      const endsAtRaw = taskForm.endsAt
        ? new Date(taskForm.endsAt).toISOString()
        : new Date(new Date(startsAt).getTime() + DEFAULT_TASK_HORIZON_DAYS * MS_PER_DAY).toISOString();

      // Guard: endsAt must be strictly after startsAt
      if (new Date(endsAtRaw) <= new Date(startsAt)) {
        setTaskError("End date must be after start date");
        setTaskSaving(false);
        return;
      }

      // Guard: daily time window — both or neither
      if (!!taskForm.dailyStartTime !== !!taskForm.dailyEndTime) {
        setTaskError("Set both daily start and end time, or leave both empty");
        setTaskSaving(false);
        return;
      }

      const payload = {
        name: taskForm.name,
        startsAt,
        endsAt: endsAtRaw,
        shiftDurationMinutes: Math.max(1, taskForm.shiftDurationMinutes),
        splitCount: taskForm.splitCount,
        requiredHeadcount: taskForm.requiredHeadcount,
        burdenLevel: taskForm.burdenLevel,
        allowsDoubleShift: taskForm.allowsDoubleShift,
        allowsOverlap: taskForm.allowsOverlap,
        dailyStartTime: taskForm.dailyStartTime || null,
        dailyEndTime: taskForm.dailyEndTime || null,
        qualificationRequirements: taskForm.qualificationRequirements,
      };
      if (editingTask) {
        await updateGroupTask(currentSpaceId, groupId, editingTask.id, payload);
        const updated = await listGroupTasks(currentSpaceId, groupId);
        setGroupTasks(updated);
      } else {
        await createGroupTask(currentSpaceId, groupId, payload);
        const updated = await listGroupTasks(currentSpaceId, groupId);
        setGroupTasks(updated);
      }
      setShowTaskForm(false);
      setEditingTask(null);
      setTaskForm(DEFAULT_TASK_FORM);
    } catch {
      setTaskError(tErrors("generic"));
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
        newConstraintFrom || null,
        newConstraintUntil || null
      );
      // Reload to get full DTO — keep all scope types (group, person, role)
      const updated = await getConstraints(currentSpaceId);
      setConstraints(updated);
      setShowConstraintForm(false);
      setNewConstraintFrom("");
      setNewConstraintUntil("");
    } catch {
      setConstraintError(tErrors("errorCreateConstraint"));
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
      setConstraintDeleteErrors(prev => ({ ...prev, [id]: tErrors("errorDeleteConstraint") }));
    }
  }

  async function handleUpdateConstraint(id: string) {
    if (!currentSpaceId) return;
    setEditConstraintSaving(true);
    setEditConstraintError(null);
    try {
      await updateConstraint(currentSpaceId, id, {
        severity: editConstraintSeverity,
        rulePayloadJson: editConstraintPayload,
        effectiveFrom: editConstraintFrom || null,
        effectiveUntil: editConstraintUntil || null,
      });
      setConstraints(prev => prev.map(c => c.id === id ? { ...c, severity: editConstraintSeverity, rulePayloadJson: editConstraintPayload, effectiveFrom: editConstraintFrom || null, effectiveUntil: editConstraintUntil || null } : c));
      setEditingConstraintId(null);
    } catch {
      setEditConstraintError(tErrors("generic"));
    } finally {
      setEditConstraintSaving(false);
    }
  }

  // ── Group role handlers ──────────────────────────────────────────────────
  async function handleCreateRole(name: string, description: string | null, permissionLevel = "view", color?: string | null) {
    if (!currentSpaceId) return;
    await createGroupRole(currentSpaceId, groupId, { name, description, permissionLevel, color });
    const updated = await getGroupRoles(currentSpaceId, groupId);
    setGroupRoles(updated);
  }

  async function handleUpdateRole(roleId: string, name: string, description: string | null, permissionLevel = "view", color?: string | null) {
    if (!currentSpaceId) return;
    await updateGroupRole(currentSpaceId, groupId, roleId, { name, description, permissionLevel, color });
    const updated = await getGroupRoles(currentSpaceId, groupId);
    setGroupRoles(updated);
  }

  async function handleDeactivateRole(roleId: string) {
    if (!currentSpaceId) return;
    await deactivateGroupRole(currentSpaceId, groupId, roleId);
    setGroupRoles(prev => prev.map(r => r.id === roleId ? { ...r, isActive: false } : r));
  }

  async function handleUpdateMemberRole(personId: string, roleId: string | null) {
    if (!currentSpaceId) return;
    await updateMemberRole(currentSpaceId, groupId, personId, roleId);
    // Update local state so the badge refreshes immediately
    const roleName = roleId ? (groupRoles.find(r => r.id === roleId)?.name ?? null) : null;
    setMembers(prev => prev.map(m =>
      m.personId === personId ? { ...m, roleId: roleId ?? null, roleName } : m
    ));
  }

  // ── Qualification handlers ───────────────────────────────────────────────
  async function handleCreateQualification(name: string, description: string | null) {
    if (!currentSpaceId) return;
    await createGroupQualification(currentSpaceId, groupId, name, description);
    const updated = await getGroupQualifications(currentSpaceId, groupId);
    setGroupQualifications(updated);
  }

  async function handleDeactivateQualification(qualId: string) {
    if (!currentSpaceId) return;
    await deactivateGroupQualification(currentSpaceId, groupId, qualId);
    setGroupQualifications(prev => prev.filter(q => q.id !== qualId));
  }

  async function handleAssignQualification(personId: string, qualificationId: string) {
    if (!currentSpaceId) return;
    await assignMemberQualification(currentSpaceId, groupId, personId, qualificationId);
    const qual = groupQualifications.find(q => q.id === qualificationId);
    setMemberQualifications(prev => [
      ...prev.filter(mq => !(mq.personId === personId && mq.qualificationId === qualificationId)),
      { id: crypto.randomUUID(), personId, qualificationId, qualificationName: qual?.name ?? "" },
    ]);
  }

  async function handleRemoveQualification(personId: string, qualificationId: string) {
    if (!currentSpaceId) return;
    await removeMemberQualification(currentSpaceId, groupId, personId, qualificationId);
    setMemberQualifications(prev => prev.filter(mq => !(mq.personId === personId && mq.qualificationId === qualificationId)));
  }
  // ── Settings handlers ────────────────────────────────────────────────────
  async function handleRenameGroup() {    if (!currentSpaceId || !newGroupName.trim()) return;
    setRenameSaving(true);
    setRenameError(null);
    try {
      await renameGroup(currentSpaceId, groupId, newGroupName);
      setGroup(prev => prev ? { ...prev, name: newGroupName } : prev);
    } catch {
      setRenameError(tErrors("generic"));
    } finally {
      setRenameSaving(false);
    }
  }

  async function handleSaveSettings() {
    if (!currentSpaceId) return;
    // Client-side validation for management timeout
    if (!Number.isInteger(managementTimeoutMinutes) || managementTimeoutMinutes < 5 || managementTimeoutMinutes > 120) {
      setSettingsError(tErrors("timeoutOutOfRange"));
      return;
    }
    setSavingSettings(true);
    setSettingsError(null);
    setSettingsSaved(false);
    try {
      // Convert the datetime-local string (YYYY-MM-DDTHH:mm) to ISO UTC string for the API
      const startDateTimeIso = solverStartDateTime
        ? new Date(solverStartDateTime).toISOString()
        : null;
      await updateGroupSettings(currentSpaceId, groupId, solverHorizon, startDateTimeIso, autoPublish, minRestBetweenShiftsHours, undefined, undefined, managementTimeoutMinutes);
      setSolverHorizonDays(solverHorizon);
      setSettingsSaved(true);
      setTimeout(() => setSettingsSaved(false), 3000);
    } catch {
      setSettingsError(tErrors("generic"));
    } finally {
      setSavingSettings(false);
    }
  }

  async function handleClosedBaseChange(value: boolean) {
    if (!currentSpaceId) return;
    try {
      await updateGroup(currentSpaceId, groupId, { isClosedBase: value });
      setIsClosedBase(value);
    } catch {
      // Revert on failure — the toggle will snap back
    }
  }

  async function handleAllowMembersViewHistoryChange(value: boolean) {
    if (!currentSpaceId) return;
    try {
      await updateGroupSettings(currentSpaceId, groupId, solverHorizon, solverStartDateTime ? new Date(solverStartDateTime).toISOString() : null, autoPublish, minRestBetweenShiftsHours, value);
      setGroup(prev => prev ? { ...prev, allowMembersViewHistory: value } : prev);
    } catch {
      // Revert on failure — the toggle will snap back
    }
  }

  async function handleAllowMembersViewStatsChange(value: boolean) {
    if (!currentSpaceId) return;
    try {
      await updateGroupSettings(currentSpaceId, groupId, solverHorizon, solverStartDateTime ? new Date(solverStartDateTime).toISOString() : null, autoPublish, minRestBetweenShiftsHours, undefined, value);
      setGroup(prev => prev ? { ...prev, allowMembersViewStats: value } : prev);
    } catch {
      // Revert on failure — the toggle will snap back
    }
  }

  async function handleTriggerSolver(startTime?: string) {
    if (!currentSpaceId) return;
    setSolverPolling(true);
    setSolverStatus(null);
    setSolverError(null);

    try {
      const res = await apiClient.post<{ runId: string }>(
        `/spaces/${currentSpaceId}/schedule-runs/trigger`,
        { triggerMode: "standard", groupId, startTime: startTime ?? null }
      );
      const runId = res.data.runId;

      pollingRef.current = setInterval(async () => {
        try {
          const statusRes = await apiClient.get<{ status: string; errorSummary?: string | null; progressPhase?: string | null; startedAt?: string | null }>(
            `/spaces/${currentSpaceId}/schedule-runs/${runId}`
          );
          const status = statusRes.data.status;
          // When running, include the progress phase for live display
          const phase = statusRes.data.progressPhase;
          if (status === "Running" && phase) {
            setSolverStatus(`Running:${phase}`);
          } else {
            setSolverStatus(status);
          }

          // Show pre-flight / solver error immediately when failed
          if (status === "Failed" && statusRes.data.errorSummary) {
            const raw = statusRes.data.errorSummary as string;
            // Parse structured error codes and translate
            if (raw.startsWith("UNCOVERED_SLOTS:")) {
              const parts = raw.split(":");
              const tasks = parts[1] || "";
              const count = parts[2] || "0";
              setSolverError(tAdmin("solver_uncovered_slots", { tasks, count }));
            } else if (raw.startsWith("MEMBER_LIMIT_REACHED:")) {
              const limit = raw.split(":")[1] || "";
              setSolverError(tAdmin("member_limit_reached", { limit }));
            } else {
              setSolverError(raw);
            }
          }

          const terminal = status === "Completed" || status === "Failed" || status === "TimedOut";
          if (terminal) {
            if (pollingRef.current) clearInterval(pollingRef.current);
            setSolverPolling(false);
            refetchNotifications();

            // Always reload both draft and discarded versions so the schedule tab
            // shows either the new draft or the infeasibility banner
            const [schedRes, draftRes, discardedRes] = await Promise.all([
              apiClient.get<{ version: { id: string; status: string }; assignments: ScheduleAssignment[] }>(
                `/spaces/${currentSpaceId}/schedule-versions/current`
              ).catch(() => null),
              apiClient.get<Array<{ id: string; status: string; summaryJson?: string | null; sourceRunId?: string | null }>>(
                `/spaces/${currentSpaceId}/schedule-versions?status=draft`
              ).catch(() => ({ data: [] as Array<{ id: string; status: string; summaryJson?: string | null; sourceRunId?: string | null }> })),
              apiClient.get<Array<{ id: string; status: string; summaryJson?: string | null }>>(
                `/spaces/${currentSpaceId}/schedule-versions?status=discarded`
              ).catch(() => ({ data: [] as Array<{ id: string; status: string; summaryJson?: string | null }> })),
            ]);

            setScheduleData(schedRes?.data?.assignments ?? []);
            const drafts = Array.isArray(draftRes?.data) ? draftRes.data : [];
            setDraftVersion(drafts.length > 0 ? drafts[0] : null);
            // Close the draft modal so it reloads with the new draft when reopened
            setShowDraftModal(false);

            // If no draft, show infeasibility reason from most recent discarded version
            if (drafts.length === 0) {
              const discarded = Array.isArray(discardedRes?.data) ? discardedRes.data : [];
              setLastRunSummary(discarded[0]?.summaryJson ?? null);
            } else {
              setLastRunSummary(null);
            }

            // Switch to schedule tab only on success so the admin can see the result.
            // On failure, stay on the current tab so the error message remains visible.
            if (status === "Completed") {
              setActiveTab("schedule");
            }
          }
        } catch {
          if (pollingRef.current) clearInterval(pollingRef.current);
          setSolverPolling(false);
          setSolverError(tErrors("solverCheckError"));
        }
      }, 3000);
    } catch (err: unknown) {
      setSolverPolling(false);
      const axiosErr = err as { response?: { data?: { error?: string; title?: string } } };
      setSolverError(axiosErr?.response?.data?.error || axiosErr?.response?.data?.title || tErrors("solverStartError"));
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
      setTransferError(tErrors("generic"));
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
      setDeleteError(tErrors("generic"));
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

  const visibleTabs = ALL_TABS.filter(t => {
    if (isAdmin) return true;
    if (ADMIN_ONLY_TABS.includes(t)) return false;
    if (t === "stats" && !group.allowMembersViewStats) return false;
    return true;
  });
  const avatarColor = getAvatarColor(group.name);
  const avatarLetter = getAvatarLetter(group.name);

  return (
    <AppShell>
      <div className="max-w-4xl mx-auto px-2 sm:px-4 py-4 sm:py-6 space-y-4 sm:space-y-6" dir="rtl">
        {/* Header */}
        <div className="flex items-center gap-3 sm:gap-4">
          <Link href="/groups" className="text-slate-400 hover:text-slate-600 transition-colors flex-shrink-0">
            <svg width="20" height="20" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
            </svg>
          </Link>
          <div
            className="w-9 h-9 sm:w-10 sm:h-10 rounded-xl flex items-center justify-center text-white text-base sm:text-lg font-bold flex-shrink-0"
            style={{ background: avatarColor }}
          >
            {avatarLetter}
          </div>
          <div className="flex-1 min-w-0">
            <h1 className="text-lg sm:text-xl font-bold text-slate-900 truncate">{group.name}</h1>
            <p className="text-xs sm:text-sm text-slate-400">{group.memberCount ?? 0} {tGroups("members")}</p>
          </div>
          {/* Admin mode toggle — always visible to group owner */}
          <Link
            href="/changelog"
            className="flex items-center gap-1 px-2 py-1.5 rounded-lg text-xs font-medium text-purple-600 bg-purple-50 border border-purple-200 hover:bg-purple-100 transition-colors flex-shrink-0"
            title={tGroups("whatsNew")}
          >
            <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <span className="hidden sm:inline">{tGroups("whatsNew")}</span>
          </Link>
          <div className="relative group/admin-btn flex-shrink-0">
            <button
              onClick={handleAdminModeToggle}
              disabled={!isAdmin && hasCredentials === false}
              aria-describedby={!isAdmin && hasCredentials === false ? "admin-btn-tooltip" : undefined}
              className={`flex items-center gap-1.5 sm:gap-2 px-2.5 sm:px-4 py-2 rounded-xl text-xs sm:text-sm font-medium border transition-colors ${
                isAdmin
                  ? "bg-amber-50 border-amber-300 text-amber-700 hover:bg-amber-100"
                  : hasCredentials === false
                    ? "bg-slate-100 border-slate-200 text-slate-400 cursor-not-allowed opacity-60"
                    : "bg-white border-slate-200 text-slate-600 hover:bg-slate-50"
              }`}
            >
              <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
              </svg>
              <span className="hidden sm:inline">{isAdmin ? tGroups("exitAdminMode") : tGroups("enterAdminMode")}</span>
            </button>
            {!isAdmin && hasCredentials === false && (
              <div
                id="admin-btn-tooltip"
                role="tooltip"
                className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-3 py-2 text-xs text-white bg-slate-800 rounded-lg shadow-lg whitespace-normal max-w-[220px] text-center opacity-0 pointer-events-none group-hover/admin-btn:opacity-100 transition-opacity z-50"
              >
                {tReAuth("noCredentials")}
                <div className="absolute top-full left-1/2 -translate-x-1/2 border-4 border-transparent border-t-slate-800" />
              </div>
            )}
          </div>
        </div>

        {/* Trial/subscription banner */}
        <TrialBanner groupId={groupId} />

        {/* Tabs */}
        <div className="flex gap-1 bg-slate-100 p-1 rounded-xl overflow-x-auto -mx-2 px-2 sm:mx-0 sm:px-1" style={{ scrollbarWidth: "thin" }}>
          {visibleTabs.map(tab => (
            <button
              key={tab}
              onClick={() => setActiveTab(tab)}
              className={`px-2.5 sm:px-3 py-1.5 rounded-lg text-xs sm:text-sm font-medium whitespace-nowrap transition-all ${
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
        <Suspense fallback={<div className="flex justify-center py-12"><svg className="animate-spin h-6 w-6 text-blue-400" fill="none" viewBox="0 0 24 24"><circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" /><path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" /></svg></div>}>
        <div>
          {activeTab === "schedule" && (
            <ScheduleTab
              groupId={groupId}
              solverHorizonDays={solverHorizonDays}
              scheduleData={scheduleData}
              scheduleLoading={scheduleLoading}
              scheduleError={scheduleError}
              scheduleIsOffline={scheduleIsOffline}
              draftVersion={draftVersion}
              lastRunSummary={lastRunSummary}
              isAdmin={isAdmin}
              publishSaving={publishSaving}
              discardSaving={discardSaving}
              scheduleVersionError={scheduleVersionError}
              currentUserName={displayName ?? undefined}
              groupName={group?.name}
              spaceId={currentSpaceId ?? undefined}
              allowMembersViewHistory={group?.allowMembersViewHistory ?? true}
              onOpenDraftModal={() => setShowDraftModal(true)}
              onPublish={handlePublish}
              onDiscard={handleDiscard}
              onTriggerSolver={handleTriggerSolver}
            />
          )}

          {activeTab === "members" && (
            <MembersTab
              isAdmin={isAdmin}
              isOwner={!!group?.ownerPersonId && members.some(m => m.personId === group.ownerPersonId && m.linkedUserId === userId)}
              members={members}
              membersLoading={membersLoading}
              membersError={membersError}
              membersSearch={membersSearch}
              removeErrors={removeErrors}
              groupRoles={groupRoles}
              onSearchChange={setMembersSearch}
              onSelectMember={m => { setSelectedMember(m); setMemberEditForm(null); }}
              onRemoveMember={handleRemoveMember}
              onOpenAddMember={() => setShowAddMember(true)}
              onOpenImport={() => { setImportMode("members"); setShowImportModal(true); }}
              onOpenInvite={handleInvite}
              onUpdateMemberRole={handleUpdateMemberRole}
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
              spaceId={currentSpaceId}
              groupTasks={groupTasks}
              groupTasksLoading={groupTasksLoading}
              groupQualifications={groupQualifications}
              showTaskForm={showTaskForm}
              editingTask={editingTask}
              taskForm={taskForm}
              taskSaving={taskSaving}
              taskError={taskError}
              onOpenCreate={() => {
                const now = new Date();
                // Default start = current date at current hour, minutes zeroed
                const pad = (n: number) => String(n).padStart(2, "0");
                const defaultStart = `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}T${pad(now.getHours())}:00`;
                setEditingTask(null);
                setTaskForm({ ...DEFAULT_TASK_FORM, startsAt: defaultStart });
                setShowTaskForm(true);
              }}
              onOpenImport={() => { setImportMode("tasks"); setShowImportModal(true); }}
              onCloseForm={() => { setShowTaskForm(false); setEditingTask(null); }}
              onFormChange={setTaskForm}
              onFormSubmit={handleTaskSubmit}
              onEditTask={t => { setEditingTask(t); setTaskForm({ name: t.name, startsAt: t.startsAt?.slice(0, 16) ?? "", endsAt: t.endsAt?.slice(0, 16) ?? "", shiftDurationMinutes: t.shiftDurationMinutes, splitCount: t.splitCount ?? 1, requiredHeadcount: t.requiredHeadcount, burdenLevel: t.burdenLevel, allowsDoubleShift: t.allowsDoubleShift, allowsOverlap: t.allowsOverlap, concurrentTaskIds: [], dailyStartTime: t.dailyStartTime ?? "", dailyEndTime: t.dailyEndTime ?? "", qualificationRequirements: t.qualificationRequirements ?? [] }); setShowTaskForm(true); }}
              onDeleteTask={handleDeleteTask}
            />
          )}

          {activeTab === "constraints" && (
            <ConstraintsTab
              isAdmin={isAdmin}
              constraints={constraints}
              constraintsLoading={constraintsLoading}
              constraintDeleteErrors={constraintDeleteErrors}
              showConstraintForm={showConstraintForm}
              newConstraintRuleType={newConstraintRuleType}
              newConstraintSeverity={newConstraintSeverity}
              newConstraintPayload={newConstraintPayload}
              newConstraintFrom={newConstraintFrom}
              newConstraintUntil={newConstraintUntil}
              constraintSaving={constraintSaving}
              constraintError={constraintError}
              editingConstraintId={editingConstraintId}
              editConstraintPayload={editConstraintPayload}
              editConstraintFrom={editConstraintFrom}
              editConstraintUntil={editConstraintUntil}
              editConstraintSeverity={editConstraintSeverity}
              editConstraintSaving={editConstraintSaving}
              editConstraintError={editConstraintError}
              onOpenCreate={() => setShowConstraintForm(true)}
              onCloseCreate={() => { setShowConstraintForm(false); setNewConstraintFrom(""); setNewConstraintUntil(""); }}
              onRuleTypeChange={rt => { setNewConstraintRuleType(rt); const defaults: Record<string, string> = { min_rest_hours: '{"hours": 8}', max_kitchen_per_week: '{"max": 2, "task_type_name": "kitchen"}', no_consecutive_burden: '{"burden_level": "hard"}', min_base_headcount: '{"min": 3, "window_hours": 24}', no_task_type_restriction: '{"task_type_id": ""}' }; setNewConstraintPayload(defaults[rt] ?? "{}"); }}
              onSeverityChange={setNewConstraintSeverity}
              onPayloadChange={setNewConstraintPayload}
              onFromChange={setNewConstraintFrom}
              onUntilChange={setNewConstraintUntil}
              onCreateSubmit={handleCreateConstraint}
              onDeleteConstraint={handleDeleteConstraint}
              onStartEdit={c => { setEditingConstraintId(c.id); setEditConstraintPayload(c.rulePayloadJson); setEditConstraintFrom(c.effectiveFrom?.slice(0, 10) ?? ""); setEditConstraintUntil(c.effectiveUntil?.slice(0, 10) ?? ""); setEditConstraintSeverity(typeof c.severity === "number" ? (c.severity === 0 ? "hard" : "soft") : String(c.severity).toLowerCase()); }}
              onCloseEdit={() => setEditingConstraintId(null)}
              onEditPayloadChange={setEditConstraintPayload}
              onEditFromChange={setEditConstraintFrom}
              onEditUntilChange={setEditConstraintUntil}
              onEditSeverityChange={setEditConstraintSeverity}
              onUpdateConstraint={handleUpdateConstraint}
              groupId={groupId}
              groupRoles={groupRoles}
              groupRolesLoading={groupRolesLoading}
              members={members}
              taskOptions={constraintTaskOptions}
              onCreateWithScope={async (scopeType, scopeId, form) => {
                if (!currentSpaceId) return;
                try {
                  await createConstraint(
                    currentSpaceId, scopeType, scopeId,
                    form.severity, form.ruleType, form.payload,
                    form.from || null, form.until || null
                  );
                } catch (err: unknown) {
                  // Extract the server error message and re-throw so SectionCreateForm can display it
                  const apiMsg =
                    (err as { response?: { data?: { error?: string; message?: string } } })
                      ?.response?.data?.error ??
                    (err as { response?: { data?: { error?: string; message?: string } } })
                      ?.response?.data?.message ??
                    tErrors("errorCreateConstraint");
                  throw new Error(apiMsg);
                }
                const updated = await getConstraints(currentSpaceId);
                setConstraints(updated);
              }}
            />
          )}

          {activeTab === "settings" && (
            <SettingsTab
              isAdmin={isAdmin}
              spaceId={currentSpaceId ?? ""}
              groupId={groupId}
              templateType={group?.templateType}
              newGroupName={newGroupName}
              renameSaving={renameSaving}
              renameError={renameError}
              solverHorizon={solverHorizon}
              savingSettings={savingSettings}
              settingsError={settingsError}
              settingsSaved={settingsSaved}
              solverStartDateTime={solverStartDateTime}
              autoPublish={autoPublish}
              isClosedBase={isClosedBase}
              minRestBetweenShiftsHours={minRestBetweenShiftsHours}
              managementTimeoutMinutes={managementTimeoutMinutes}
              allowMembersViewHistory={group?.allowMembersViewHistory ?? true}
              solverPolling={solverPolling}
              solverStatus={solverStatus}
              solverError={solverError}
              draftVersion={draftVersion}
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
              onSolverStartDateTimeChange={setSolverStartDateTime}
              onAutoPublishChange={setAutoPublish}
              onClosedBaseChange={handleClosedBaseChange}
              onMinRestBetweenShiftsChange={setMinRestBetweenShiftsHours}
              onManagementTimeoutChange={setManagementTimeoutMinutes}
              onAllowMembersViewHistoryChange={handleAllowMembersViewHistoryChange}
              allowMembersViewStats={group?.allowMembersViewStats ?? false}
              onAllowMembersViewStatsChange={handleAllowMembersViewStatsChange}
              onSaveSettings={handleSaveSettings}
              onTriggerSolver={handleTriggerSolver}
              onOpenDraftModal={() => setShowDraftModal(true)}
              onTransferPersonChange={setTransferPersonId}
              onInitiateTransfer={handleInitiateTransfer}
              onCancelTransfer={handleCancelTransfer}
              onShowDeleteConfirm={setShowDeleteConfirm}
              onDeleteGroup={handleDeleteGroup}
            />
          )}

          {activeTab === "qualifications" && currentSpaceId && (
            <QualificationsTab
              isAdmin={isAdmin}
              members={members}
              qualifications={groupQualifications}
              memberQualifications={memberQualifications}
              loading={qualificationsLoading}
              onCreateQualification={handleCreateQualification}
              onDeactivateQualification={handleDeactivateQualification}
              onAssign={handleAssignQualification}
              onRemove={handleRemoveQualification}
            />
          )}

          {activeTab === "roles" && (
            <RolesTab
              isAdmin={isAdmin}
              groupRoles={groupRoles}
              groupRolesLoading={groupRolesLoading}
              members={members}
              onCreateRole={handleCreateRole}
              onUpdateRole={handleUpdateRole}
              onDeactivateRole={handleDeactivateRole}
              onUpdateMemberRole={handleUpdateMemberRole}
            />
          )}

          {activeTab === "stats" && currentSpaceId && (
            <StatsTab groupId={groupId} spaceId={currentSpaceId} />
          )}

          {activeTab === "live-status" && currentSpaceId && (
            <>
              <LiveStatusPanel spaceId={currentSpaceId} groupId={groupId} />
              {isClosedBase && (
                <HomeLeaveScheduleTable spaceId={currentSpaceId} groupId={groupId} />
              )}
            </>
          )}
        </div>
        </Suspense>
      </div>

      {/* Draft schedule modal */}
      {showDraftModal && draftVersion && currentSpaceId && (
        <DraftScheduleModal
          open={showDraftModal}
          onClose={() => setShowDraftModal(false)}
          spaceId={currentSpaceId}
          groupId={groupId}
          draftVersionId={draftVersion.id}
          sourceRunId={draftVersion.sourceRunId}
          groupMemberIds={new Set(members.map(m => m.personId))}
          isAdmin={isAdmin}
          onPublish={async () => { await handlePublish(); setShowDraftModal(false); }}
          onDiscard={async () => { await handleDiscard(); setShowDraftModal(false); }}
          onRunAgain={() => { setShowDraftModal(false); setActiveTab("settings"); handleTriggerSolver(); }}
        />
      )}

      {/* Import modal */}
      {showImportModal && currentSpaceId && (
        <ImportModal
          open={showImportModal}
          onClose={() => setShowImportModal(false)}
          spaceId={currentSpaceId}
          groupId={groupId}
          onImported={() => {
            setShowImportModal(false);
            // Reload the relevant tab data
            if (importMode === "members") {
              getGroupMembers(currentSpaceId, groupId).then(setMembers).catch(() => {});
            } else {
              listGroupTasks(currentSpaceId, groupId).then(setGroupTasks).catch(() => {});
            }
          }}
        />
      )}

      {/* Add member modal */}
      <Modal title={tGroups("members_tab.addMember")} open={showAddMember} onClose={() => { setShowAddMember(false); setAddMemberName(""); setAddMemberPhone(""); setAddMemberEmail(""); setAddMemberError(null); }}>
        <form onSubmit={handleAddMember} className="space-y-4">
          <div>
            <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1">{tGroups("members_tab.fullName")} *</label>
            <input
              type="text"
              value={addMemberName}
              onChange={e => setAddMemberName(e.target.value)}
              placeholder={tGroups("members_tab.fullName")}
              required
              className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1">{tGroups("members_tab.phone")} <span className="text-slate-400 normal-case font-normal">({tGroups("members_tab.optionalInvite")})</span></label>
            <input
              type="tel"
              value={addMemberPhone}
              onChange={e => setAddMemberPhone(e.target.value)}
              placeholder="+1..."
              dir="ltr"
              className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1">{tGroups("members_tab.email")} <span className="text-slate-400 normal-case font-normal">({tGroups("members_tab.optionalInvite")})</span></label>
            <input
              type="email"
              value={addMemberEmail}
              onChange={e => setAddMemberEmail(e.target.value)}
              placeholder="example@email.com"
              dir="ltr"
              className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          {addMemberError && <p className="text-sm text-red-600">{addMemberError}</p>}
          <button type="submit" disabled={addMemberSaving}
            className="w-full bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
            {addMemberSaving ? tGroups("members_tab.adding") : tGroups("members_tab.addMember")}
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
            email: selectedMember.email ?? "",
            profileImageUrl: selectedMember.profileImageUrl ?? "",
            birthday: selectedMember.birthday ?? "",
          })}
          onCancelEdit={() => setMemberEditForm(null)}
          onChangeForm={setMemberEditForm}
          onSave={handleSaveMemberEdit}
        />
      )}

      {/* Sandbox split view — renders as full-screen overlay when sandbox is active */}
      <SandboxView />

      {/* Re-authentication dialog for management mode entry */}
      <ReAuthDialog
        open={showReAuthDialog}
        onSuccess={handleReAuthSuccess}
        onCancel={handleReAuthCancel}
        mode="management"
        spaceId={currentSpaceId ?? undefined}
      />

    </AppShell>
  );
}
