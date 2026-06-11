import { apiClient } from "./client";

// ── Shift Template ───────────────────────────────────────────────────────────

export interface ShiftTemplateDto {
  id: string;
  groupId: string;
  groupTaskId: string;
  groupTaskName: string;
  dayOfWeek: number; // 0=Sunday, 6=Saturday
  startTime: string; // "HH:mm:ss"
  endTime: string;   // "HH:mm:ss"
  requiredHeadcount: number;
  isDeleted: boolean;
  createdAt: string;
}

export interface CreateShiftTemplatePayload {
  groupTaskId: string;
  dayOfWeek: number;
  startTime: string; // "HH:mm"
  endTime: string;   // "HH:mm"
  requiredHeadcount: number;
}

export interface UpdateShiftTemplatePayload {
  dayOfWeek: number;
  startTime: string;
  endTime: string;
  requiredHeadcount: number;
  groupTaskId?: string;
}

// ── Self-Service Config ──────────────────────────────────────────────────────

export interface SelfServiceConfigDto {
  id: string | null;
  groupId: string;
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

export interface UpdateSelfServiceConfigPayload {
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


// ── Shift Slot ───────────────────────────────────────────────────────────────

export interface AvailableSlotDto {
  id: string;
  shiftSlotId?: string;
  date: string;         // "YYYY-MM-DD"
  startTime: string;    // "HH:mm"
  endTime: string;      // "HH:mm"
  taskName: string;
  capacity: number;
  currentFillCount: number;
  schedulingCycleId: string;
}

export interface AvailableSlotsResponse {
  slots: AvailableSlotDto[];
  requestWindowOpen: boolean;
  requestWindowOpensAt: string | null;
  requestWindowClosesAt: string | null;
  currentCycleId: string | null;
}

export interface SelfServiceCycleStatusDto {
  cycleId: string | null;
  startsAt: string | null;
  endsAt: string | null;
  requestWindowOpensAt: string | null;
  requestWindowClosesAt: string | null;
  requestWindowOpen: boolean;
  isGenerated: boolean;
  slotCount: number;
  totalCapacity: number;
  filledCount: number;
  approvedCount: number;
  pendingCount: number;
  waitlistCount: number;
  pendingAbsenceReportCount: number;
  latePendingAbsenceReportCount: number;
  pendingShiftChangeRequestCount: number;
  pendingSwapRequestCount: number;
  pendingSpecialLeaveRequestCount: number;
  underfilledSlotCount: number;
  underfilledSlots: UnderfilledSlotDto[];
}

export interface SelfServiceCycleCloseoutDto {
  cycleId: string | null;
  startsAt: string | null;
  endsAt: string | null;
  isClosed: boolean;
  slotCount: number;
  totalCapacity: number;
  filledCount: number;
  underfilledSlotCount: number;
  overfilledSlotCount: number;
  approvedAssignments: number;
  cancelledAssignments: number;
  rejectedRequests: number;
  pendingRequests: number;
  adminOverrideAssignments: number;
  cannotAttendCancellations: number;
  lateAbsenceReports: number;
  approvedAbsenceReports: number;
  rejectedAbsenceReports: number;
  pendingAbsenceReports: number;
  approvedChangeRequests: number;
  rejectedChangeRequests: number;
  pendingChangeRequests: number;
  cancelledChangeRequests: number;
  acceptedSwapRequests: number;
  declinedSwapRequests: number;
  pendingSwapRequests: number;
  cancelledSwapRequests: number;
  expiredSwapRequests: number;
  activeWaitlistEntries: number;
  acceptedWaitlistEntries: number;
  declinedWaitlistEntries: number;
  expiredWaitlistEntries: number;
  removedWaitlistEntries: number;
  approvedSpecialLeaveRequests: number;
  rejectedSpecialLeaveRequests: number;
  pendingSpecialLeaveRequests: number;
  cancelledSpecialLeaveRequests: number;
  issueCount: number;
}

export interface UnderfilledSlotDto {
  shiftSlotId: string;
  date: string;
  startTime: string;
  endTime: string;
  taskName: string;
  currentFillCount: number;
  capacity: number;
  openSeats: number;
}

export interface UnderScheduledMemberDto {
  personId: string;
  personName: string;
  approvedCount: number;
  minRequired: number;
}

export interface UnderScheduledCheckResponse {
  success: boolean;
  underScheduledMembers: UnderScheduledMemberDto[];
}

export interface ShiftSlotAssignmentDto {
  shiftSlotId: string;
  personId: string;
  personName: string;
}

// ── Shift Request ────────────────────────────────────────────────────────────

export interface ShiftRequestDto {
  id: string;
  shiftSlotId: string;
  schedulingCycleId: string;
  slotDate: string;
  slotStartTime: string;
  slotEndTime: string;
  taskName: string;
  status: "Pending" | "Approved" | "Rejected" | "Cancelled";
  isAdminOverride: boolean;
  rejectionReason: string | null;
  cancellationReason: string | null;
  cancelledAt: string | null;
  createdAt: string;
  requestWindowOpen?: boolean;
}

export interface AdminShiftRequestDto extends ShiftRequestDto {
  personId: string;
  personName: string;
  groupId: string;
  schedulingCycleId: string;
}

export interface MyShiftsResponse {
  requests: ShiftRequestDto[];
  currentShiftCount: number;
  minShiftsPerCycle: number;
  maxShiftsPerCycle: number;
  cancellationCutoffHours: number;
  maxLateReports: number;
  lateCancellationWindowHours: number;
}

export interface CannotAttendResponse {
  absenceReportId: string;
  wasLate: boolean;
  absenceReportsUsed: number;
  maxAbsenceReports: number;
  lateReportsUsed: number;
  maxLateReports: number;
}

export interface AbsenceReportDto {
  id: string;
  shiftRequestId: string;
  personId: string;
  personName: string;
  shiftSlotId: string;
  date: string;
  startTime: string;
  endTime: string;
  taskName: string;
  reason: string;
  isLate: boolean;
  status: "Pending" | "Approved" | "Rejected";
  reportedAt: string;
  adminNote: string | null;
  reviewedAt: string | null;
}

export interface MyAbsenceReportsResponse {
  reports: AbsenceReportDto[];
  absenceReportsUsed: number;
  maxAbsenceReports: number;
  lateReportsUsed: number;
  maxLateReports: number;
  schedulingCycleId: string | null;
}

export interface ShiftChangeRequestDto {
  id: string;
  shiftRequestId: string;
  personId: string;
  personName: string;
  schedulingCycleId: string;
  originalShiftSlotId: string;
  originalSlotDate: string;
  originalSlotStartTime: string;
  originalSlotEndTime: string;
  originalTaskName: string;
  requestedShiftSlotId: string | null;
  requestedSlotDate: string | null;
  requestedSlotStartTime: string | null;
  requestedSlotEndTime: string | null;
  requestedTaskName: string | null;
  reason: string;
  status: "Pending" | "Approved" | "Rejected" | "Cancelled";
  requestedAt: string;
  adminNote: string | null;
  reviewedAt: string | null;
}

// ── Waitlist ─────────────────────────────────────────────────────────────────

export interface WaitlistEntryDto {
  id: string;
  shiftSlotId: string;
  slotDate: string;
  slotStartTime: string;
  slotEndTime: string;
  taskName: string;
  position: number;
  status: "Waiting" | "Offered" | "Accepted" | "Expired" | "Declined" | "Removed";
  offeredAt: string | null;
  expiresAt: string | null;
}

export interface AdminWaitlistEntryDto extends WaitlistEntryDto {
  personId: string;
  personName: string;
}

// ── Swap Request ─────────────────────────────────────────────────────────────

export interface SwapRequestDto {
  id: string;
  initiatorPersonId: string;
  targetPersonId: string;
  initiatorPersonName: string;
  targetPersonName: string;
  initiatorShiftRequestId: string;
  targetShiftRequestId: string;
  initiatorSlotDate: string;
  initiatorSlotTime: string;
  initiatorTaskName: string;
  targetSlotDate: string;
  targetSlotTime: string;
  targetTaskName: string;
  status: "Pending" | "Accepted" | "Declined" | "Cancelled" | "Expired";
  expiresAt: string | null;
  createdAt: string;
}

// ══════════════════════════════════════════════════════════════════════════════
// API Functions
// ══════════════════════════════════════════════════════════════════════════════

// ── Shift Templates ──────────────────────────────────────────────────────────

export async function listShiftTemplates(
  spaceId: string,
  groupId: string
): Promise<ShiftTemplateDto[]> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/shift-templates`
  );
  return data;
}

export async function createShiftTemplate(
  spaceId: string,
  groupId: string,
  payload: CreateShiftTemplatePayload
): Promise<{ id: string }> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/shift-templates`,
    payload
  );
  return data;
}

