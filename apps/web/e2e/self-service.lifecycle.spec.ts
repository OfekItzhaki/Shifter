import { APIRequestContext, expect, test } from "@playwright/test";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";
const DEMO_PASSWORD = process.env.E2E_DEMO_PASSWORD ?? "Demo1234!";

type HttpMethod = "GET" | "POST" | "DELETE";

interface LoginResponse {
  accessToken: string;
}

interface SpaceDto {
  id: string;
  name: string;
}

interface GroupDto {
  id: string;
  name?: string;
  schedulingMode?: string;
}

interface GroupMemberDto {
  personId: string;
  fullName: string;
  displayName: string | null;
  linkedUserId: string | null;
  email: string | null;
}

interface CycleStatusDto {
  cycleId: string | null;
  requestWindowOpen: boolean;
  slotCount: number;
  approvedCount: number;
  waitlistCount: number;
  pendingAbsenceReportCount: number;
}

interface AvailableSlotDto {
  id?: string;
  shiftSlotId?: string;
  date?: string;
  startTime?: string;
  endTime?: string;
  taskName: string;
  capacity: number;
  currentFillCount: number;
}

interface AvailableSlotsResponse {
  slots: AvailableSlotDto[];
}

interface ShiftRequestDto {
  id: string;
  shiftSlotId: string;
  schedulingCycleId?: string;
  slotDate?: string;
  slotStartTime?: string;
  slotEndTime?: string;
  taskName: string;
  status: "Pending" | "Approved" | "Rejected" | "Cancelled";
  cancellationReason: string | null;
}

interface MyShiftsResponse {
  requests: ShiftRequestDto[];
}

interface AbsenceReportDto {
  id: string;
  shiftRequestId: string;
  taskName: string;
  status: "Pending" | "Approved" | "Rejected";
}

interface MyAbsenceReportsResponse {
  reports: AbsenceReportDto[];
  lateReportsUsed: number;
  maxLateReports: number;
}

interface AdminWaitlistEntryDto {
  id: string;
  shiftSlotId: string;
  personName: string;
  status: "Waiting" | "Offered" | "Accepted" | "Expired" | "Declined" | "Removed";
}

interface ShiftChangeRequestDto {
  id: string;
  shiftRequestId: string;
  originalShiftSlotId: string;
  requestedShiftSlotId: string | null;
  status: "Pending" | "Approved" | "Rejected" | "Cancelled";
  reason: string;
  adminNote: string | null;
}

interface SwapRequestDto {
  id: string;
  initiatorPersonId: string;
  targetPersonId: string;
  initiatorShiftRequestId: string;
  targetShiftRequestId: string;
  status: "Pending" | "Accepted" | "Declined" | "Cancelled" | "Expired";
}

async function login(request: APIRequestContext, identifier: string): Promise<string> {
  const response = await request.post(`${API_URL}/auth/login`, {
    data: { identifier, password: DEMO_PASSWORD },
  });

  expect(response.ok(), `login failed for ${identifier}`).toBeTruthy();
  const body = await response.json() as LoginResponse;
  expect(body.accessToken).toBeTruthy();
  return body.accessToken;
}

async function api<T>(
  request: APIRequestContext,
  token: string,
  method: HttpMethod,
  path: string,
  data?: unknown
): Promise<T> {
  const response = await request.fetch(`${API_URL}${path}`, {
    method,
    headers: { Authorization: `Bearer ${token}` },
    data,
  });

  expect(response.ok(), `${method} ${path} failed with ${response.status()}: ${await response.text()}`).toBeTruthy();

  if (response.status() === 204) {
    return undefined as T;
  }

  return await response.json() as T;
}

async function findDemoSelfServiceGroup(
  request: APIRequestContext,
  adminToken: string
): Promise<{ spaceId: string; groupId: string }> {
  const spaces = await api<SpaceDto[]>(request, adminToken, "GET", "/spaces");
  const space = spaces.find((row) => row.name === "Unit Alpha") ?? spaces[0];
  expect(space, "seed.sql should create a demo space").toBeTruthy();

  const groups = await api<GroupDto[]>(request, adminToken, "GET", `/spaces/${space.id}/groups`);
  const group = groups.find((row) =>
    row.name === "Self-Service Demo" && row.schedulingMode === "SelfService"
  );
  expect(group, "seed.sql should create the Self-Service Demo group").toBeTruthy();

  return { spaceId: space.id, groupId: group!.id };
}

