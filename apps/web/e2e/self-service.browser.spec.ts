import { APIRequestContext, expect, Page, test } from "@playwright/test";
import { loginAsUser } from "./helpers/auth";

const BASE = process.env.E2E_BASE_URL ?? "http://localhost:3000";
const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";
const DEMO_PASSWORD = process.env.E2E_DEMO_PASSWORD ?? "Demo1234!";

type HttpMethod = "GET" | "POST";

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
  email: string | null;
}

interface CycleStatusDto {
  cycleId: string | null;
  specialDayCount?: number;
}

interface AvailableSlotDto {
  id?: string;
  shiftSlotId?: string;
  date?: string;
  capacity: number;
  currentFillCount: number;
  isSpecialDay?: boolean;
  specialDayName?: string | null;
  specialDayKind?: string | null;
  specialDayRequiresCoverage?: boolean | null;
}

interface AvailableSlotsResponse {
  slots: AvailableSlotDto[];
}

interface ShiftRequestDto {
  id: string;
  shiftSlotId: string;
  slotDate?: string;
  slotStartTime?: string;
  slotEndTime?: string;
  taskName?: string;
  status: "Pending" | "Approved" | "Rejected" | "Cancelled";
  cancellationReason: string | null;
}

interface MyShiftsResponse {
  requests: ShiftRequestDto[];
}

interface AbsenceReportDto {
  id: string;
  shiftRequestId: string;
  reason: string;
  status: "Pending" | "Approved" | "Rejected";
}

interface ShiftChangeRequestDto {
  id: string;
  shiftRequestId: string;
  requestedShiftSlotId: string | null;
  reason: string;
  status: "Pending" | "Approved" | "Rejected" | "Cancelled";
}

interface MyAbsenceReportsResponse {
  reports: AbsenceReportDto[];
  absenceReportsUsed: number;
  maxAbsenceReports: number;
}

interface WaitlistEntryDto {
  id: string;
  shiftSlotId: string;
  status: "Waiting" | "Offered" | "Accepted" | "Expired" | "Declined" | "Removed";
}

interface SwapRequestDto {
  id: string;
  initiatorPersonId: string;
  targetPersonId: string;
  initiatorShiftRequestId: string;
  targetShiftRequestId: string;
  status: "Pending" | "Accepted" | "Declined" | "Cancelled" | "Expired";
}

interface SpecialLeaveRequestDto {
  id: string;
  startsAt: string;
  endsAt: string;
  reason: string;
  status: "Pending" | "Approved" | "Rejected" | "Cancelled";
  presenceWindowId: string | null;
}

interface SpaceSpecialDayDto {
  id: string;
  date: string;
  name: string;
  kind: "Holiday" | "Weekend" | "Custom";
  requiresCoverage: boolean;
}

async function login(request: APIRequestContext, identifier: string): Promise<string> {
  const response = await request.post(`${API_URL}/auth/login`, {
    data: { identifier, password: DEMO_PASSWORD },
  });

  expect(response.ok(), `login failed for ${identifier}`).toBeTruthy();
  const body = await response.json() as LoginResponse;
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
  return await response.json() as T;
}

async function apiNoContent(
  request: APIRequestContext,
  token: string,
  method: "DELETE",
  path: string
): Promise<void> {
  const response = await request.fetch(`${API_URL}${path}`, {
    method,
    headers: { Authorization: `Bearer ${token}` },
  });

  expect(response.ok(), `${method} ${path} failed with ${response.status()}: ${await response.text()}`).toBeTruthy();
}