export async function updateShiftTemplate(
  spaceId: string,
  groupId: string,
  templateId: string,
  payload: UpdateShiftTemplatePayload
): Promise<ShiftTemplateDto> {
  const { data } = await apiClient.put(
    `/spaces/${spaceId}/groups/${groupId}/shift-templates/${templateId}`,
    payload
  );
  return data;
}

export async function deleteShiftTemplate(
  spaceId: string,
  groupId: string,
  templateId: string
): Promise<void> {
  await apiClient.delete(
    `/spaces/${spaceId}/groups/${groupId}/shift-templates/${templateId}`
  );
}

// ── Self-Service Config ──────────────────────────────────────────────────────

export async function getSelfServiceConfig(
  spaceId: string,
  groupId: string
): Promise<SelfServiceConfigDto> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/self-service-config`
  );
  return data;
}

export async function updateSelfServiceConfig(
  spaceId: string,
  groupId: string,
  payload: UpdateSelfServiceConfigPayload
): Promise<SelfServiceConfigDto> {
  const { data } = await apiClient.put(
    `/spaces/${spaceId}/groups/${groupId}/self-service-config`,
    payload
  );
  return data;
}

// ── Available Slots ──────────────────────────────────────────────────────────

export async function getAvailableSlots(
  spaceId: string,
  groupId: string,
  cycleId: string
): Promise<AvailableSlotsResponse> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/shift-slots/available`,
    { params: { cycleId } }
  );
  return {
    ...data,
    slots: (data.slots ?? []).map((slot: AvailableSlotDto & { shiftSlotId?: string }) => ({
      ...slot,
      id: slot.id ?? slot.shiftSlotId,
      shiftSlotId: slot.shiftSlotId ?? slot.id,
    })),
  };
}