async function getOrCreateApprovedMemberShift(
  request: APIRequestContext,
  memberToken: string,
  spaceId: string,
  groupId: string,
  cycleId: string
): Promise<ShiftRequestDto> {
  const mine = await api<MyShiftsResponse>(
    request,
    memberToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-requests/mine`
  );

  const approved = mine.requests.find((row) => row.status === "Approved");
  if (approved) return approved;

  const available = await api<AvailableSlotsResponse>(
    request,
    memberToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-slots/available?cycleId=${cycleId}`
  );
  const openSlot = available.slots.find((slot) => slot.currentFillCount < slot.capacity);
  expect(openSlot, "self-service lifecycle test needs an open or seeded approved slot").toBeTruthy();

  const shiftSlotId = openSlot!.id ?? openSlot!.shiftSlotId;
  expect(shiftSlotId).toBeTruthy();

  await api<{ shiftRequestId: string }>(
    request,
    memberToken,
    "POST",
    `/spaces/${spaceId}/groups/${groupId}/shift-requests`,
    { shiftSlotId }
  );

  const refreshed = await api<MyShiftsResponse>(
    request,
    memberToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-requests/mine`
  );
  const created = refreshed.requests.find((row) => row.shiftSlotId === shiftSlotId && row.status === "Approved");
  expect(created, "newly requested open slot should become an approved shift").toBeTruthy();
  return created!;
}

async function getGroupMemberByEmail(
  request: APIRequestContext,
  adminToken: string,
  spaceId: string,
  groupId: string,
  emailOrName: string
): Promise<GroupMemberDto> {
  const members = await api<GroupMemberDto[]>(
    request,
    adminToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/members`
  );
  const normalized = emailOrName.toLowerCase();
  const member = members.find((row) =>
    row.email?.toLowerCase() === normalized
    || row.displayName?.toLowerCase().includes(normalized)
    || row.fullName.toLowerCase().includes(normalized)
  );
  expect(member, `seed.sql should link ${emailOrName} as a member of the self-service demo group`).toBeTruthy();
  return member!;
}

