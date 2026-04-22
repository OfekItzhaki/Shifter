import { apiClient } from "./client";

export interface RoleDto {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
}

export interface PersonRoleDto {
  roleId: string;
  name: string;
}

export interface PersonDto {
  id: string;
  spaceId: string;
  fullName: string;
  displayName: string | null;
  profileImageUrl: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface RestrictionDto {
  id: string;
  restrictionType: string;
  effectiveFrom: string;
  effectiveUntil: string | null;
  operationalNote: string | null;
  sensitiveReason: string | null;
}

export interface PersonDetailDto extends PersonDto {
  qualifications: string[];
  roleNames: string[];
  roles: PersonRoleDto[];
  groupNames: string[];
  restrictions: RestrictionDto[];
}

export async function getPeople(spaceId: string): Promise<PersonDto[]> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/people`);
  return data;
}

export async function getPersonDetail(spaceId: string, personId: string): Promise<PersonDetailDto> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/people/${personId}`);
  return data;
}

export async function createPerson(
  spaceId: string, fullName: string, displayName: string | null
): Promise<{ id: string }> {
  const { data } = await apiClient.post(`/spaces/${spaceId}/people`, { fullName, displayName });
  return data;
}

export async function updatePerson(
  spaceId: string, personId: string,
  fullName: string, displayName: string | null, profileImageUrl: string | null
): Promise<void> {
  await apiClient.put(`/spaces/${spaceId}/people/${personId}`, {
    fullName, displayName, profileImageUrl
  });
}

export async function addRestriction(
  spaceId: string, personId: string,
  restrictionType: string, effectiveFrom: string,
  effectiveUntil: string | null, operationalNote: string | null,
  sensitiveReason: string | null
): Promise<void> {
  await apiClient.post(`/spaces/${spaceId}/people/${personId}/restrictions`, {
    restrictionType, effectiveFrom, effectiveUntil, operationalNote, sensitiveReason
  });
}

export async function getSpaceRoles(spaceId: string): Promise<RoleDto[]> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/roles`);
  return data;
}

export async function assignRole(
  spaceId: string, personId: string, roleId: string
): Promise<void> {
  await apiClient.post(`/spaces/${spaceId}/people/${personId}/roles`, { roleId });
}

export async function removeRole(
  spaceId: string, personId: string, roleId: string
): Promise<void> {
  await apiClient.delete(`/spaces/${spaceId}/people/${personId}/roles/${roleId}`);
}