export async function getSelfServiceCycleStatus(
  spaceId: string,
  groupId: string
): Promise<SelfServiceCycleStatusDto> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/status`
  );
  return data;
}

export async function getSelfServiceCycleCloseout(
  spaceId: string,
  groupId: string,
  cycleId?: string | null
): Promise<SelfServiceCycleCloseoutDto> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/closeout`,
    { params: cycleId ? { cycleId } : undefined }
  );
  return data;
}

export async function generateNextSelfServiceCycle(
  spaceId: string,
  groupId: string
): Promise<SelfServiceCycleStatusDto> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/generate-next`
  );
  return data;
}

export async function openSelfServiceCycleWindow(
  spaceId: string,
  groupId: string,
  cycleId: string,
  hours = 24
): Promise<SelfServiceCycleStatusDto> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/${cycleId}/open`,
    { hours }
  );
  return data;
}

export async function closeSelfServiceCycleWindow(
  spaceId: string,
  groupId: string,
  cycleId: string
): Promise<SelfServiceCycleStatusDto> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/${cycleId}/close`
  );
  return data;
}

export async function checkUnderScheduledMembers(
  spaceId: string,
  groupId: string,
  cycleId: string
): Promise<UnderScheduledCheckResponse> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/${cycleId}/check-under-scheduled`
  );
  return data;
}

// ── Shift Requests ───────────────────────────────────────────────────────────

export async function submitShiftRequest(
  spaceId: string,
  groupId: string,
  shiftSlotId: string
): Promise<{ shiftRequestId: string }> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/shift-requests`,
    { shiftSlotId }
  );
  return data;
}

export async function cancelShiftRequest(
  spaceId: string,
  groupId: string,
  shiftRequestId: string,
  reason: string
): Promise<void> {
  await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/shift-requests/${shiftRequestId}/cancel`,
    { reason }
  );
}

