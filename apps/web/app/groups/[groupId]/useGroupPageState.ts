/**
 * useGroupPageState — extracts all state declarations from GroupDetailPage
 * into a single hook so the page component stays under ~300 lines.
 *
 * Each state slice is grouped by the tab it belongs to.
 */
"use client";

import { useState, useRef } from "react";
import type { ActiveTab, ScheduleAssignment } from "./types";
import type {
  GroupWithMemberCountDto, GroupMemberDto, GroupAlertDto, DeletedGroupDto,
  GroupRoleDto, GroupQualificationDto, MemberQualificationDto,
} from "@/lib/api/groups";
import type { GroupTaskDto } from "@/lib/api/tasks";
import type { ConstraintDto } from "@/lib/api/constraints";
import type { TaskForm } from "./tabs/TasksTab";

const DEFAULT_TASK_FORM: TaskForm = {
  name: "",
  startsAt: "",
  endsAt: "",
  shiftDurationMinutes: 60,
  requiredHeadcount: 1,
  burdenLevel: "neutral",
  allowsDoubleShift: false,
  allowsOverlap: false,
  concurrentTaskIds: [],
  dailyStartTime: "",
  dailyEndTime: "",
  requiredQualificationNames: [],
};

export { DEFAULT_TASK_FORM };

