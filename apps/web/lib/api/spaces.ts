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
  managementTimeoutMinutes: number;
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

// ── Space Permission Level ────────────────────────────────────────────────────

export enum SpacePermissionLevel {
  Member = "Member",
  Admin = "Admin",
  GroupOwner = "GroupOwner",
  SpaceOwner = "SpaceOwner",
}

export interface SpacePermissionLevelDto {
  userId: string;
  permissionLevel: SpacePermissionLevel;
}

export const SpacePermissions = {
  ScheduleRollback: "schedule.rollback",
} as const;

export interface CurrentUserPermissionDto {
  permissionKey: string;
  hasPermission: boolean;
}

// ── Space Home Leave Config ───────────────────────────────────────────────────

export type SpaceHomeLeaveMode = "automatic" | "manual" | "disabled";

export interface SpaceHomeLeaveConfigDto {
  mode: SpaceHomeLeaveMode;
  balanceValue: number;
  baseDays: number;
  homeDays: number;
  minPeopleAtBase: number;
  minRestHours: number;
  eligibilityThresholdHours: number;
  leaveCapacity: number;
  leaveDurationHours: number;
  emergencyFreezeActive: boolean;
  emergencyUseForScheduling: boolean;
  freezeStartedAt: string | null;
  preFreezeMode: SpaceHomeLeaveMode;
}

export interface UpdateSpaceHomeLeaveConfigPayload {
  mode: SpaceHomeLeaveMode;
  balanceValue: number;
  baseDays: number;
  homeDays: number;
  minPeopleAtBase: number;
  minRestHours: number;
  eligibilityThresholdHours: number;
  leaveCapacity: number;
  leaveDurationHours: number;
  emergencyFreezeActive: boolean;
  emergencyUseForScheduling: boolean;
}

export interface SpaceSelfServiceDefaultsDto {
  id: string | null;
  source: "space" | "organization" | "install";
  minShiftsPerCycle: number;
  maxShiftsPerCycle: number;
  requestWindowOpenOffsetHours: number;
  requestWindowCloseOffsetHours: number;
  cancellationCutoffHours: number;
  maxAbsencesPerCycle: number;
  maxLateCancellationsPerCycle: number;
  lateCancellationWindowHours: number;
  waitlistOfferMinutes: number;
  cycleDurationDays: number;
  allowMemberShiftClaims: boolean;
  allowWaitlist: boolean;
  allowShiftChangeRequests: boolean;
  allowAbsenceReports: boolean;
  allowShiftSwaps: boolean;
}

export type UpdateSpaceSelfServiceDefaultsPayload = Omit<
  SpaceSelfServiceDefaultsDto,
  "id" | "source"
>;

export async function getMySpaces(): Promise<SpaceDto[]> {
  const { data } = await apiClient.get("/spaces");
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

// ── Space Management ──────────────────────────────────────────────────────────

export async function softDeleteSpace(spaceId: string): Promise<void> {
  await apiClient.delete(`/spaces/${spaceId}`);
}

export async function restoreSpace(spaceId: string): Promise<void> {
  await apiClient.post(`/spaces/${spaceId}/restore`);
}

export async function transferOwnership(
  spaceId: string,
  targetUserId: string,
  reason?: string
): Promise<void> {
  await apiClient.post(`/spaces/${spaceId}/transfer-ownership`, {
    targetUserId,
    reason: reason ?? null,
  });
}

export async function updateManagementTimeout(
  spaceId: string,
  minutes: number
): Promise<void> {
  await apiClient.put(`/spaces/${spaceId}/management-timeout`, { minutes });
}

export async function updateHomeLeaveConfig(
  spaceId: string,
  config: UpdateSpaceHomeLeaveConfigPayload
): Promise<void> {
  await apiClient.put(`/spaces/${spaceId}/home-leave-config`, config);
}

export async function getHomeLeaveConfig(
  spaceId: string
): Promise<SpaceHomeLeaveConfigDto | null> {
  try {
    const { data } = await apiClient.get(`/spaces/${spaceId}/home-leave-config`);
    return data;
  } catch (err: unknown) {
    if (
      err &&
      typeof err === "object" &&
      "response" in err &&
      (err as { response?: { status?: number } }).response?.status === 404
    ) {
      return null;
    }
    throw err;
  }
}

export async function getSpaceSelfServiceDefaults(
  spaceId: string
): Promise<SpaceSelfServiceDefaultsDto> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/self-service-defaults`);
  return data;
}

export async function updateSpaceSelfServiceDefaults(
  spaceId: string,
  payload: UpdateSpaceSelfServiceDefaultsPayload
): Promise<SpaceSelfServiceDefaultsDto> {
  const { data } = await apiClient.put(`/spaces/${spaceId}/self-service-defaults`, payload);
  return data;
}

export async function regenerateSpaceInviteCode(
  spaceId: string
): Promise<{ inviteCode: string }> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/regenerate-invite-code`
  );
  return data;
}

export async function assignSpaceRole(
  spaceId: string,
  userId: string,
  level: SpacePermissionLevel
): Promise<void> {
  await apiClient.put(`/spaces/${spaceId}/members/${userId}/role`, { level });
}

export async function getSpacePermissionLevels(
  spaceId: string
): Promise<SpacePermissionLevelDto[]> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/members/roles`);
  return data;
}

export async function getCurrentUserSpacePermission(
  spaceId: string,
  permissionKey: string
): Promise<CurrentUserPermissionDto> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/permissions/${encodeURIComponent(permissionKey)}`
  );
  return data;
}
