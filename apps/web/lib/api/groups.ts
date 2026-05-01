import { apiClient } from "./client";

export interface GroupTypeDto {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
}

export interface GroupDto {
  id: string;
  groupTypeId: string;
  name: string;
  description: string | null;
  isActive: boolean;
}

export async function getSpaceRoles(spaceId: string) {
  const { data } = await apiClient.get(`/spaces/${spaceId}/roles`);
  return data as Array<{ id: string; name: string; description: string | null; isActive: boolean }>;
}

export async function createSpaceRole(
  spaceId: string, name: string, description: string | null
) {
  const { data } = await apiClient.post(`/spaces/${spaceId}/roles`, { name, description });
  return data as { id: string };
}

export interface GroupWithMemberCountDto {
  id: string;
  name: string;
  memberCount: number;
  solverHorizonDays: number;
  ownerPersonId: string | null;
}

export interface GroupMemberDto {
  personId: string;
  fullName: string;
  displayName: string | null;
  isOwner: boolean;
  phoneNumber: string | null;
  invitationStatus: string;
  profileImageUrl: string | null;
  birthday: string | null;
  linkedUserId: string | null;
}

export interface DeletedGroupDto {
  id: string;
  name: string;
  deletedAt: string;
}

export async function getGroups(spaceId: string): Promise<GroupWithMemberCountDto[]> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/groups`);
  return data as GroupWithMemberCountDto[];
}

export async function getGroupMembers(spaceId: string, groupId: string): Promise<GroupMemberDto[]> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/groups/${groupId}/members`);
  return data as GroupMemberDto[];
}

export async function addGroupMemberByEmail(spaceId: string, groupId: string, email: string): Promise<void> {
  await apiClient.post(`/spaces/${spaceId}/groups/${groupId}/members/by-email`, { email });
}

export async function addGroupMemberById(spaceId: string, groupId: string, personId: string): Promise<void> {
  await apiClient.post(`/spaces/${spaceId}/groups/${groupId}/members`, { personId });
}

export async function updatePersonInfo(
  spaceId: string,
  personId: string,
  payload: { fullName?: string; displayName?: string; phoneNumber?: string; profileImageUrl?: string; birthday?: string }
): Promise<void> {
  await apiClient.put(`/spaces/${spaceId}/people/${personId}/info`, payload);
}

export async function addGroupMemberByPhone(spaceId: string, groupId: string, phoneNumber: string): Promise<void> {
  await apiClient.post(`/spaces/${spaceId}/groups/${groupId}/members/by-phone`, { phoneNumber });
}

export async function removeGroupMember(spaceId: string, groupId: string, personId: string): Promise<void> {
  await apiClient.delete(`/spaces/${spaceId}/groups/${groupId}/members/${personId}`);
}

export async function updateGroupSettings(spaceId: string, groupId: string, solverHorizonDays: number): Promise<void> {
  await apiClient.patch(`/spaces/${spaceId}/groups/${groupId}/settings`, { solverHorizonDays });
}

export async function renameGroup(spaceId: string, groupId: string, name: string): Promise<void> {
  await apiClient.patch(`/spaces/${spaceId}/groups/${groupId}/name`, { name });
}

export async function softDeleteGroup(spaceId: string, groupId: string): Promise<void> {
  await apiClient.delete(`/spaces/${spaceId}/groups/${groupId}`);
}

export async function restoreGroup(spaceId: string, groupId: string): Promise<void> {
  await apiClient.post(`/spaces/${spaceId}/groups/${groupId}/restore`);
}