export function useGroupPageState() {
  // ── Group / header ───────────────────────────────────────────────────────
  const [group, setGroup] = useState<GroupWithMemberCountDto | null>(null);
  const [groupLoading, setGroupLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<ActiveTab>("schedule");
  const [isAdmin, setIsAdmin] = useState(false);

  // ── Schedule ─────────────────────────────────────────────────────────────
  const [scheduleData, setScheduleData] = useState<ScheduleAssignment[] | null>(null);
  const [scheduleLoading, setScheduleLoading] = useState(false);
  const [scheduleError, setScheduleError] = useState<string | null>(null);
  const [scheduleIsOffline, setScheduleIsOffline] = useState(false);
  const [draftVersion, setDraftVersion] = useState<{ id: string; status: string; summaryJson?: string | null } | null>(null);
  const [lastRunSummary, setLastRunSummary] = useState<string | null>(null);
  const [showDraftModal, setShowDraftModal] = useState(false);
  const [publishSaving, setPublishSaving] = useState(false);
  const [discardSaving, setDiscardSaving] = useState(false);
  const [scheduleVersionError, setScheduleVersionError] = useState<string | null>(null);
  const [solverHorizonDays, setSolverHorizonDays] = useState(14);

  // ── Members ──────────────────────────────────────────────────────────────
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

  // ── Add member modal ─────────────────────────────────────────────────────
  const [showAddMember, setShowAddMember] = useState(false);
  const [addMemberName, setAddMemberName] = useState("");
  const [addMemberPhone, setAddMemberPhone] = useState("");
  const [addMemberEmail, setAddMemberEmail] = useState("");
  const [addMemberSaving, setAddMemberSaving] = useState(false);
  const [addMemberError, setAddMemberError] = useState<string | null>(null);

  // ── Alerts ───────────────────────────────────────────────────────────────
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

  // ── Messages ─────────────────────────────────────────────────────────────
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

  // ── Import modal ─────────────────────────────────────────────────────────
  const [showImportModal, setShowImportModal] = useState(false);
  const [importMode, setImportMode] = useState<"members" | "tasks">("members");

  // ── Tasks ────────────────────────────────────────────────────────────────
  const [groupTasks, setGroupTasks] = useState<GroupTaskDto[]>([]);
  const [groupTasksLoading, setGroupTasksLoading] = useState(false);
  const [showTaskForm, setShowTaskForm] = useState(false);
  const [editingTask, setEditingTask] = useState<GroupTaskDto | null>(null);
  const [taskForm, setTaskForm] = useState<TaskForm>(DEFAULT_TASK_FORM);
  const [taskSaving, setTaskSaving] = useState(false);
  const [taskError, setTaskError] = useState<string | null>(null);

  // ── Constraints ──────────────────────────────────────────────────────────
  const [constraints, setConstraints] = useState<ConstraintDto[]>([]);
  const [constraintsLoading, setConstraintsLoading] = useState(false);
  const [constraintDeleteErrors, setConstraintDeleteErrors] = useState<Record<string, string>>({});
  const [showConstraintForm, setShowConstraintForm] = useState(false);
  const [newConstraintRuleType, setNewConstraintRuleType] = useState("min_rest_hours");
  const [newConstraintSeverity, setNewConstraintSeverity] = useState("hard");
  const [newConstraintPayload, setNewConstraintPayload] = useState('{"hours": 8}');
  const [newConstraintFrom, setNewConstraintFrom] = useState("");
  const [newConstraintUntil, setNewConstraintUntil] = useState("");
  const [constraintSaving, setConstraintSaving] = useState(false);
  const [constraintError, setConstraintError] = useState<string | null>(null);
  const [editingConstraintId, setEditingConstraintId] = useState<string | null>(null);
  const [editConstraintPayload, setEditConstraintPayload] = useState("");
  const [editConstraintFrom, setEditConstraintFrom] = useState("");
  const [editConstraintUntil, setEditConstraintUntil] = useState("");
  const [editConstraintSeverity, setEditConstraintSeverity] = useState("hard");
  const [editConstraintSaving, setEditConstraintSaving] = useState(false);
  const [editConstraintError, setEditConstraintError] = useState<string | null>(null);

  // ── Settings ─────────────────────────────────────────────────────────────
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

  // ── Group roles ──────────────────────────────────────────────────────────
  const [groupRoles, setGroupRoles] = useState<GroupRoleDto[]>([]);
  const [groupRolesLoading, setGroupRolesLoading] = useState(false);
  const [constraintTaskOptions, setConstraintTaskOptions] = useState<{ id: string; name: string }[]>([]);

  // ── Qualifications ───────────────────────────────────────────────────────
  const [groupQualifications, setGroupQualifications] = useState<GroupQualificationDto[]>([]);
  const [memberQualifications, setMemberQualifications] = useState<MemberQualificationDto[]>([]);
  const [qualificationsLoading, setQualificationsLoading] = useState(false);

  // ── Polling ref ──────────────────────────────────────────────────────────
  const pollingRef = useRef<ReturnType<typeof setInterval> | null>(null);

  return {
    // Group
    group, setGroup,
    groupLoading, setGroupLoading,
    activeTab, setActiveTab,
    isAdmin, setIsAdmin,

    // Schedule
    scheduleData, setScheduleData,
    scheduleLoading, setScheduleLoading,
    scheduleError, setScheduleError,
    scheduleIsOffline, setScheduleIsOffline,
    draftVersion, setDraftVersion,
    lastRunSummary, setLastRunSummary,
    showDraftModal, setShowDraftModal,
    publishSaving, setPublishSaving,
    discardSaving, setDiscardSaving,
    scheduleVersionError, setScheduleVersionError,
    solverHorizonDays, setSolverHorizonDays,

    // Members
    members, setMembers,
    membersLoading, setMembersLoading,
    membersError, setMembersError,
    membersSearch, setMembersSearch,
    removeErrors, setRemoveErrors,
    selectedMember, setSelectedMember,
    memberEditForm, setMemberEditForm,
    memberEditSaving, setMemberEditSaving,
    memberEditError, setMemberEditError,

    // Add member
    showAddMember, setShowAddMember,
    addMemberName, setAddMemberName,
    addMemberPhone, setAddMemberPhone,
    addMemberEmail, setAddMemberEmail,
    addMemberSaving, setAddMemberSaving,
    addMemberError, setAddMemberError,

    // Alerts
    alerts, setAlerts,
    alertsLoading, setAlertsLoading,
    alertsError, setAlertsError,
    alertDeleteErrors, setAlertDeleteErrors,
    showAlertForm, setShowAlertForm,
    newAlertTitle, setNewAlertTitle,
    newAlertBody, setNewAlertBody,
    newAlertSeverity, setNewAlertSeverity,
    alertSubmitting, setAlertSubmitting,
    alertSubmitError, setAlertSubmitError,
    editingAlertId, setEditingAlertId,
    editAlertTitle, setEditAlertTitle,
    editAlertBody, setEditAlertBody,
    editAlertSeverity, setEditAlertSeverity,
    editAlertSaving, setEditAlertSaving,
    editAlertError, setEditAlertError,

    // Messages
    messages, setMessages,
    messagesLoading, setMessagesLoading,
    messagesError, setMessagesError,
    newMessageContent, setNewMessageContent,
    messageSending, setMessageSending,
    messageError, setMessageError,
    messagePinErrors, setMessagePinErrors,
    editingMessageId, setEditingMessageId,
    editMessageContent, setEditMessageContent,
    editMessageSaving, setEditMessageSaving,
    editMessageError, setEditMessageError,

    // Import
    showImportModal, setShowImportModal,
    importMode, setImportMode,

    // Tasks
    groupTasks, setGroupTasks,
    groupTasksLoading, setGroupTasksLoading,
    showTaskForm, setShowTaskForm,
    editingTask, setEditingTask,
    taskForm, setTaskForm,
    taskSaving, setTaskSaving,
    taskError, setTaskError,

    // Constraints
    constraints, setConstraints,
    constraintsLoading, setConstraintsLoading,
    constraintDeleteErrors, setConstraintDeleteErrors,
    showConstraintForm, setShowConstraintForm,
    newConstraintRuleType, setNewConstraintRuleType,
    newConstraintSeverity, setNewConstraintSeverity,
    newConstraintPayload, setNewConstraintPayload,
    newConstraintFrom, setNewConstraintFrom,
    newConstraintUntil, setNewConstraintUntil,
    constraintSaving, setConstraintSaving,
    constraintError, setConstraintError,
    editingConstraintId, setEditingConstraintId,
    editConstraintPayload, setEditConstraintPayload,
    editConstraintFrom, setEditConstraintFrom,
    editConstraintUntil, setEditConstraintUntil,
    editConstraintSeverity, setEditConstraintSeverity,
    editConstraintSaving, setEditConstraintSaving,
    editConstraintError, setEditConstraintError,

    // Settings
    newGroupName, setNewGroupName,
    renameSaving, setRenameSaving,
    renameError, setRenameError,
    solverHorizon, setSolverHorizon,
    savingSettings, setSavingSettings,
    settingsError, setSettingsError,
    settingsSaved, setSettingsSaved,
    solverPolling, setSolverPolling,
    solverStatus, setSolverStatus,
    solverError, setSolverError,
    deletedGroups, setDeletedGroups,
    deletedGroupsLoading, setDeletedGroupsLoading,
    transferPersonId, setTransferPersonId,
    transferSaving, setTransferSaving,
    transferError, setTransferError,
    hasPendingTransfer, setHasPendingTransfer,
    cancelTransferSaving, setCancelTransferSaving,
    showDeleteConfirm, setShowDeleteConfirm,
    deleteSaving, setDeleteSaving,
    deleteError, setDeleteError,

    // Group roles
    groupRoles, setGroupRoles,
    groupRolesLoading, setGroupRolesLoading,
    constraintTaskOptions, setConstraintTaskOptions,

    // Qualifications
    groupQualifications, setGroupQualifications,
    memberQualifications, setMemberQualifications,
    qualificationsLoading, setQualificationsLoading,

    // Polling
    pollingRef,

    // Default task form
    DEFAULT_TASK_FORM,
  };
}
