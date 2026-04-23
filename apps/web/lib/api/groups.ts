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
}

export interface GroupMemberDto {
  personId: string;
  fullName: string;
  displayName: string | null;
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

export async function removeGroupMember(spaceId: string, groupId: string, personId: string): Promise<void> {
  await apiClient.delete(`/spaces/${spaceId}/groups/${groupId}/members/${personId}`);
}

export async function updateGroupSettings(spaceId: string, groupId: string, solverHorizonDays: number): Promise<void> {
  await apiClient.patch(`/spaces/${spaceId}/groups/${groupId}/settings`, { solverHorizonDays });
}