async function getOrCreateAdminAssignedShift(
  request: APIRequestContext,
  adminToken: string,
  memberToken: string,
  spaceId: string,
  groupId: string,
  cycleId: string,
  personId: string,
  excludedSlotIds: string[] = []
): Promise<ShiftRequestDto> {
  const mine = await api<MyShiftsResponse>(
    request,
    memberToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-requests/mine`
  );
  const approved = mine.requests.find((row) =>
    row.status === "Approved" && !excludedSlotIds.includes(row.shiftSlotId)
  );
  if (approved) return approved;

  const adminSlots = await api<AvailableSlotsResponse>(
    request,
    adminToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-slots/admin/slots?cycleId=${cycleId}`
  );
  const targetSlot = adminSlots.slots.find((slot) => {
    const slotId = slot.id ?? slot.shiftSlotId;
    return slotId && !excludedSlotIds.includes(slotId);
  });
  expect(targetSlot, "swap lifecycle test needs an assignable target slot").toBeTruthy();

  const shiftSlotId = targetSlot!.id ?? targetSlot!.shiftSlotId;
  expect(shiftSlotId).toBeTruthy();

  const created = await api<{ shiftRequestId: string }>(
    request,
    adminToken,
    "POST",
    `/spaces/${spaceId}/groups/${groupId}/shift-slots/${shiftSlotId}/admin-overrides/assign`,
    { personId }
  );

  const refreshed = await api<MyShiftsResponse>(
    request,
    memberToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-requests/mine`
  );
  const assigned = refreshed.requests.find((row) => row.id === created.shiftRequestId);
  expect(assigned, "admin override should create an approved target-member shift").toBeTruthy();
  expect(assigned!.status).toBe("Approved");
  return assigned!;
}

test.describe("Self-service scheduling lifecycle", () => {
  test("seeded group supports member absence and admin review workflow", async ({ request }) => {
    const adminToken = await login(request, process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local");
    const memberToken = await login(request, process.env.E2E_MEMBER_EMAIL ?? "ofek@demo.local");
    const { spaceId, groupId } = await findDemoSelfServiceGroup(request, adminToken);

    const initialStatus = await api<CycleStatusDto>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/status`
    );
    expect(initialStatus.cycleId).toBeTruthy();
    expect(initialStatus.slotCount).toBeGreaterThanOrEqual(2);
    expect(initialStatus.waitlistCount).toBeGreaterThanOrEqual(1);

    const waitlistBefore = await api<AdminWaitlistEntryDto[]>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/waitlist/admin`
    );
    expect(waitlistBefore.length).toBeGreaterThanOrEqual(1);
    expect(waitlistBefore.some((entry) => entry.status === "Waiting" || entry.status === "Offered")).toBeTruthy();

    const ownedShift = await getOrCreateApprovedMemberShift(
      request,
      memberToken,
      spaceId,
      groupId,
      initialStatus.cycleId!
    );

    let memberReports = await api<MyAbsenceReportsResponse>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-requests/absence-reports/mine`
    );
    let absenceReport = memberReports.reports.find((report) => report.shiftRequestId === ownedShift.id);

    if (!absenceReport) {
      const response = await api<{ absenceReportId: string }>(
        request,
        memberToken,
        "POST",
        `/spaces/${spaceId}/groups/${groupId}/shift-requests/${ownedShift.id}/cannot-attend`,
        { reason: "E2E lifecycle coverage" }
      );

      memberReports = await api<MyAbsenceReportsResponse>(
        request,
        memberToken,
        "GET",
        `/spaces/${spaceId}/groups/${groupId}/shift-requests/absence-reports/mine`
      );
      absenceReport = memberReports.reports.find((report) => report.id === response.absenceReportId);
    }

    expect(absenceReport, "member cannot-attend flow should create or expose an absence report").toBeTruthy();
    expect(memberReports.maxLateReports).toBeGreaterThanOrEqual(memberReports.lateReportsUsed);

    if (absenceReport!.status === "Pending") {
      await api<void>(
        request,
        adminToken,
        "POST",
        `/spaces/${spaceId}/groups/${groupId}/shift-requests/absence-reports/${absenceReport!.id}/approve`,
        { adminNote: "Reviewed by self-service lifecycle E2E" }
      );
    }

    const adminReports = await api<AbsenceReportDto[]>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-requests/absence-reports`
    );
    const reviewed = adminReports.find((report) => report.id === absenceReport!.id);
    expect(reviewed?.status).toBe("Approved");

    const cancelledRows = await api<ShiftRequestDto[]>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-requests/admin?status=Cancelled&limit=50`
    );
    expect(cancelledRows.some((row) =>
      row.id === ownedShift.id
      && (row.cancellationReason?.includes("Cannot attend") || row.cancellationReason?.includes("Late absence report"))
    )).toBeTruthy();

    const refreshedStatus = await api<CycleStatusDto>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/status`
    );
    expect(refreshedStatus.slotCount).toBeGreaterThanOrEqual(2);
    expect(refreshedStatus.waitlistCount).toBeGreaterThanOrEqual(1);
  });

  test("seeded group supports member shift-change request and admin approval workflow", async ({ request }) => {
    const adminToken = await login(request, process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local");
    const memberToken = await login(request, process.env.E2E_MEMBER_EMAIL ?? "ofek@demo.local");
    const { spaceId, groupId } = await findDemoSelfServiceGroup(request, adminToken);

    const status = await api<CycleStatusDto>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/status`
    );
    expect(status.cycleId).toBeTruthy();

    const ownedShift = await getOrCreateApprovedMemberShift(
      request,
      memberToken,
      spaceId,
      groupId,
      status.cycleId!
    );

    const available = await api<AvailableSlotsResponse>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-slots/available?cycleId=${status.cycleId}`
    );
    const targetSlot = available.slots.find((slot) => {
      const slotId = slot.id ?? slot.shiftSlotId;
      return slotId
        && slotId !== ownedShift.shiftSlotId
        && slot.currentFillCount < slot.capacity;
    });
    expect(targetSlot, "shift-change lifecycle test needs another open slot in the same cycle").toBeTruthy();

    const targetSlotId = targetSlot!.id ?? targetSlot!.shiftSlotId;
    expect(targetSlotId).toBeTruthy();

    let changeRequests = await api<ShiftChangeRequestDto[]>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-change-requests/mine`
    );
    let changeRequest = changeRequests.find((row) =>
      row.shiftRequestId === ownedShift.id
      && row.requestedShiftSlotId === targetSlotId
      && row.status === "Pending"
    );

    if (!changeRequest) {
      const created = await api<{ id: string }>(
        request,
        memberToken,
        "POST",
        `/spaces/${spaceId}/groups/${groupId}/shift-change-requests`,
        {
          shiftRequestId: ownedShift.id,
          requestedShiftSlotId: targetSlotId,
          reason: "E2E shift-change lifecycle coverage",
        }
      );

      changeRequests = await api<ShiftChangeRequestDto[]>(
        request,
        memberToken,
        "GET",
        `/spaces/${spaceId}/groups/${groupId}/shift-change-requests/mine`
      );
      changeRequest = changeRequests.find((row) => row.id === created.id);
    }

    expect(changeRequest, "member shift-change flow should create or expose a pending change request").toBeTruthy();
    expect(changeRequest!.status).toBe("Pending");

    const adminQueue = await api<ShiftChangeRequestDto[]>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-change-requests/admin?status=Pending`
    );
    expect(adminQueue.some((row) => row.id === changeRequest!.id)).toBeTruthy();

    const targetSlots = await api<AvailableSlotDto[]>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-change-requests/admin/target-slots?cycleId=${status.cycleId}&changeRequestId=${changeRequest!.id}`
    );
    expect(targetSlots.some((slot) => (slot.id ?? slot.shiftSlotId) === targetSlotId)).toBeTruthy();

    await api<void>(
      request,
      adminToken,
      "POST",
      `/spaces/${spaceId}/groups/${groupId}/shift-change-requests/admin/${changeRequest!.id}/approve`,
      {
        targetShiftSlotId: targetSlotId,
        adminNote: "Approved by self-service lifecycle E2E",
      }
    );

    const reviewed = await api<ShiftChangeRequestDto[]>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-change-requests/mine`
    );
    expect(reviewed.find((row) => row.id === changeRequest!.id)?.status).toBe("Approved");

    const refreshedShifts = await api<MyShiftsResponse>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-requests/mine`
    );
    const reassignedShift = refreshedShifts.requests.find((row) => row.id === ownedShift.id);
    expect(reassignedShift?.status).toBe("Approved");
    expect(reassignedShift?.shiftSlotId).toBe(targetSlotId);
  });

  test("seeded group supports member-to-member shift swap workflow", async ({ request }) => {
    const adminToken = await login(request, process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local");
    const initiatorToken = await login(request, process.env.E2E_MEMBER_EMAIL ?? "ofek@demo.local");
    const targetToken = await login(request, process.env.E2E_SWAP_TARGET_EMAIL ?? "yael@demo.local");
    const { spaceId, groupId } = await findDemoSelfServiceGroup(request, adminToken);

    const status = await api<CycleStatusDto>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/status`
    );
    expect(status.cycleId).toBeTruthy();

    const targetMember = await getGroupMemberByEmail(
      request,
      adminToken,
      spaceId,
      groupId,
      process.env.E2E_SWAP_TARGET_NAME ?? "yael"
    );

    const initiatorShift = await getOrCreateApprovedMemberShift(
      request,
      initiatorToken,
      spaceId,
      groupId,
      status.cycleId!
    );
    const targetShift = await getOrCreateAdminAssignedShift(
      request,
      adminToken,
      targetToken,
      spaceId,
      groupId,
      status.cycleId!,
      targetMember.personId,
      [initiatorShift.shiftSlotId]
    );
    expect(targetShift.shiftSlotId).not.toBe(initiatorShift.shiftSlotId);

    let swaps = await api<SwapRequestDto[]>(
      request,
      initiatorToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-swaps/my`
    );
    let swap = swaps.find((row) =>
      row.initiatorShiftRequestId === initiatorShift.id
      && row.targetShiftRequestId === targetShift.id
      && row.status === "Pending"
    );

    if (!swap) {
      const created = await api<{ swapRequestId: string }>(
        request,
        initiatorToken,
        "POST",
        `/spaces/${spaceId}/groups/${groupId}/shift-swaps/propose`,
        {
          initiatorShiftRequestId: initiatorShift.id,
          targetShiftRequestId: targetShift.id,
        }
      );

      swaps = await api<SwapRequestDto[]>(
        request,
        initiatorToken,
        "GET",
        `/spaces/${spaceId}/groups/${groupId}/shift-swaps/my`
      );
      swap = swaps.find((row) => row.id === created.swapRequestId);
    }

    expect(swap, "member swap flow should create or expose a pending swap").toBeTruthy();
    expect(swap!.status).toBe("Pending");

    const incomingForTarget = await api<SwapRequestDto[]>(
      request,
      targetToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-swaps/my`
    );
    expect(incomingForTarget.some((row) => row.id === swap!.id && row.status === "Pending")).toBeTruthy();

    await api<{ swapRequestId: string }>(
      request,
      targetToken,
      "POST",
      `/spaces/${spaceId}/groups/${groupId}/shift-swaps/${swap!.id}/accept`
    );

    const initiatorSwaps = await api<SwapRequestDto[]>(
      request,
      initiatorToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-swaps/my`
    );
    expect(initiatorSwaps.find((row) => row.id === swap!.id)?.status).toBe("Accepted");

    const initiatorShifts = await api<MyShiftsResponse>(
      request,
      initiatorToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-requests/mine`
    );
    const targetShifts = await api<MyShiftsResponse>(
      request,
      targetToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-requests/mine`
    );

    expect(initiatorShifts.requests.find((row) => row.id === initiatorShift.id)?.shiftSlotId).toBe(targetShift.shiftSlotId);
    expect(targetShifts.requests.find((row) => row.id === targetShift.id)?.shiftSlotId).toBe(initiatorShift.shiftSlotId);
  });
});
