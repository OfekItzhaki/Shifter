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
}

interface AvailableSlotDto {
  id?: string;
  shiftSlotId?: string;
}

interface AvailableSlotsResponse {
  slots: AvailableSlotDto[];
}

interface ShiftRequestDto {
  id: string;
  shiftSlotId: string;
  status: "Pending" | "Approved" | "Rejected" | "Cancelled";
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

interface MyAbsenceReportsResponse {
  reports: AbsenceReportDto[];
  absenceReportsUsed: number;
  maxAbsenceReports: number;
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

test.describe("Self-service browser lifecycle", () => {
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
});
