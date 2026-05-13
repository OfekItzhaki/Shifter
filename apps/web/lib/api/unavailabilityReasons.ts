import { apiClient } from "./client";

export interface UnavailabilityReasonDto {
  id: string;
  displayName: string;
  sortOrder: number;
}

export async function getReasons(spaceId: string): Promise<UnavailabilityReasonDto[]> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/unavailability-reasons`);
  return data;
}

export async function createReason(
  spaceId: string,
  data: { displayName: string; sortOrder: number }
): Promise<{ id: string }> {
  const { data: result } = await apiClient.post(`/spaces/${spaceId}/unavailability-reasons`, data);
  return result;
}

export async function updateReason(
  spaceId: string,
  reasonId: string,
  data: { displayName: string; sortOrder: number }
): Promise<void> {
  await apiClient.put(`/spaces/${spaceId}/unavailability-reasons/${reasonId}`, data);
}

export async function deleteReason(spaceId: string, reasonId: string): Promise<void> {
  await apiClient.delete(`/spaces/${spaceId}/unavailability-reasons/${reasonId}`);
}

export async function seedReasons(spaceId: string, reasons: string[]): Promise<void> {
  await apiClient.post(`/spaces/${spaceId}/unavailability-reasons/seed`, { reasonDisplayNames: reasons });
}