export async function reportCannotAttend(
  spaceId: string,
  groupId: string,
  shiftRequestId: string,
  reason: string
): Promise<CannotAttendResponse> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/shift-requests/${shiftRequestId}/cannot-attend`,
    { reason }
  );
  return data;
}

export async function getMyShiftRequests(
  spaceId: string,
  groupId: string,
  schedulingCycleId?: string
): Promise<MyShiftsResponse> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/shift-requests/mine`,
    { params: schedulingCycleId ? { schedulingCycleId } : undefined }
  );
  return data;
}

export async function getAdminShiftRequests(
  spaceId: string,
  groupId: string,
  status?: AdminShiftRequestDto["status"],
  limit?: number
): Promise<AdminShiftRequestDto[]> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/shift-requests/admin`,
    { params: { ...(status ? { status } : {}), ...(limit ? { limit } : {}) } }
  );
  return data;
}

export async function getAbsenceReports(
  spaceId: string,
  groupId: string,
  status?: AbsenceReportDto["status"]
): Promise<AbsenceReportDto[]> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/shift-requests/absence-reports`,
    { params: status ? { status } : undefined }
  );
  return data;
}

export async function getMyAbsenceReports(
  spaceId: string,
  groupId: string,
  cycleId = "current"
): Promise<MyAbsenceReportsResponse> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/shift-requests/absence-reports/mine`,
    { params: { cycleId } }
  );
  return data;
}

export async function approveAbsenceReport(
  spaceId: string,
  groupId: string,
  absenceReportId: string,
  adminNote?: string
): Promise<void> {
  await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/shift-requests/absence-reports/${absenceReportId}/approve`,
    { adminNote: adminNote?.trim() || null }
  );
}

export async function rejectAbsenceReport(
  spaceId: string,
  groupId: string,
  absenceReportId: string,
  adminNote?: string
): Promise<void> {
  await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/shift-requests/absence-reports/${absenceReportId}/reject`,
    { adminNote: adminNote?.trim() || null }
  );
}

export async function submitShiftChangeRequest(
  spaceId: string,
  groupId: string,
  shiftRequestId: string,
  reason: string,
  requestedShiftSlotId?: string | null
): Promise<{ id: string }> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/shift-change-requests`,
    { shiftRequestId, requestedShiftSlotId: requestedShiftSlotId || null, reason }
  );
  return data;
}

export async function getMyShiftChangeRequests(
  spaceId: string,
  groupId: string
): Promise<ShiftChangeRequestDto[]> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/shift-change-requests/mine`
  );
  return data;
}

export async function cancelShiftChangeRequest(
  spaceId: string,
  groupId: string,
  changeRequestId: string
): Promise<void> {
  await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/shift-change-requests/${changeRequestId}/cancel`
  );
}

export async function getShiftChangeRequests(
  spaceId: string,
  groupId: string,
  status?: ShiftChangeRequestDto["status"]
): Promise<ShiftChangeRequestDto[]> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/shift-change-requests/admin`,
    { params: status ? { status } : undefined }
  );
  return data;
}

export async function getShiftChangeTargetSlots(
  spaceId: string,
  groupId: string,
  cycleId: string,
  changeRequestId?: string
): Promise<AvailableSlotDto[]> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/shift-change-requests/admin/target-slots`,
    { params: { cycleId, ...(changeRequestId ? { changeRequestId } : {}) } }
  );
  return (data ?? []).map((slot: AvailableSlotDto & { shiftSlotId?: string }) => ({
    ...slot,
    id: slot.id ?? slot.shiftSlotId,
    shiftSlotId: slot.shiftSlotId ?? slot.id,
  }));
}

export async function approveShiftChangeRequest(
  spaceId: string,
  groupId: string,
  changeRequestId: string,
  adminNote?: string,
  targetShiftSlotId?: string | null
): Promise<void> {
  await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/shift-change-requests/admin/${changeRequestId}/approve`,
    { adminNote: adminNote?.trim() || null, targetShiftSlotId: targetShiftSlotId || null }
  );
}

export async function rejectShiftChangeRequest(
  spaceId: string,
  groupId: string,
  changeRequestId: string,
  adminNote?: string
): Promise<void> {
  await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/shift-change-requests/admin/${changeRequestId}/reject`,
    { adminNote: adminNote?.trim() || null }
  );
}