export async function getDeletedGroups(spaceId: string): Promise<DeletedGroupDto[]> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/groups/deleted`);
  return data as DeletedGroupDto[];
}

export async function initiateOwnershipTransfer(spaceId: string, groupId: string, proposedPersonId: string): Promise<void> {
  await apiClient.post(`/spaces/${spaceId}/groups/${groupId}/transfer`, { proposedPersonId });
}

export async function cancelOwnershipTransfer(spaceId: string, groupId: string): Promise<void> {
  await apiClient.delete(`/spaces/${spaceId}/groups/${groupId}/transfer`);
}

export interface GroupAlertDto {
  id: string;
  title: string;
  body: string;
  severity: "info" | "warning" | "critical";
  createdAt: string;
  createdByPersonId: string;
  createdByDisplayName: string;
}

export async function getGroupAlerts(spaceId: string, groupId: string): Promise<GroupAlertDto[]> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/groups/${groupId}/alerts`);
  return data as GroupAlertDto[];
}

export async function createGroupAlert(
  spaceId: string,
  groupId: string,
  payload: { title: string; body: string; severity: string }
): Promise<{ id: string }> {
  const { data } = await apiClient.post(`/spaces/${spaceId}/groups/${groupId}/alerts`, payload);
  return data as { id: string };
}

export async function deleteGroupAlert(
  spaceId: string,
  groupId: string,
  alertId: string
): Promise<void> {
  await apiClient.delete(`/spaces/${spaceId}/groups/${groupId}/alerts/${alertId}`);
}

export async function updateGroupAlert(
  spaceId: string,
  groupId: string,
  alertId: string,
  payload: { title: string; body: string; severity: string }
): Promise<void> {
  await apiClient.put(`/spaces/${spaceId}/groups/${groupId}/alerts/${alertId}`, payload);
}

export interface GroupMessageDto {
  id: string;
  content: string;
  authorUserId: string;
  authorName: string;
  isPinned: boolean;
  createdAt: string;
  updatedAt: string;
}

export async function updateGroupMessage(
  spaceId: string,
  groupId: string,
  messageId: string,
  content: string
): Promise<void> {
  await apiClient.put(`/spaces/${spaceId}/groups/${groupId}/messages/${messageId}`, { content });
}

export async function deleteGroupMessage(
  spaceId: string,
  groupId: string,
  messageId: string
): Promise<void> {
  await apiClient.delete(`/spaces/${spaceId}/groups/${groupId}/messages/${messageId}`);
}

export async function pinGroupMessage(
  spaceId: string,
  groupId: string,
  messageId: string,
  isPinned: boolean
): Promise<void> {
  await apiClient.patch(`/spaces/${spaceId}/groups/${groupId}/messages/${messageId}/pin`, { isPinned });
}

// ── Group Roles ───────────────────────────────────────────────────────────────

export interface GroupRoleDto {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
  permissionLevel: "View" | "ViewAndEdit" | "Owner";
}

export async function getGroupRoles(spaceId: string, groupId: string): Promise<GroupRoleDto[]> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/groups/${groupId}/roles`);
  return data as GroupRoleDto[];
}

export async function createGroupRole(
  spaceId: string,
  groupId: string,
  payload: { name: string; description?: string | null; permissionLevel?: string }
): Promise<{ id: string }> {
  const { data } = await apiClient.post(`/spaces/${spaceId}/groups/${groupId}/roles`, payload);
  return data as { id: string };
}

export async function updateGroupRole(
  spaceId: string,
  groupId: string,
  roleId: string,
  payload: { name: string; description?: string | null; permissionLevel?: string }
): Promise<void> {
  await apiClient.put(`/spaces/${spaceId}/groups/${groupId}/roles/${roleId}`, payload);
}

export async function deactivateGroupRole(
  spaceId: string,
  groupId: string,
  roleId: string
): Promise<void> {
  await apiClient.delete(`/spaces/${spaceId}/groups/${groupId}/roles/${roleId}`);
}

// ── Live Status ───────────────────────────────────────────────────────────────

export interface MemberLiveStatusDto {
  personId: string;
  displayName: string;
  status: "on_mission" | "at_home" | "blocked" | "free_in_base";
  taskName: string | null;
  slotEndsAt: string | null;
  location: string | null;
}

export async function getGroupLiveStatus(
  spaceId: string,
  groupId: string
): Promise<MemberLiveStatusDto[]> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/live-status`
  );
  return data as MemberLiveStatusDto[];
}
