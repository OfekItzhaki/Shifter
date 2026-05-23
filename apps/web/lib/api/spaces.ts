import { apiClient } from "./client";

export interface SpaceDto {
  id: string;
  name: string;
  description: string | null;
  locale: string;
  isActive: boolean;
}

export interface SpaceDetailDto {
  id: string;
  name: string;
  description: string | null;
  locale: string;
  isActive: boolean;
  inviteCode: string | null;
  memberCount: number;
  groupCount: number;
  isOwner: boolean;
  createdAt: string;
}

export interface JoinSpaceResult {
  spaceId: string;
  spaceName: string;
  alreadyMember: boolean;
}

export interface SpaceMemberDto {
  userId: string;
  displayName: string | null;
  email: string | null;
  joinedAt: string;
}

export interface MigrateResult {
  spaceId: string | null;
  spaceName: string | null;
  alreadyMigrated: boolean;
  groupsMigrated: number;
}

export async function getMySpaces(): Promise<SpaceDto[]> {
  const { data } = await apiClient.get("/spaces", { _skipErrorRedirect: true } as any);
  return data;
}

export async function createSpace(
  name: string, description: string | null, locale: string
): Promise<{ spaceId: string }> {
  const { data } = await apiClient.post("/spaces", { name, description, locale });
  return data;
}

export async function getSpaceDetail(spaceId: string): Promise<SpaceDetailDto> {
  const { data } = await apiClient.get(`/spaces/${spaceId}`);
  return data;
}

export async function updateSpace(
  spaceId: string, body: { name: string; description: string | null; locale: string }
): Promise<void> {
  await apiClient.put(`/spaces/${spaceId}`, body);
}

export async function joinSpaceByCode(inviteCode: string): Promise<JoinSpaceResult> {
  const { data } = await apiClient.post("/spaces/join", { inviteCode });
  return data;
}

export async function regenerateInviteCode(spaceId: string): Promise<{ inviteCode: string }> {
  const { data } = await apiClient.post(`/spaces/${spaceId}/invite-code/regenerate`);
  return data;
}

export async function getSpaceMembers(spaceId: string): Promise<SpaceMemberDto[]> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/members`);
  return data;
}

export async function linkParentGroup(
  spaceId: string, groupId: string, parentGroupId: string
): Promise<void> {
  await apiClient.post(`/spaces/${spaceId}/groups/${groupId}/link-parent`, { parentGroupId });
}

export async function unlinkParentGroup(spaceId: string, groupId: string): Promise<void> {
  await apiClient.delete(`/spaces/${spaceId}/groups/${groupId}/link-parent`);
}

export async function migrateUserSpace(): Promise<MigrateResult> {
  const { data } = await apiClient.post("/spaces/migrate");
  return data;
}