async function apiExpectStatus(
  request: APIRequestContext,
  token: string,
  method: HttpMethod,
  path: string,
  expectedStatus: number,
  data?: unknown
): Promise<string> {
  const response = await request.fetch(`${API_URL}${path}`, {
    method,
    headers: { Authorization: `Bearer ${token}` },
    data,
  });

  const text = await response.text();
  expect(response.status(), `${method} ${path} expected ${expectedStatus} but got ${response.status()}: ${text}`)
    .toBe(expectedStatus);
  return text;
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

async function getGroupMemberByEmail(
  request: APIRequestContext,
  adminToken: string,
  spaceId: string,
  groupId: string,
  email: string
): Promise<GroupMemberDto> {
  const members = await api<GroupMemberDto[]>(
    request,
    adminToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/members`
  );
  const member = members.find((row) => row.email?.toLowerCase() === email.toLowerCase());
  expect(member, `seed.sql should link ${email} as a member of the self-service demo group`).toBeTruthy();
  return member!;
}

async function ensureApprovedShift(
  request: APIRequestContext,
  adminToken: string,
  memberToken: string,
  spaceId: string,
  groupId: string,
  cycleId: string,
  personId: string
): Promise<ShiftRequestDto> {
  const mine = await api<MyShiftsResponse>(
    request,
    memberToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-requests/mine`
  );

  const approved = mine.requests.find((row) => row.status === "Approved");
  if (approved) return approved;

  const adminSlots = await api<AvailableSlotsResponse>(
    request,
    adminToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-slots/admin/slots?cycleId=${cycleId}`
  );
  const targetSlot = adminSlots.slots.find((slot) => slot.id ?? slot.shiftSlotId);
  expect(targetSlot, "browser lifecycle test needs an assignable shift slot").toBeTruthy();

  const shiftSlotId = targetSlot!.id ?? targetSlot!.shiftSlotId;
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
  expect(assigned?.status).toBe("Approved");
  return assigned!;
}

async function getAdminSlot(
  request: APIRequestContext,
  adminToken: string,
  spaceId: string,
  groupId: string,
  cycleId: string,
  shiftSlotId: string
): Promise<AvailableSlotDto> {
  const adminSlots = await api<AvailableSlotsResponse>(
    request,
    adminToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-slots/admin/slots?cycleId=${cycleId}`
  );
  const slot = adminSlots.slots.find((row) => (row.id ?? row.shiftSlotId) === shiftSlotId);
  expect(slot, `admin slot list should include ${shiftSlotId}`).toBeTruthy();
  return slot!;
}

async function ensureAbsenceReportableApprovedShift(
  request: APIRequestContext,
  adminToken: string,
  memberToken: string,
  spaceId: string,
  groupId: string,
  cycleId: string,
  personId: string
): Promise<ShiftRequestDto> {
  const reports = await api<MyAbsenceReportsResponse>(
    request,
    memberToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-requests/absence-reports/mine`
  );
  const reportedShiftIds = new Set(reports.reports.map((report) => report.shiftRequestId));

  const mine = await api<MyShiftsResponse>(
    request,
    memberToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-requests/mine`
  );
  const approved = mine.requests.find((row) =>
    row.status === "Approved" && !reportedShiftIds.has(row.id)
  );
  if (approved) return approved;

  const unavailableSlotIds = new Set(
    mine.requests
      .filter((row) => row.status === "Approved" || row.status === "Pending")
      .map((row) => row.shiftSlotId)
  );
  const adminSlots = await api<AvailableSlotsResponse>(
    request,
    adminToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-slots/admin/slots?cycleId=${cycleId}`
  );
  const targetSlot = adminSlots.slots.find((slot) => {
    const slotId = slot.id ?? slot.shiftSlotId;
    return slotId && !unavailableSlotIds.has(slotId);
  });
  expect(targetSlot, "browser absence rejection test needs an assignable shift without an existing report").toBeTruthy();

  const shiftSlotId = targetSlot!.id ?? targetSlot!.shiftSlotId;
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
  expect(assigned?.status).toBe("Approved");
  return assigned!;
}

async function getPendingSwapShiftIds(
  request: APIRequestContext,
  token: string,
  spaceId: string,
  groupId: string
): Promise<Set<string>> {
  const swaps = await api<SwapRequestDto[]>(
    request,
    token,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-swaps/my`
  );
  const shiftIds = new Set<string>();
  for (const swap of swaps) {
    if (swap.status !== "Pending") continue;
    shiftIds.add(swap.initiatorShiftRequestId);
    shiftIds.add(swap.targetShiftRequestId);
  }
  return shiftIds;
}

async function ensureSwappableApprovedShift(
  request: APIRequestContext,
  adminToken: string,
  memberToken: string,
  spaceId: string,
  groupId: string,
  cycleId: string,
  personId: string,
  excludedSlotIds: string[] = []
): Promise<ShiftRequestDto> {
  const pendingSwapShiftIds = await getPendingSwapShiftIds(request, memberToken, spaceId, groupId);
  const mine = await api<MyShiftsResponse>(
    request,
    memberToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-requests/mine`
  );
  const approved = mine.requests.find((row) =>
    row.status === "Approved"
    && !pendingSwapShiftIds.has(row.id)
    && !excludedSlotIds.includes(row.shiftSlotId)
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
  expect(targetSlot, "browser swap test needs an assignable shift slot").toBeTruthy();

  const shiftSlotId = targetSlot!.id ?? targetSlot!.shiftSlotId;
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
  expect(assigned?.status).toBe("Approved");
  return assigned!;
}

async function ensureChangeableApprovedShift(
  request: APIRequestContext,
  adminToken: string,
  memberToken: string,
  spaceId: string,
  groupId: string,
  cycleId: string,
  personId: string
): Promise<ShiftRequestDto> {
  const changes = await api<ShiftChangeRequestDto[]>(
    request,
    memberToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-change-requests/mine`
  );
  const pendingShiftIds = new Set(
    changes
      .filter((row) => row.status === "Pending")
      .map((row) => row.shiftRequestId)
  );

  const mine = await api<MyShiftsResponse>(
    request,
    memberToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-requests/mine`
  );
  const approved = mine.requests.find((row) =>
    row.status === "Approved" && !pendingShiftIds.has(row.id)
  );
  if (approved) return approved;

  const unavailableSlotIds = new Set(
    mine.requests
      .filter((row) => row.status === "Approved" || row.status === "Pending")
      .map((row) => row.shiftSlotId)
  );
  const adminSlots = await api<AvailableSlotsResponse>(
    request,
    adminToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-slots/admin/slots?cycleId=${cycleId}`
  );
  const targetSlot = adminSlots.slots.find((slot) => {
    const slotId = slot.id ?? slot.shiftSlotId;
    return slotId && !unavailableSlotIds.has(slotId);
  });
  expect(targetSlot, "browser shift-change test needs an assignable shift without a pending change").toBeTruthy();

  const shiftSlotId = targetSlot!.id ?? targetSlot!.shiftSlotId;
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
  expect(assigned?.status).toBe("Approved");
  return assigned!;
}

async function findOpenTargetSlot(
  request: APIRequestContext,
  memberToken: string,
  spaceId: string,
  groupId: string,
  cycleId: string,
  currentShiftSlotId: string
): Promise<AvailableSlotDto> {
  const available = await api<AvailableSlotsResponse>(
    request,
    memberToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-slots/available?cycleId=${cycleId}`
  );
  const targetSlot = available.slots.find((slot) => {
    const slotId = slot.id ?? slot.shiftSlotId;
    return slotId && slotId !== currentShiftSlotId && slot.currentFillCount < slot.capacity;
  });
  expect(targetSlot, "browser shift-change approval test needs another open target slot").toBeTruthy();
  return targetSlot!;
}

async function ensureSpecialDayForAvailableSlot(
  request: APIRequestContext,
  adminToken: string,
  memberToken: string,
  spaceId: string,
  groupId: string,
  cycleId: string,
  requiresCoverage = true
): Promise<{ slot: AvailableSlotDto; specialDayId: string; specialDayName: string }> {
  const slotsBefore = await api<AvailableSlotsResponse>(
    request,
    memberToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-slots/available?cycleId=${cycleId}`
  );
  const targetSlot = slotsBefore.slots.find((slot) =>
    (slot.id ?? slot.shiftSlotId) && slot.date && slot.currentFillCount < slot.capacity
  ) ?? slotsBefore.slots.find((slot) => (slot.id ?? slot.shiftSlotId) && slot.date);

  expect(targetSlot, "holiday browser test needs a dated self-service slot").toBeTruthy();

  const slotDate = targetSlot!.date!;
  const specialDayName = `E2E ${requiresCoverage ? "Coverage" : "No Coverage"} Special Day ${slotDate}`;
  const existing = await api<SpaceSpecialDayDto[]>(
    request,
    adminToken,
    "GET",
    `/spaces/${spaceId}/special-days?from=${slotDate}&to=${slotDate}`
  );

  for (const day of existing.filter((row) => row.name === specialDayName)) {
    await apiNoContent(request, adminToken, "DELETE", `/spaces/${spaceId}/special-days/${day.id}`);
  }

  const created = await api<{ id: string }>(
    request,
    adminToken,
    "POST",
    `/spaces/${spaceId}/special-days`,
    {
      date: slotDate,
      name: specialDayName,
      kind: "Holiday",
      homeLeaveWeightMultiplier: 1.5,
      requiresCoverage,
    }
  );

  const labeledSlots = await api<AvailableSlotsResponse>(
    request,
    memberToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-slots/available?cycleId=${cycleId}`
  );
  const slotId = targetSlot!.id ?? targetSlot!.shiftSlotId;
  const labeledSlot = labeledSlots.slots.find((slot) => (slot.id ?? slot.shiftSlotId) === slotId);
  expect(labeledSlot?.isSpecialDay).toBeTruthy();
  expect(labeledSlot?.specialDayName).toBe(specialDayName);
  expect(labeledSlot?.specialDayRequiresCoverage).toBe(requiresCoverage);

  const status = await api<CycleStatusDto>(
    request,
    adminToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/status`
  );
  expect(status.specialDayCount ?? 0).toBeGreaterThan(0);

  return { slot: labeledSlot!, specialDayId: created.id, specialDayName };
}

async function ensureWaitingWaitlistEntry(
  request: APIRequestContext,
  memberToken: string,
  spaceId: string,
  groupId: string,
  cycleId: string
): Promise<WaitlistEntryDto> {
  const existing = await api<WaitlistEntryDto[]>(
    request,
    memberToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/waitlist/mine`
  );
  const waiting = existing.find((entry) => entry.status === "Waiting");
  if (waiting) return waiting;

  for (const entry of existing.filter((row) => row.status === "Offered")) {
    await apiNoContent(
      request,
      memberToken,
      "DELETE",
      `/spaces/${spaceId}/groups/${groupId}/waitlist/${entry.shiftSlotId}`
    );
  }

  const available = await api<AvailableSlotsResponse>(
    request,
    memberToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/shift-slots/available?cycleId=${cycleId}`
  );
  const fullSlot = available.slots.find((slot) => {
    const slotId = slot.id ?? slot.shiftSlotId;
    return slotId && slot.currentFillCount >= slot.capacity;
  });
  test.skip(!fullSlot, "waitlist leave browser test needs a full shift slot in the seeded cycle");

  const shiftSlotId = fullSlot!.id ?? fullSlot!.shiftSlotId;
  await api<{ position: number; shiftSlotId: string }>(
    request,
    memberToken,
    "POST",
    `/spaces/${spaceId}/groups/${groupId}/waitlist`,
    { shiftSlotId }
  );

  const refreshed = await api<WaitlistEntryDto[]>(
    request,
    memberToken,
    "GET",
    `/spaces/${spaceId}/groups/${groupId}/waitlist/mine`
  );
  const created = refreshed.find((entry) =>
    entry.shiftSlotId === shiftSlotId && entry.status === "Waiting"
  );
  expect(created, "waitlist leave browser test needs a waiting entry").toBeTruthy();
  return created!;
}

async function enterElevatedGroup(page: Page, groupId: string): Promise<void> {
  await page.evaluate((targetGroupId) => {
    const authRaw = localStorage.getItem("jobuler-auth");
    const authState = authRaw ? JSON.parse(authRaw) : { state: {}, version: 0 };
    authState.state = { ...(authState.state ?? {}), adminGroupId: targetGroupId };
    localStorage.setItem("jobuler-auth", JSON.stringify(authState));

    localStorage.setItem("jobuler-admin-session", JSON.stringify({
      state: {
        isElevated: true,
        elevatedMode: "management",
        elevatedGroupId: targetGroupId,
        timeoutDuration: 15,
        remainingMs: 15 * 60 * 1000,
        lastActivityAt: Date.now(),
      },
      version: 0,
    }));
  }, groupId);
}

function toDateTimeLocalInput(value: Date): string {
  const pad = (part: number) => String(part).padStart(2, "0");
  return [
    value.getFullYear(),
    pad(value.getMonth() + 1),
    pad(value.getDate()),
  ].join("-") + `T${pad(value.getHours())}:${pad(value.getMinutes())}`;
}

function rangesOverlap(
  startA: Date,
  endA: Date,
  startB: Date,
  endB: Date
): boolean {
  return startA < endB && startB < endA;
}

async function findSpecialLeaveWindow(
  request: APIRequestContext,
  memberToken: string,
  spaceId: string
): Promise<{ startsAt: Date; endsAt: Date }> {
  const existingRequests = await api<SpecialLeaveRequestDto[]>(
    request,
    memberToken,
    "GET",
    `/spaces/${spaceId}/special-leave-requests/mine`
  );
  const activeRequests = existingRequests.filter((row) =>
    row.status === "Pending" || row.status === "Approved"
  );

  for (let offsetDays = 60; offsetDays < 120; offsetDays += 1) {
    const startsAt = new Date(Date.now() + offsetDays * 24 * 60 * 60 * 1000);
    startsAt.setHours(10, 0, 0, 0);
    const endsAt = new Date(startsAt.getTime() + 4 * 60 * 60 * 1000);
    const overlapsExisting = activeRequests.some((row) =>
      rangesOverlap(startsAt, endsAt, new Date(row.startsAt), new Date(row.endsAt))
    );
    if (!overlapsExisting) return { startsAt, endsAt };
  }

  throw new Error("Could not find a non-overlapping special leave window for the seeded member.");
}

async function submitSpecialLeaveThroughUi(
  page: Page,
  request: APIRequestContext,
  memberToken: string,
  spaceId: string,
  groupId: string,
  reason: string
): Promise<SpecialLeaveRequestDto> {
  const { startsAt, endsAt } = await findSpecialLeaveWindow(request, memberToken, spaceId);

  await page.evaluate((targetGroupId) => {
    localStorage.setItem("shifter-pick-last-group", targetGroupId);
  }, groupId);
  await page.goto(`${BASE}/pick`);
  await page.getByTestId("pick-tab-my-shifts").click();
  await page.getByTestId("self-service-special-leave-start").fill(toDateTimeLocalInput(startsAt));
  await page.getByTestId("self-service-special-leave-end").fill(toDateTimeLocalInput(endsAt));
  await page.getByTestId("self-service-special-leave-reason").fill(reason);
  await Promise.all([
    page.waitForResponse((response) =>
      response.url().includes(`/spaces/${spaceId}/special-leave-requests`)
      && !response.url().includes("/admin/")
      && response.request().method() === "POST"
    ),
    page.getByTestId("self-service-submit-special-leave").click(),
  ]);

  const memberRequests = await api<SpecialLeaveRequestDto[]>(
    request,
    memberToken,
    "GET",
    `/spaces/${spaceId}/special-leave-requests/mine`
  );
  const createdRequest = memberRequests.find((row) => row.reason === reason);
  expect(createdRequest?.status).toBe("Pending");

  const memberCard = page.locator(
    `[data-testid="self-service-special-leave-card"][data-special-leave-request-id="${createdRequest!.id}"]`
  );
  await expect(memberCard).toBeVisible({ timeout: 15000 });
  return createdRequest!;
}

test.describe("Self-service browser lifecycle", () => {
  test("member sees special-day labels on available shifts", async ({ page, request }) => {
    const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local";
    const memberEmail = process.env.E2E_MEMBER_EMAIL ?? "ofek@demo.local";
    const adminToken = await login(request, adminEmail);
    const memberToken = await login(request, memberEmail);
    const { spaceId, groupId } = await findDemoSelfServiceGroup(request, adminToken);
    const status = await api<CycleStatusDto>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/status`
    );
    expect(status.cycleId).toBeTruthy();

    const { specialDayName } = await ensureSpecialDayForAvailableSlot(
      request,
      adminToken,
      memberToken,
      spaceId,
      groupId,
      status.cycleId!
    );

    await loginAsUser(page, memberEmail, DEMO_PASSWORD);
    await page.evaluate((targetGroupId) => {
      localStorage.setItem("shifter-pick-last-group", targetGroupId);
    }, groupId);
    await page.goto(`${BASE}/pick`);
    await page.getByTestId("pick-tab-slots").click();

    await expect(page.getByText(specialDayName)).toBeVisible({ timeout: 15000 });
  });

  test("no-coverage special-day slots are visible but not claimable", async ({ page, request }) => {
    const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local";
    const memberEmail = process.env.E2E_MEMBER_EMAIL ?? "ofek@demo.local";
    const adminToken = await login(request, adminEmail);
    const memberToken = await login(request, memberEmail);
    const { spaceId, groupId } = await findDemoSelfServiceGroup(request, adminToken);
    const status = await api<CycleStatusDto>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/status`
    );
    expect(status.cycleId).toBeTruthy();

    const { slot, specialDayId, specialDayName } = await ensureSpecialDayForAvailableSlot(
      request,
      adminToken,
      memberToken,
      spaceId,
      groupId,
      status.cycleId!,
      false
    );
    const shiftSlotId = slot.id ?? slot.shiftSlotId;
    expect(shiftSlotId).toBeTruthy();

    try {
      const rejection = await apiExpectStatus(
        request,
        memberToken,
        "POST",
        `/spaces/${spaceId}/groups/${groupId}/shift-requests`,
        422,
        { shiftSlotId }
      );
      expect(rejection).toContain("not configured for coverage");

      await loginAsUser(page, memberEmail, DEMO_PASSWORD);
      await page.evaluate((targetGroupId) => {
        localStorage.setItem("shifter-pick-last-group", targetGroupId);
      }, groupId);
      await page.goto(`${BASE}/pick`);
      await page.getByTestId("pick-tab-slots").click();

      const card = page.locator(`[data-shift-slot-id="${shiftSlotId}"]`);
      await expect(card.getByText(specialDayName)).toBeVisible({ timeout: 15000 });
      await expect(card.getByTestId("self-service-special-day-unavailable")).toBeVisible();
      await expect(card.getByTestId("self-service-request-shift")).toHaveCount(0);
      await expect(card.getByTestId("self-service-join-waitlist")).toHaveCount(0);
    } finally {
      await apiNoContent(request, adminToken, "DELETE", `/spaces/${spaceId}/special-days/${specialDayId}`);
    }
  });

  test("member proposes and target accepts a shift swap through the UI", async ({ page, request }) => {
    const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local";
    const initiatorEmail = process.env.E2E_SWAP_INITIATOR_EMAIL ?? "ofek@demo.local";
    const targetEmail = process.env.E2E_SWAP_TARGET_EMAIL ?? "yael@demo.local";
    const adminToken = await login(request, adminEmail);
    const initiatorToken = await login(request, initiatorEmail);
    const targetToken = await login(request, targetEmail);
    const { spaceId, groupId } = await findDemoSelfServiceGroup(request, adminToken);
    const status = await api<CycleStatusDto>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/status`
    );
    expect(status.cycleId).toBeTruthy();

    const initiatorMember = await getGroupMemberByEmail(request, adminToken, spaceId, groupId, initiatorEmail);
    const targetMember = await getGroupMemberByEmail(request, adminToken, spaceId, groupId, targetEmail);
    const initiatorShift = await ensureSwappableApprovedShift(
      request,
      adminToken,
      initiatorToken,
      spaceId,
      groupId,
      status.cycleId!,
      initiatorMember.personId
    );
    const targetShift = await ensureSwappableApprovedShift(
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
    const initiatorSlotBeforeSwap = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      initiatorShift.shiftSlotId
    );
    const targetSlotBeforeSwap = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      targetShift.shiftSlotId
    );

    await loginAsUser(page, initiatorEmail, DEMO_PASSWORD);
    await page.evaluate((targetGroupId) => {
      localStorage.setItem("shifter-pick-last-group", targetGroupId);
    }, groupId);
    await page.goto(`${BASE}/pick`);
    await page.getByTestId("pick-tab-swaps").click();
    await expect(page.getByTestId("self-service-propose-swap")).toBeVisible({ timeout: 15000 });
    await page.getByTestId("self-service-propose-swap").click();
    await page.locator(`[data-testid="self-service-swap-my-shift"][data-shift-request-id="${initiatorShift.id}"]`).click();
    await page.locator(`[data-testid="self-service-swap-target-member"][data-person-id="${targetMember.personId}"]`).click();
    await Promise.all([
      page.waitForResponse((response) =>
        response.url().includes("/shift-swaps/propose") && response.request().method() === "POST"
      ),
      page.locator(`[data-testid="self-service-swap-target-shift"][data-shift-request-id="${targetShift.id}"]`).click(),
    ]);

    const proposedSwaps = await api<SwapRequestDto[]>(
      request,
      initiatorToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-swaps/my`
    );
    const proposedSwap = proposedSwaps.find((row) =>
      row.initiatorShiftRequestId === initiatorShift.id
      && row.targetShiftRequestId === targetShift.id
      && row.status === "Pending"
    );
    expect(proposedSwap, "browser swap proposal should create a pending swap").toBeTruthy();

    await loginAsUser(page, targetEmail, DEMO_PASSWORD);
    await page.evaluate((targetGroupId) => {
      localStorage.setItem("shifter-pick-last-group", targetGroupId);
    }, groupId);
    await page.goto(`${BASE}/pick`);
    await page.getByTestId("pick-tab-swaps").click();
    const incomingSwapCard = page.locator(`[data-testid="self-service-swap-card"][data-swap-request-id="${proposedSwap!.id}"]`);
    await expect(incomingSwapCard.getByTestId("self-service-accept-swap")).toBeVisible({ timeout: 15000 });
    await Promise.all([
      page.waitForResponse((response) =>
        response.url().includes(`/shift-swaps/${proposedSwap!.id}/accept`)
        && response.request().method() === "POST"
      ),
      incomingSwapCard.getByTestId("self-service-accept-swap").click(),
    ]);

    const reviewedSwaps = await api<SwapRequestDto[]>(
      request,
      initiatorToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-swaps/my`
    );
    expect(reviewedSwaps.find((row) => row.id === proposedSwap!.id)?.status).toBe("Accepted");

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

    const initiatorSlotAfterSwap = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      initiatorShift.shiftSlotId
    );
    const targetSlotAfterSwap = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      targetShift.shiftSlotId
    );
    expect(initiatorSlotAfterSwap.currentFillCount).toBe(initiatorSlotBeforeSwap.currentFillCount);
    expect(targetSlotAfterSwap.currentFillCount).toBe(targetSlotBeforeSwap.currentFillCount);
  });

  test("member proposes and target declines a shift swap through the UI", async ({ page, request }) => {
    const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local";
    const initiatorEmail = process.env.E2E_DECLINE_SWAP_INITIATOR_EMAIL ?? "viewer@demo.local";
    const targetEmail = process.env.E2E_DECLINE_SWAP_TARGET_EMAIL ?? "yael@demo.local";
    const adminToken = await login(request, adminEmail);
    const initiatorToken = await login(request, initiatorEmail);
    const targetToken = await login(request, targetEmail);
    const { spaceId, groupId } = await findDemoSelfServiceGroup(request, adminToken);
    const status = await api<CycleStatusDto>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/status`
    );
    expect(status.cycleId).toBeTruthy();

    const initiatorMember = await getGroupMemberByEmail(request, adminToken, spaceId, groupId, initiatorEmail);
    const targetMember = await getGroupMemberByEmail(request, adminToken, spaceId, groupId, targetEmail);
    const initiatorShift = await ensureSwappableApprovedShift(
      request,
      adminToken,
      initiatorToken,
      spaceId,
      groupId,
      status.cycleId!,
      initiatorMember.personId
    );
    const targetShift = await ensureSwappableApprovedShift(
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
    const initiatorSlotBeforeDecline = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      initiatorShift.shiftSlotId
    );
    const targetSlotBeforeDecline = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      targetShift.shiftSlotId
    );

    await loginAsUser(page, initiatorEmail, DEMO_PASSWORD);
    await page.evaluate((targetGroupId) => {
      localStorage.setItem("shifter-pick-last-group", targetGroupId);
    }, groupId);
    await page.goto(`${BASE}/pick`);
    await page.getByTestId("pick-tab-swaps").click();
    await expect(page.getByTestId("self-service-propose-swap")).toBeVisible({ timeout: 15000 });
    await page.getByTestId("self-service-propose-swap").click();
    await page.locator(`[data-testid="self-service-swap-my-shift"][data-shift-request-id="${initiatorShift.id}"]`).click();
    await page.locator(`[data-testid="self-service-swap-target-member"][data-person-id="${targetMember.personId}"]`).click();
    await Promise.all([
      page.waitForResponse((response) =>
        response.url().includes("/shift-swaps/propose") && response.request().method() === "POST"
      ),
      page.locator(`[data-testid="self-service-swap-target-shift"][data-shift-request-id="${targetShift.id}"]`).click(),
    ]);

    const proposedSwaps = await api<SwapRequestDto[]>(
      request,
      initiatorToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-swaps/my`
    );
    const proposedSwap = proposedSwaps.find((row) =>
      row.initiatorShiftRequestId === initiatorShift.id
      && row.targetShiftRequestId === targetShift.id
      && row.status === "Pending"
    );
    expect(proposedSwap, "browser swap proposal should create a pending swap to decline").toBeTruthy();

    await loginAsUser(page, targetEmail, DEMO_PASSWORD);
    await page.evaluate((targetGroupId) => {
      localStorage.setItem("shifter-pick-last-group", targetGroupId);
    }, groupId);
    await page.goto(`${BASE}/pick`);
    await page.getByTestId("pick-tab-swaps").click();
    const incomingSwapCard = page.locator(`[data-testid="self-service-swap-card"][data-swap-request-id="${proposedSwap!.id}"]`);
    await expect(incomingSwapCard.getByTestId("self-service-decline-swap")).toBeVisible({ timeout: 15000 });
    await Promise.all([
      page.waitForResponse((response) =>
        response.url().includes(`/shift-swaps/${proposedSwap!.id}/decline`)
        && response.request().method() === "POST"
      ),
      incomingSwapCard.getByTestId("self-service-decline-swap").click(),
    ]);

    const reviewedSwaps = await api<SwapRequestDto[]>(
      request,
      initiatorToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-swaps/my`
    );
    expect(reviewedSwaps.find((row) => row.id === proposedSwap!.id)?.status).toBe("Declined");

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
    expect(initiatorShifts.requests.find((row) => row.id === initiatorShift.id)?.shiftSlotId).toBe(initiatorShift.shiftSlotId);
    expect(targetShifts.requests.find((row) => row.id === targetShift.id)?.shiftSlotId).toBe(targetShift.shiftSlotId);

    const initiatorSlotAfterDecline = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      initiatorShift.shiftSlotId
    );
    const targetSlotAfterDecline = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      targetShift.shiftSlotId
    );
    expect(initiatorSlotAfterDecline.currentFillCount).toBe(initiatorSlotBeforeDecline.currentFillCount);
    expect(targetSlotAfterDecline.currentFillCount).toBe(targetSlotBeforeDecline.currentFillCount);
  });

  test("member proposes and cancels a pending shift swap through the UI", async ({ page, request }) => {
    const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local";
    const initiatorEmail = process.env.E2E_CANCEL_SWAP_INITIATOR_EMAIL ?? "ofek@demo.local";
    const targetEmail = process.env.E2E_CANCEL_SWAP_TARGET_EMAIL ?? "viewer@demo.local";
    const adminToken = await login(request, adminEmail);
    const initiatorToken = await login(request, initiatorEmail);
    const targetToken = await login(request, targetEmail);
    const { spaceId, groupId } = await findDemoSelfServiceGroup(request, adminToken);
    const status = await api<CycleStatusDto>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/status`
    );
    expect(status.cycleId).toBeTruthy();

    const initiatorMember = await getGroupMemberByEmail(request, adminToken, spaceId, groupId, initiatorEmail);
    const targetMember = await getGroupMemberByEmail(request, adminToken, spaceId, groupId, targetEmail);
    const initiatorShift = await ensureSwappableApprovedShift(
      request,
      adminToken,
      initiatorToken,
      spaceId,
      groupId,
      status.cycleId!,
      initiatorMember.personId
    );
    const targetShift = await ensureSwappableApprovedShift(
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
    const initiatorSlotBeforeCancel = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      initiatorShift.shiftSlotId
    );
    const targetSlotBeforeCancel = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      targetShift.shiftSlotId
    );

    await loginAsUser(page, initiatorEmail, DEMO_PASSWORD);
    await page.evaluate((targetGroupId) => {
      localStorage.setItem("shifter-pick-last-group", targetGroupId);
    }, groupId);
    await page.goto(`${BASE}/pick`);
    await page.getByTestId("pick-tab-swaps").click();
    await expect(page.getByTestId("self-service-propose-swap")).toBeVisible({ timeout: 15000 });
    await page.getByTestId("self-service-propose-swap").click();
    await page.locator(`[data-testid="self-service-swap-my-shift"][data-shift-request-id="${initiatorShift.id}"]`).click();
    await page.locator(`[data-testid="self-service-swap-target-member"][data-person-id="${targetMember.personId}"]`).click();
    await Promise.all([
      page.waitForResponse((response) =>
        response.url().includes("/shift-swaps/propose") && response.request().method() === "POST"
      ),
      page.locator(`[data-testid="self-service-swap-target-shift"][data-shift-request-id="${targetShift.id}"]`).click(),
    ]);

    const proposedSwaps = await api<SwapRequestDto[]>(
      request,
      initiatorToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-swaps/my`
    );
    const proposedSwap = proposedSwaps.find((row) =>
      row.initiatorShiftRequestId === initiatorShift.id
      && row.targetShiftRequestId === targetShift.id
      && row.status === "Pending"
    );
    expect(proposedSwap, "browser swap proposal should create a pending swap to cancel").toBeTruthy();

    const outgoingSwapCard = page.locator(`[data-testid="self-service-swap-card"][data-swap-request-id="${proposedSwap!.id}"]`);
    await expect(outgoingSwapCard.getByTestId("self-service-cancel-swap")).toBeVisible({ timeout: 15000 });
    await Promise.all([
      page.waitForResponse((response) =>
        response.url().includes(`/shift-swaps/${proposedSwap!.id}/cancel`)
        && response.request().method() === "POST"
      ),
      outgoingSwapCard.getByTestId("self-service-cancel-swap").click(),
    ]);

    const reviewedSwaps = await api<SwapRequestDto[]>(
      request,
      initiatorToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-swaps/my`
    );
    expect(reviewedSwaps.find((row) => row.id === proposedSwap!.id)?.status).toBe("Cancelled");

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
    expect(initiatorShifts.requests.find((row) => row.id === initiatorShift.id)?.shiftSlotId).toBe(initiatorShift.shiftSlotId);
    expect(targetShifts.requests.find((row) => row.id === targetShift.id)?.shiftSlotId).toBe(targetShift.shiftSlotId);

    const initiatorSlotAfterCancel = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      initiatorShift.shiftSlotId
    );
    const targetSlotAfterCancel = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      targetShift.shiftSlotId
    );
    expect(initiatorSlotAfterCancel.currentFillCount).toBe(initiatorSlotBeforeCancel.currentFillCount);
    expect(targetSlotAfterCancel.currentFillCount).toBe(targetSlotBeforeCancel.currentFillCount);
  });

  test("member cancels an approved shift through the UI", async ({ page, request }) => {
    const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local";
    const memberEmail = process.env.E2E_CANCEL_MEMBER_EMAIL ?? "yael@demo.local";
    const adminToken = await login(request, adminEmail);
    const memberToken = await login(request, memberEmail);
    const { spaceId, groupId } = await findDemoSelfServiceGroup(request, adminToken);
    const status = await api<CycleStatusDto>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/status`
    );
    expect(status.cycleId).toBeTruthy();

    const member = await getGroupMemberByEmail(request, adminToken, spaceId, groupId, memberEmail);
    const ownedShift = await ensureApprovedShift(
      request,
      adminToken,
      memberToken,
      spaceId,
      groupId,
      status.cycleId!,
      member.personId
    );
    const reason = `Browser E2E cancellation ${Date.now()}`;
    const slotBefore = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      ownedShift.shiftSlotId
    );

    await loginAsUser(page, memberEmail, DEMO_PASSWORD);
    await page.evaluate((targetGroupId) => {
      localStorage.setItem("shifter-pick-last-group", targetGroupId);
    }, groupId);
    await page.goto(`${BASE}/pick`);
    await page.getByTestId("pick-tab-my-shifts").click();
    const shiftCard = page.locator(`[data-testid="self-service-shift-card"][data-shift-request-id="${ownedShift.id}"]`);
    await expect(shiftCard.getByTestId("self-service-cancel-shift")).toBeVisible({ timeout: 15000 });
    await shiftCard.getByTestId("self-service-cancel-shift").click();
    await page.locator("textarea").fill(reason);
    await Promise.all([
      page.waitForResponse((response) =>
        response.url().includes(`/shift-requests/${ownedShift.id}/cancel`)
        && response.request().method() === "POST"
      ),
      page.getByTestId("self-service-confirm-cancel-shift").click(),
    ]);

    const refreshedShifts = await api<MyShiftsResponse>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-requests/mine`
    );
    const cancelledShift = refreshedShifts.requests.find((row) => row.id === ownedShift.id);
    expect(cancelledShift?.status).toBe("Cancelled");
    expect(cancelledShift?.cancellationReason).toBe(reason);
    const slotAfter = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      ownedShift.shiftSlotId
    );
    expect(slotAfter.currentFillCount).toBe(slotBefore.currentFillCount - 1);

    await expect(shiftCard).toBeVisible({ timeout: 15000 });
  });

  test("member submits and admin approves a shift-change request through the UI", async ({ page, request }) => {
    const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local";
    const memberEmail = process.env.E2E_CHANGE_MEMBER_EMAIL ?? "ofek@demo.local";
    const adminToken = await login(request, adminEmail);
    const memberToken = await login(request, memberEmail);
    const { spaceId, groupId } = await findDemoSelfServiceGroup(request, adminToken);
    const status = await api<CycleStatusDto>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/status`
    );
    expect(status.cycleId).toBeTruthy();

    const member = await getGroupMemberByEmail(request, adminToken, spaceId, groupId, memberEmail);
    const ownedShift = await ensureChangeableApprovedShift(
      request,
      adminToken,
      memberToken,
      spaceId,
      groupId,
      status.cycleId!,
      member.personId
    );
    const targetSlot = await findOpenTargetSlot(
      request,
      memberToken,
      spaceId,
      groupId,
      status.cycleId!,
      ownedShift.shiftSlotId
    );
    const targetSlotId = targetSlot.id ?? targetSlot.shiftSlotId;
    expect(targetSlotId).toBeTruthy();
    const originalSlotBeforeChange = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      ownedShift.shiftSlotId
    );
    const targetSlotBeforeChange = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      targetSlotId!
    );

    const reason = `Browser E2E shift change ${Date.now()}`;

    await loginAsUser(page, memberEmail, DEMO_PASSWORD);
    await page.evaluate((targetGroupId) => {
      localStorage.setItem("shifter-pick-last-group", targetGroupId);
    }, groupId);
    await page.goto(`${BASE}/pick`);
    await page.getByTestId("pick-tab-my-shifts").click();
    await expect(page.getByTestId("self-service-change-shift").first()).toBeVisible({ timeout: 15000 });
    await page.getByTestId("self-service-change-shift").first().click();
    await page.getByTestId("self-service-change-target-slot").selectOption(targetSlotId!);
    await page.locator("textarea").fill(reason);
    await Promise.all([
      page.waitForResponse((response) =>
        response.url().includes("/shift-change-requests") && response.request().method() === "POST"
      ),
      page.getByTestId("self-service-confirm-change").click(),
    ]);

    const changes = await api<ShiftChangeRequestDto[]>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-change-requests/mine`
    );
    const createdChange = changes.find((row) => row.reason === reason);
    expect(createdChange?.status).toBe("Pending");
    expect(createdChange?.requestedShiftSlotId).toBe(targetSlotId);

    await loginAsUser(page, adminEmail, DEMO_PASSWORD);
    await enterElevatedGroup(page, groupId);
    await page.goto(`${BASE}/groups/${groupId}`);
    await page.getByTestId("group-tab-absence-reports").click();

    const changeCard = page
      .getByTestId("self-service-change-request")
      .filter({ hasText: reason });
    await expect(changeCard).toBeVisible({ timeout: 15000 });
    await changeCard.getByTestId("self-service-change-approval-target").selectOption(targetSlotId!);
    await Promise.all([
      page.waitForResponse((response) =>
        response.url().includes(`/shift-change-requests/admin/${createdChange!.id}/approve`)
        && response.request().method() === "POST"
      ),
      changeCard.getByTestId("self-service-approve-change").click(),
    ]);

    const reviewedChanges = await api<ShiftChangeRequestDto[]>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-change-requests/mine`
    );
    expect(reviewedChanges.find((row) => row.id === createdChange!.id)?.status).toBe("Approved");

    const refreshedShifts = await api<MyShiftsResponse>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-requests/mine`
    );
    const reassignedShift = refreshedShifts.requests.find((row) => row.id === ownedShift.id);
    expect(reassignedShift?.status).toBe("Approved");
    expect(reassignedShift?.shiftSlotId).toBe(targetSlotId);

    const originalSlotAfterChange = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      ownedShift.shiftSlotId
    );
    const targetSlotAfterChange = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      targetSlotId!
    );
    expect(originalSlotAfterChange.currentFillCount).toBe(originalSlotBeforeChange.currentFillCount - 1);
    expect(targetSlotAfterChange.currentFillCount).toBe(targetSlotBeforeChange.currentFillCount + 1);
  });

  test("admin rejects a member shift-change request through the UI", async ({ page, request }) => {
    const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local";
    const memberEmail = process.env.E2E_REJECT_CHANGE_MEMBER_EMAIL ?? "viewer@demo.local";
    const adminToken = await login(request, adminEmail);
    const memberToken = await login(request, memberEmail);
    const { spaceId, groupId } = await findDemoSelfServiceGroup(request, adminToken);
    const status = await api<CycleStatusDto>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/status`
    );
    expect(status.cycleId).toBeTruthy();

    const member = await getGroupMemberByEmail(request, adminToken, spaceId, groupId, memberEmail);
    const ownedShift = await ensureChangeableApprovedShift(
      request,
      adminToken,
      memberToken,
      spaceId,
      groupId,
      status.cycleId!,
      member.personId
    );
    const originalSlotId = ownedShift.shiftSlotId;
    const reason = `Browser E2E reject shift change ${Date.now()}`;
    const originalSlotBeforeChange = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      originalSlotId
    );

    await loginAsUser(page, memberEmail, DEMO_PASSWORD);
    await page.evaluate((targetGroupId) => {
      localStorage.setItem("shifter-pick-last-group", targetGroupId);
    }, groupId);
    await page.goto(`${BASE}/pick`);
    await page.getByTestId("pick-tab-my-shifts").click();
    const shiftCard = page.locator(`[data-testid="self-service-shift-card"][data-shift-request-id="${ownedShift.id}"]`);
    await expect(shiftCard.getByTestId("self-service-change-shift")).toBeVisible({ timeout: 15000 });
    await shiftCard.getByTestId("self-service-change-shift").click();
    await page.locator("textarea").fill(reason);
    await Promise.all([
      page.waitForResponse((response) =>
        response.url().includes("/shift-change-requests") && response.request().method() === "POST"
      ),
      page.getByTestId("self-service-confirm-change").click(),
    ]);

    const changes = await api<ShiftChangeRequestDto[]>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-change-requests/mine`
    );
    const createdChange = changes.find((row) => row.reason === reason);
    expect(createdChange?.status).toBe("Pending");

    await loginAsUser(page, adminEmail, DEMO_PASSWORD);
    await enterElevatedGroup(page, groupId);
    await page.goto(`${BASE}/groups/${groupId}`);
    await page.getByTestId("group-tab-absence-reports").click();
    const changeCard = page
      .getByTestId("self-service-change-request")
      .filter({ hasText: reason });
    await expect(changeCard).toBeVisible({ timeout: 15000 });
    await Promise.all([
      page.waitForResponse((response) =>
        response.url().includes(`/shift-change-requests/admin/${createdChange!.id}/reject`)
        && response.request().method() === "POST"
      ),
      changeCard.getByTestId("self-service-reject-change").click(),
    ]);

    const reviewedChanges = await api<ShiftChangeRequestDto[]>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-change-requests/mine`
    );
    expect(reviewedChanges.find((row) => row.id === createdChange!.id)?.status).toBe("Rejected");

    const refreshedShifts = await api<MyShiftsResponse>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-requests/mine`
    );
    const unchangedShift = refreshedShifts.requests.find((row) => row.id === ownedShift.id);
    expect(unchangedShift?.status).toBe("Approved");
    expect(unchangedShift?.shiftSlotId).toBe(originalSlotId);

    const originalSlotAfterRejection = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      originalSlotId
    );
    expect(originalSlotAfterRejection.currentFillCount).toBe(originalSlotBeforeChange.currentFillCount);
  });

  test("member can pick an open shift and join a full-slot waitlist through the UI", async ({ page, request }) => {
    const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local";
    const memberEmail = process.env.E2E_PICK_MEMBER_EMAIL ?? "viewer@demo.local";
    const adminToken = await login(request, adminEmail);
    const memberToken = await login(request, memberEmail);
    const { spaceId, groupId } = await findDemoSelfServiceGroup(request, adminToken);
    const status = await api<CycleStatusDto>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/status`
    );
    expect(status.cycleId).toBeTruthy();

    const availableBefore = await api<AvailableSlotsResponse>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-slots/available?cycleId=${status.cycleId}`
    );
    const openSlot = availableBefore.slots.find((slot) => slot.currentFillCount < slot.capacity);
    const fullSlot = availableBefore.slots.find((slot) => slot.currentFillCount >= slot.capacity);
    const existingWaitlist = await api<WaitlistEntryDto[]>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/waitlist/mine`
    );

    await loginAsUser(page, memberEmail, DEMO_PASSWORD);
    await page.evaluate((targetGroupId) => {
      localStorage.setItem("shifter-pick-last-group", targetGroupId);
    }, groupId);
    await page.goto(`${BASE}/pick`);

    if (openSlot) {
      const openSlotId = openSlot.id ?? openSlot.shiftSlotId;
      const openSlotBeforePick = await getAdminSlot(
        request,
        adminToken,
        spaceId,
        groupId,
        status.cycleId!,
        openSlotId!
      );
      await page.getByTestId("pick-tab-slots").click();
      await expect(page.getByTestId("self-service-open-slot").first()).toBeVisible({ timeout: 15000 });
      await Promise.all([
        page.waitForResponse((response) =>
          response.url().includes("/shift-requests") && response.request().method() === "POST"
        ),
        page.getByTestId("self-service-request-shift").first().click(),
      ]);

      const shifts = await api<MyShiftsResponse>(
        request,
        memberToken,
        "GET",
        `/spaces/${spaceId}/groups/${groupId}/shift-requests/mine`
      );
      expect(shifts.requests.some((row) =>
        row.shiftSlotId === openSlotId && row.status === "Approved"
      )).toBeTruthy();

      const openSlotAfterPick = await getAdminSlot(
        request,
        adminToken,
        spaceId,
        groupId,
        status.cycleId!,
        openSlotId!
      );
      expect(openSlotAfterPick.currentFillCount).toBe(openSlotBeforePick.currentFillCount + 1);
    }

    const alreadyWaitlisted = fullSlot
      ? existingWaitlist.some((entry) =>
          entry.shiftSlotId === (fullSlot.id ?? fullSlot.shiftSlotId)
          && (entry.status === "Waiting" || entry.status === "Offered")
        )
      : existingWaitlist.some((entry) => entry.status === "Waiting" || entry.status === "Offered");

    if (fullSlot && !alreadyWaitlisted) {
      const fullSlotId = fullSlot.id ?? fullSlot.shiftSlotId;
      const fullSlotBeforeWaitlist = await getAdminSlot(
        request,
        adminToken,
        spaceId,
        groupId,
        status.cycleId!,
        fullSlotId!
      );
      await page.getByTestId("pick-tab-slots").click();
      await expect(page.getByTestId("self-service-full-slot").first()).toBeVisible({ timeout: 15000 });
      await Promise.all([
        page.waitForResponse((response) =>
          response.url().includes("/waitlist") && response.request().method() === "POST"
        ),
        page.getByTestId("self-service-join-waitlist").first().click(),
      ]);

      const fullSlotAfterWaitlist = await getAdminSlot(
        request,
        adminToken,
        spaceId,
        groupId,
        status.cycleId!,
        fullSlotId!
      );
      expect(fullSlotAfterWaitlist.currentFillCount).toBe(fullSlotBeforeWaitlist.currentFillCount);
    }

    const waitlistAfter = await api<WaitlistEntryDto[]>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/waitlist/mine`
    );
    expect(waitlistAfter.some((entry) => entry.status === "Waiting" || entry.status === "Offered")).toBeTruthy();

    await page.getByTestId("pick-tab-waitlist").click();
    await expect(
      page.getByTestId("self-service-waitlist-entry")
        .or(page.getByTestId("self-service-offered-waitlist-entry"))
        .first()
    ).toBeVisible({ timeout: 15000 });
  });

  test("member leaves a waiting-list entry through the UI", async ({ page, request }) => {
    const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local";
    const memberEmail = process.env.E2E_WAITLIST_LEAVE_MEMBER_EMAIL ?? "viewer@demo.local";
    const adminToken = await login(request, adminEmail);
    const memberToken = await login(request, memberEmail);
    const { spaceId, groupId } = await findDemoSelfServiceGroup(request, adminToken);
    const status = await api<CycleStatusDto>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/status`
    );
    expect(status.cycleId).toBeTruthy();

    const waitingEntry = await ensureWaitingWaitlistEntry(
      request,
      memberToken,
      spaceId,
      groupId,
      status.cycleId!
    );
    const slotBeforeLeave = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      waitingEntry.shiftSlotId
    );

    await loginAsUser(page, memberEmail, DEMO_PASSWORD);
    await page.evaluate((targetGroupId) => {
      localStorage.setItem("shifter-pick-last-group", targetGroupId);
    }, groupId);
    await page.goto(`${BASE}/pick`);
    await page.getByTestId("pick-tab-waitlist").click();

    const entryCard = page.locator(
      `[data-testid="self-service-waitlist-entry"][data-waitlist-entry-id="${waitingEntry.id}"]`
    );
    await expect(entryCard).toBeVisible({ timeout: 15000 });
    await entryCard.getByTestId("self-service-leave-waitlist").click();
    await Promise.all([
      page.waitForResponse((response) =>
        response.url().includes(`/waitlist/${waitingEntry.shiftSlotId}`)
        && response.request().method() === "DELETE"
      ),
      page.getByTestId("self-service-confirm-leave-waitlist").click(),
    ]);

    const entriesAfter = await api<WaitlistEntryDto[]>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/waitlist/mine`
    );
    expect(entriesAfter.some((entry) =>
      entry.id === waitingEntry.id && (entry.status === "Waiting" || entry.status === "Offered")
    )).toBeFalsy();

    const slotAfterLeave = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      waitingEntry.shiftSlotId
    );
    expect(slotAfterLeave.currentFillCount).toBe(slotBeforeLeave.currentFillCount);
  });

  test("member reports cannot attend and admin approves it through the UI", async ({ page, request }) => {
    const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local";
    const memberEmail = process.env.E2E_BROWSER_MEMBER_EMAIL ?? "viewer@demo.local";
    const adminToken = await login(request, adminEmail);
    const memberToken = await login(request, memberEmail);
    const { spaceId, groupId } = await findDemoSelfServiceGroup(request, adminToken);
    const status = await api<CycleStatusDto>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/status`
    );
    expect(status.cycleId).toBeTruthy();

    const limits = await api<MyAbsenceReportsResponse>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-requests/absence-reports/mine`
    );
    test.skip(
      limits.absenceReportsUsed >= limits.maxAbsenceReports,
      `${memberEmail} has used all absence reports in the seeded demo cycle`
    );

    const member = await getGroupMemberByEmail(request, adminToken, spaceId, groupId, memberEmail);
    await ensureApprovedShift(request, adminToken, memberToken, spaceId, groupId, status.cycleId!, member.personId);

    const reason = `Browser E2E cannot attend ${Date.now()}`;

    await loginAsUser(page, memberEmail, DEMO_PASSWORD);
    await page.evaluate((targetGroupId) => {
      localStorage.setItem("shifter-pick-last-group", targetGroupId);
    }, groupId);
    await page.goto(`${BASE}/pick`);
    await page.getByTestId("pick-tab-my-shifts").click();
    await expect(page.getByTestId("self-service-cannot-attend").first()).toBeVisible({ timeout: 15000 });
    await page.getByTestId("self-service-cannot-attend").first().click();
    await page.locator("textarea").fill(reason);
    await Promise.all([
      page.waitForResponse((response) =>
        response.url().includes("/cannot-attend") && response.request().method() === "POST"
      ),
      page.getByTestId("self-service-confirm-cannot-attend").click(),
    ]);

    const memberReports = await api<MyAbsenceReportsResponse>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-requests/absence-reports/mine`
    );
    const createdReport = memberReports.reports.find((report) => report.reason === reason);
    expect(createdReport?.status).toBe("Pending");

    await loginAsUser(page, adminEmail, DEMO_PASSWORD);
    await enterElevatedGroup(page, groupId);
    await page.goto(`${BASE}/groups/${groupId}`);
    await page.getByTestId("group-tab-absence-reports").click();

    const reportCard = page
      .getByTestId("self-service-absence-report")
      .filter({ hasText: reason });
    await expect(reportCard).toBeVisible({ timeout: 15000 });
    await Promise.all([
      page.waitForResponse((response) =>
        response.url().includes(`/absence-reports/${createdReport!.id}/approve`)
        && response.request().method() === "POST"
      ),
      reportCard.getByTestId("self-service-approve-absence").click(),
    ]);

    const reviewed = await api<AbsenceReportDto[]>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-requests/absence-reports`
    );
    expect(reviewed.find((report) => report.id === createdReport!.id)?.status).toBe("Approved");
  });

  test("member reports cannot attend and admin rejects it through the UI", async ({ page, request }) => {
    const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local";
    const memberEmail = process.env.E2E_REJECT_ABSENCE_MEMBER_EMAIL ?? "ofek@demo.local";
    const adminToken = await login(request, adminEmail);
    const memberToken = await login(request, memberEmail);
    const { spaceId, groupId } = await findDemoSelfServiceGroup(request, adminToken);
    const status = await api<CycleStatusDto>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/self-service-cycles/status`
    );
    expect(status.cycleId).toBeTruthy();

    const limits = await api<MyAbsenceReportsResponse>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-requests/absence-reports/mine`
    );
    test.skip(
      limits.absenceReportsUsed >= limits.maxAbsenceReports,
      `${memberEmail} has used all absence reports in the seeded demo cycle`
    );

    const member = await getGroupMemberByEmail(request, adminToken, spaceId, groupId, memberEmail);
    const ownedShift = await ensureAbsenceReportableApprovedShift(
      request,
      adminToken,
      memberToken,
      spaceId,
      groupId,
      status.cycleId!,
      member.personId
    );
    const reason = `Browser E2E reject cannot attend ${Date.now()}`;
    const slotBeforeReport = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      ownedShift.shiftSlotId
    );

    await loginAsUser(page, memberEmail, DEMO_PASSWORD);
    await page.evaluate((targetGroupId) => {
      localStorage.setItem("shifter-pick-last-group", targetGroupId);
    }, groupId);
    await page.goto(`${BASE}/pick`);
    await page.getByTestId("pick-tab-my-shifts").click();
    const shiftCard = page.locator(`[data-testid="self-service-shift-card"][data-shift-request-id="${ownedShift.id}"]`);
    await expect(shiftCard.getByTestId("self-service-cannot-attend")).toBeVisible({ timeout: 15000 });
    await shiftCard.getByTestId("self-service-cannot-attend").click();
    await page.locator("textarea").fill(reason);
    await Promise.all([
      page.waitForResponse((response) =>
        response.url().includes("/cannot-attend") && response.request().method() === "POST"
      ),
      page.getByTestId("self-service-confirm-cannot-attend").click(),
    ]);

    const memberReports = await api<MyAbsenceReportsResponse>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-requests/absence-reports/mine`
    );
    const createdReport = memberReports.reports.find((report) => report.reason === reason);
    expect(createdReport?.status).toBe("Pending");

    await loginAsUser(page, adminEmail, DEMO_PASSWORD);
    await enterElevatedGroup(page, groupId);
    await page.goto(`${BASE}/groups/${groupId}`);
    await page.getByTestId("group-tab-absence-reports").click();

    const reportCard = page
      .getByTestId("self-service-absence-report")
      .filter({ hasText: reason });
    await expect(reportCard).toBeVisible({ timeout: 15000 });
    await Promise.all([
      page.waitForResponse((response) =>
        response.url().includes(`/absence-reports/${createdReport!.id}/reject`)
        && response.request().method() === "POST"
      ),
      reportCard.getByTestId("self-service-reject-absence").click(),
    ]);

    const reviewed = await api<AbsenceReportDto[]>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-requests/absence-reports`
    );
    expect(reviewed.find((report) => report.id === createdReport!.id)?.status).toBe("Rejected");

    const restoredShifts = await api<MyShiftsResponse>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/groups/${groupId}/shift-requests/mine`
    );
    const restoredShift = restoredShifts.requests.find((row) => row.id === ownedShift.id);
    expect(restoredShift?.status).toBe("Approved");
    expect(restoredShift?.shiftSlotId).toBe(ownedShift.shiftSlotId);
    expect(restoredShift?.cancellationReason).toBeNull();

    const slotAfterRejection = await getAdminSlot(
      request,
      adminToken,
      spaceId,
      groupId,
      status.cycleId!,
      ownedShift.shiftSlotId
    );
    expect(slotAfterRejection.currentFillCount).toBe(slotBeforeReport.currentFillCount);
  });

  test("member requests special leave and admin approves it through the UI", async ({ page, request }) => {
    const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local";
    const memberEmail = process.env.E2E_SPECIAL_LEAVE_MEMBER_EMAIL ?? "ofek@demo.local";
    const adminToken = await login(request, adminEmail);
    const memberToken = await login(request, memberEmail);
    const { spaceId, groupId } = await findDemoSelfServiceGroup(request, adminToken);
    await getGroupMemberByEmail(request, adminToken, spaceId, groupId, memberEmail);
    const reason = `Browser E2E special leave ${Date.now()}`;

    await loginAsUser(page, memberEmail, DEMO_PASSWORD);
    const createdRequest = await submitSpecialLeaveThroughUi(page, request, memberToken, spaceId, groupId, reason);

    await loginAsUser(page, adminEmail, DEMO_PASSWORD);
    await enterElevatedGroup(page, groupId);
    await page.goto(`${BASE}/groups/${groupId}`);
    await page.getByTestId("group-tab-absence-reports").click();

    const reviewCard = page.locator(
      `[data-testid="self-service-special-leave-review"][data-special-leave-request-id="${createdRequest!.id}"]`
    );
    await expect(reviewCard).toBeVisible({ timeout: 15000 });
    await Promise.all([
      page.waitForResponse((response) =>
        response.url().includes(`/special-leave-requests/admin/${createdRequest!.id}/approve`)
        && response.request().method() === "POST"
      ),
      reviewCard.getByTestId("self-service-approve-special-leave").click(),
    ]);

    const reviewed = await api<SpecialLeaveRequestDto[]>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/special-leave-requests/admin?groupId=${groupId}`
    );
    const approved = reviewed.find((row) => row.id === createdRequest!.id);
    expect(approved?.status).toBe("Approved");
    expect(approved?.presenceWindowId).toBeTruthy();
  });

  test("member requests and cancels special leave through the UI", async ({ page, request }) => {
    const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local";
    const memberEmail = process.env.E2E_SPECIAL_LEAVE_CANCEL_MEMBER_EMAIL ?? "ofek@demo.local";
    const adminToken = await login(request, adminEmail);
    const memberToken = await login(request, memberEmail);
    const { spaceId, groupId } = await findDemoSelfServiceGroup(request, adminToken);
    await getGroupMemberByEmail(request, adminToken, spaceId, groupId, memberEmail);
    const reason = `Browser E2E cancel special leave ${Date.now()}`;

    await loginAsUser(page, memberEmail, DEMO_PASSWORD);
    const createdRequest = await submitSpecialLeaveThroughUi(page, request, memberToken, spaceId, groupId, reason);
    const memberCard = page.locator(
      `[data-testid="self-service-special-leave-card"][data-special-leave-request-id="${createdRequest.id}"]`
    );

    await Promise.all([
      page.waitForResponse((response) =>
        response.url().includes(`/special-leave-requests/${createdRequest.id}/cancel`)
        && response.request().method() === "POST"
      ),
      memberCard.getByTestId("self-service-cancel-special-leave").click(),
    ]);

    const memberRequests = await api<SpecialLeaveRequestDto[]>(
      request,
      memberToken,
      "GET",
      `/spaces/${spaceId}/special-leave-requests/mine`
    );
    const cancelled = memberRequests.find((row) => row.id === createdRequest.id);
    expect(cancelled?.status).toBe("Cancelled");
    expect(cancelled?.presenceWindowId).toBeNull();
  });

  test("member requests special leave and admin rejects it through the UI", async ({ page, request }) => {
    const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@demo.local";
    const memberEmail = process.env.E2E_SPECIAL_LEAVE_REJECT_MEMBER_EMAIL ?? "ofek@demo.local";
    const adminToken = await login(request, adminEmail);
    const memberToken = await login(request, memberEmail);
    const { spaceId, groupId } = await findDemoSelfServiceGroup(request, adminToken);
    await getGroupMemberByEmail(request, adminToken, spaceId, groupId, memberEmail);
    const reason = `Browser E2E reject special leave ${Date.now()}`;

    await loginAsUser(page, memberEmail, DEMO_PASSWORD);
    const createdRequest = await submitSpecialLeaveThroughUi(page, request, memberToken, spaceId, groupId, reason);

    await loginAsUser(page, adminEmail, DEMO_PASSWORD);
    await enterElevatedGroup(page, groupId);
    await page.goto(`${BASE}/groups/${groupId}`);
    await page.getByTestId("group-tab-absence-reports").click();

    const reviewCard = page.locator(
      `[data-testid="self-service-special-leave-review"][data-special-leave-request-id="${createdRequest.id}"]`
    );
    await expect(reviewCard).toBeVisible({ timeout: 15000 });
    await Promise.all([
      page.waitForResponse((response) =>
        response.url().includes(`/special-leave-requests/admin/${createdRequest.id}/reject`)
        && response.request().method() === "POST"
      ),
      reviewCard.getByTestId("self-service-reject-special-leave").click(),
    ]);

    const reviewed = await api<SpecialLeaveRequestDto[]>(
      request,
      adminToken,
      "GET",
      `/spaces/${spaceId}/special-leave-requests/admin?groupId=${groupId}`
    );
    const rejected = reviewed.find((row) => row.id === createdRequest.id);
    expect(rejected?.status).toBe("Rejected");
    expect(rejected?.presenceWindowId).toBeNull();
  });
});
