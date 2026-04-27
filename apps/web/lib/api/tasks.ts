import { apiClient } from "./client";

export interface TaskTypeDto {
  id: string;
  name: string;
  description: string | null;
  burdenLevel: string;
  defaultPriority: number;
  allowsOverlap: boolean;
  isActive: boolean;
}

export interface TaskSlotDto {
  id: string;
  taskTypeId: string;
  taskTypeName: string;
  startsAt: string;
  endsAt: string;
  requiredHeadcount: number;
  priority: number;
  status: string;
}

export async function getTaskTypes(spaceId: string): Promise<TaskTypeDto[]> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/task-types`);
  return data;
}

export async function createTaskType(
  spaceId: string,
  name: string,
  description: string | null,
  burdenLevel: string,
  defaultPriority: number,
  allowsOverlap: boolean
): Promise<{ id: string }> {
  const { data } = await apiClient.post(`/spaces/${spaceId}/task-types`, {
    name, description, burdenLevel, defaultPriority, allowsOverlap,
  });
  return data;
}

export async function getTaskSlots(
  spaceId: string, from?: string, to?: string
): Promise<TaskSlotDto[]> {
  const params = new URLSearchParams();
  if (from) params.set("from", from);
  if (to) params.set("to", to);
  const query = params.toString();
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/task-slots${query ? `?${query}` : ''}`
  );
  return data;
}

export async function createTaskSlot(
  spaceId: string,
  taskTypeId: string,
  startsAt: string,
  endsAt: string,
  requiredHeadcount: number,
  priority: number,
  location: string | null
): Promise<{ id: string }> {
  const { data } = await apiClient.post(`/spaces/${spaceId}/task-slots`, {
    taskTypeId, startsAt, endsAt, requiredHeadcount, priority,
    requiredRoleIds: [], requiredQualificationIds: [], location,
  });
  return data;
}

// ── Group Tasks (new flat model) ──────────────────────────────────────────────

export interface GroupTaskDto {
  id: string;
  name: string;
  startsAt: string;
  endsAt: string;
  shiftDurationMinutes: number;
  requiredHeadcount: number;
  burdenLevel: string;
  allowsDoubleShift: boolean;
  allowsOverlap: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface GroupTaskPayload {
  name: string;
  startsAt: string;
  endsAt: string;
  shiftDurationMinutes: number;
  requiredHeadcount: number;
  burdenLevel: string;
  allowsDoubleShift: boolean;
  allowsOverlap: boolean;
}

export async function listGroupTasks(spaceId: string, groupId: string): Promise<GroupTaskDto[]> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/groups/${groupId}/tasks`);
  return data as GroupTaskDto[];
}

export async function createGroupTask(
  spaceId: string, groupId: string, payload: GroupTaskPayload
): Promise<{ id: string }> {
  const { data } = await apiClient.post(`/spaces/${spaceId}/groups/${groupId}/tasks`, payload);
  return data as { id: string };
}

export async function updateGroupTask(
  spaceId: string, groupId: string, taskId: string, payload: GroupTaskPayload
): Promise<void> {
  await apiClient.put(`/spaces/${spaceId}/groups/${groupId}/tasks/${taskId}`, payload);
}

export async function deleteGroupTask(
  spaceId: string, groupId: string, taskId: string
): Promise<void> {
  await apiClient.delete(`/spaces/${spaceId}/groups/${groupId}/tasks/${taskId}`);
}
