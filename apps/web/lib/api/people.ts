import { apiClient } from "./client";

// ── DTOs ──────────────────────────────────────────────────────────────────────

export interface PersonDto {
  id: string;
  spaceId: string;
  fullName: string;
  displayName: string | null;
  profileImageUrl: string | null;
  isActive: boolean;
  createdAt: string;
  invitationStatus?: string;
}

export interface RoleDto {
  id: string;
  roleId: string;
  name: string;
  description?: string | null;
  isActive?: boolean;
}

export interface RestrictionDto {
  id: string;
  restrictionType: string;
  effectiveFrom: string;
  effectiveUntil: string | null;
  operationalNote: string | null;
  sensitiveReason: string | null;
}

export interface PersonDetailDto {
  id: string;
  spaceId: string;
  fullName: string;
  displayName: string | null;
  profileImageUrl: string | null;
  isActive: boolean;
  createdAt: string;
  qualifications: string[];
  roleNames: string[];
  groupNames: string[];
  restrictions: RestrictionDto[];
  roles: RoleDto[];
}

export interface PersonSearchResultDto {
  id: string;
  fullName: string;
  displayName: string | null;
  phoneNumber: string | null;
  linkedUserId: string | null;
  invitationStatus: string;
}

// ── List / Get ────────────────────────────────────────────────────────────────

export async function getPeople(spaceId: string): Promise<PersonDto[]> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/people`);
  return data as PersonDto[];
}

export async function getPersonDetail(
  spaceId: string,
  personId: string
): Promise<PersonDetailDto> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/people/${personId}`);
  return data as PersonDetailDto;
}

// ── Search ────────────────────────────────────────────────────────────────────

export async function searchPeople(
  spaceId: string,
  query: string
): Promise<PersonSearchResultDto[]> {
  if (!query || query.trim().length < 2) return [];
  const { data } = await apiClient.get(`/spaces/${spaceId}/people/search`, {
    params: { q: query.trim() },
  });
  return data as PersonSearchResultDto[];
}

// ── Create / Invite ───────────────────────────────────────────────────────────

export async function createPerson(
  spaceId: string,
  fullName: string,
  displayName?: string | null | undefined
): Promise<{ id: string }> {
  const { data } = await apiClient.post(`/spaces/${spaceId}/people`, {
    fullName,
    displayName: displayName || null,
    linkedUserId: null,
  });
  return data as { id: string };
}

export async function invitePerson(
  spaceId: string,
  personId: string,
  contact: string,
  channel: "email" | "whatsapp"
): Promise<void> {
  await apiClient.post(`/spaces/${spaceId}/people/${personId}/invite`, {
    contact,
    channel,
  });
}

// ── Roles ─────────────────────────────────────────────────────────────────────

export async function getSpaceRoles(spaceId: string): Promise<RoleDto[]> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/roles`);
  // Backend returns { id, name, ... } — normalize to include roleId
  return (data as Array<{ id: string; name: string; description?: string | null; isActive?: boolean }>)
    .map(r => ({ id: r.id, roleId: r.id, name: r.name, description: r.description, isActive: r.isActive }));
}

export async function assignRole(
  spaceId: string,
  personId: string,
  roleId: string
): Promise<void> {
  await apiClient.post(`/spaces/${spaceId}/people/${personId}/roles`, { roleId });
}

export async function removeRole(
  spaceId: string,
  personId: string,
  roleId: string
): Promise<void> {
  await apiClient.delete(`/spaces/${spaceId}/people/${personId}/roles/${roleId}`);
}

// ── Restrictions ──────────────────────────────────────────────────────────────

export async function addRestriction(
  spaceId: string,
  personId: string,
  restrictionType: string,
  effectiveFrom: string,
  effectiveUntil: string | null,
  operationalNote: string | null,
  sensitiveReason: string | null
): Promise<{ id: string }> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/people/${personId}/restrictions`,
    { restrictionType, effectiveFrom, effectiveUntil, operationalNote, sensitiveReason }
  );
  return data as { id: string };
}
