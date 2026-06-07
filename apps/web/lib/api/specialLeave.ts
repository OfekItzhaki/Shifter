import { apiClient } from "./client";

export type SpecialLeaveStatus = "Pending" | "Approved" | "Rejected" | "Cancelled";

export interface SpecialLeaveRequestDto {
  id: string;
  spaceId: string;
  personId: string;
  personName: string;
  startsAt: string;
  endsAt: string;
  reason: string;
  status: SpecialLeaveStatus;
  requestedByUserId: string;
  processedByUserId: string | null;
  processedAt: string | null;
  adminNote: string | null;
  presenceWindowId: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface SubmitSpecialLeaveRequestPayload {
  startsAt: string;
  endsAt: string;
  reason: string;
}

export async function getMySpecialLeaveRequests(
  spaceId: string,
  from?: string,
  to?: string
): Promise<SpecialLeaveRequestDto[]> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/special-leave-requests/mine`,
    { params: { from, to } }
  );
  return data;
}

export async function submitSpecialLeaveRequest(
  spaceId: string,
  payload: SubmitSpecialLeaveRequestPayload
): Promise<{ id: string }> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/special-leave-requests`,
    payload
  );
  return data;
}

export async function cancelSpecialLeaveRequest(
  spaceId: string,
  requestId: string
): Promise<void> {
  await apiClient.post(`/spaces/${spaceId}/special-leave-requests/${requestId}/cancel`);
}

export async function getAdminSpecialLeaveRequests(
  spaceId: string,
  status?: SpecialLeaveStatus,
  from?: string,
  to?: string
): Promise<SpecialLeaveRequestDto[]> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/special-leave-requests/admin`,
    { params: { status, from, to } }
  );
  return data;
}

export async function approveSpecialLeaveRequest(
  spaceId: string,
  requestId: string,
  adminNote?: string
): Promise<{ presenceWindowId: string; regenerationRunIds: string[] }> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/special-leave-requests/admin/${requestId}/approve`,
    { adminNote: adminNote?.trim() || null }
  );
  return data;
}

export async function rejectSpecialLeaveRequest(
  spaceId: string,
  requestId: string,
  adminNote?: string
): Promise<void> {
  await apiClient.post(
    `/spaces/${spaceId}/special-leave-requests/admin/${requestId}/reject`,
    { adminNote: adminNote?.trim() || null }
  );
}