export async function getMemberApprovedShifts(
  spaceId: string,
  groupId: string,
  personId: string
): Promise<ShiftRequestDto[]> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/shift-swaps/members/${personId}/approved-shifts`
  );
  return data;
}

// ── Waitlist ─────────────────────────────────────────────────────────────────

export async function joinWaitlist(
  spaceId: string,
  groupId: string,
  shiftSlotId: string
): Promise<{ position: number; shiftSlotId: string }> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/waitlist`,
    { shiftSlotId }
  );
  return data;
}

export async function leaveWaitlist(
  spaceId: string,
  groupId: string,
  shiftSlotId: string
): Promise<void> {
  await apiClient.delete(
    `/spaces/${spaceId}/groups/${groupId}/waitlist/${shiftSlotId}`
  );
}

export async function acceptWaitlistOffer(
  spaceId: string,
  groupId: string,
  shiftSlotId: string
): Promise<{ shiftRequestId: string }> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/waitlist/accept`,
    { shiftSlotId }
  );
  return data;
}

export async function getMyWaitlistEntries(
  spaceId: string,
  groupId: string
): Promise<WaitlistEntryDto[]> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/waitlist/mine`
  );
  return data;
}

export async function getAdminWaitlistEntries(
  spaceId: string,
  groupId: string
): Promise<AdminWaitlistEntryDto[]> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/waitlist/admin`
  );
  return data;
}

// ── Shift Swaps ──────────────────────────────────────────────────────────────

export async function proposeSwap(
  spaceId: string,
  groupId: string,
  initiatorShiftRequestId: string,
  targetShiftRequestId: string
): Promise<{ swapRequestId: string }> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/shift-swaps/propose`,
    { initiatorShiftRequestId, targetShiftRequestId }
  );
  return data;
}

export async function acceptSwap(
  spaceId: string,
  groupId: string,
  swapRequestId: string
): Promise<{ swapRequestId: string }> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/shift-swaps/${swapRequestId}/accept`
  );
  return data;
}

export async function declineSwap(
  spaceId: string,
  groupId: string,
  swapRequestId: string
): Promise<void> {
  await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/shift-swaps/${swapRequestId}/decline`
  );
}

export async function cancelSwap(
  spaceId: string,
  groupId: string,
  swapRequestId: string
): Promise<void> {
  await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/shift-swaps/${swapRequestId}/cancel`
  );
}

export async function getMySwaps(
  spaceId: string,
  groupId: string
): Promise<SwapRequestDto[]> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/shift-swaps/my`
  );
  return data;
}

export async function getAdminSwaps(
  spaceId: string,
  groupId: string,
  status?: SwapRequestDto["status"],
  limit?: number
): Promise<SwapRequestDto[]> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/shift-swaps/admin`,
    { params: { ...(status ? { status } : {}), ...(limit ? { limit } : {}) } }
  );
  return data;
}

// ── Admin Overrides ──────────────────────────────────────────────────────────

export async function adminAssignMember(
  spaceId: string,
  groupId: string,
  shiftSlotId: string,
  personId: string
): Promise<{ shiftRequestId: string }> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/shift-slots/${shiftSlotId}/admin-overrides/assign`,
    { personId }
  );
  return data;
}

export async function getAdminShiftSlotAssignments(
  spaceId: string,
  groupId: string,
  cycleId = "current"
): Promise<ShiftSlotAssignmentDto[]> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/shift-slots/admin/assignments`,
    { params: { cycleId } }
  );
  return data;
}

export async function getAdminShiftSlots(
  spaceId: string,
  groupId: string,
  cycleId = "current"
): Promise<AvailableSlotsResponse> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/shift-slots/admin/slots`,
    { params: { cycleId } }
  );
  return {
    ...data,
    slots: (data.slots ?? []).map((slot: AvailableSlotDto) => ({
      ...slot,
      id: slot.id ?? slot.shiftSlotId,
      shiftSlotId: slot.shiftSlotId ?? slot.id,
    })),
  };
}

export async function adminRemoveMember(
  spaceId: string,
  groupId: string,
  shiftSlotId: string,
  personId: string
): Promise<void> {
  await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/shift-slots/${shiftSlotId}/admin-overrides/remove`,
    { personId }
  );
}
