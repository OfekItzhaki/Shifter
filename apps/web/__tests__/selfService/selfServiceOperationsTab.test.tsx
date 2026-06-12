import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import SelfServiceOperationsTab from "../../components/groups/selfService/SelfServiceOperationsTab";

const mockGetSelfServiceCycleStatus = vi.fn();
const mockGetSelfServiceCycleCloseout = vi.fn();
const mockGetSelfServiceConfig = vi.fn();
const mockGetAdminWaitlistEntries = vi.fn();
const mockDownloadSelfServiceCycleCloseoutCsv = vi.fn();
const mockDownloadSelfServiceCycleCloseoutPdf = vi.fn();
const mockCycleControlPanel = vi.fn();

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string, values?: Record<string, unknown>) => {
    const translations: Record<string, string> = {
      title: "Self-service operations",
      description: "Run the current cycle and jump into queues.",
      statusLoading: "Checking queues...",
      activeSignals: `${values?.count ?? 0} item(s) need attention`,
      allClear: "No urgent items",
      "closeout.title": "Cycle closeout",
      "closeout.description": "Summarize this cycle.",
      "closeout.needsReview": `${values?.count ?? 0} unresolved item(s)`,
      "closeout.ready": "Ready for closeout",
      "closeout.exportCsv": "Export CSV",
      "closeout.exportPdf": "Export PDF",
      "closeout.exporting": "Exporting...",
      "closeout.exportError": "Could not export closeout CSV.",
      "closeout.metrics.coverage": "Coverage",
      "closeout.metrics.underfilled": "Under-filled",
      "closeout.metrics.pending": "Unresolved",
      "closeout.metrics.overrides": "Overrides",
      "closeout.metrics.lateAbsences": "Late absences",
      "closeout.metrics.noShows": "No-shows",
      "closeout.details.assignments": "Assignments",
      "closeout.details.assignmentsValue": `${values?.approved ?? 0} approved / ${values?.cancelled ?? 0} cancelled / ${values?.rejected ?? 0} rejected`,
      "closeout.details.absences": "Absences",
      "closeout.details.absencesValue": `${values?.approved ?? 0} approved / ${values?.rejected ?? 0} rejected / ${values?.pending ?? 0} pending`,
      "closeout.details.attendance": "Attendance",
      "closeout.details.attendanceValue": `${values?.present ?? 0} present / ${values?.noshow ?? 0} no-show / ${values?.unconfirmed ?? 0} unconfirmed`,
      "closeout.details.changes": "Changes",
      "closeout.details.changesValue": `${values?.approved ?? 0} approved / ${values?.rejected ?? 0} rejected / ${values?.pending ?? 0} pending`,
      "closeout.details.waitlist": "Waitlist",
      "closeout.details.waitlistValue": `${values?.active ?? 0} active / ${values?.accepted ?? 0} accepted / ${values?.expired ?? 0} expired`,
      "closeout.details.specialDays": "Special days",
      "closeout.details.specialDaysValue": `${values?.total ?? 0} slot(s) / ${values?.noCoverage ?? 0} no-coverage / ${values?.underfilled ?? 0} under-filled`,
      "closeout.details.workflowPolicy": "Workflow policy",
      "closeout.details.workflowPolicyValue": `${values?.enabled ?? 0}/${values?.total ?? 0} workflows enabled`,
      "policy.title": "Active member policy",
      "policy.description": "Current rules for members.",
      "policy.edit": "Edit policy",
      "policy.enabled": "Enabled",
      "policy.disabled": "Off",
      "policy.loading": "-",
      "policy.workflows.claims": "Claims",
      "policy.workflows.waitlist": "Waitlist",
      "policy.workflows.changes": "Changes",
      "policy.workflows.absence": "Absence",
      "policy.workflows.swaps": "Swaps",
      "policy.metrics.shiftLimit": "Shift limit",
      "policy.metrics.shiftLimitValue": `${values?.min ?? 0}-${values?.max ?? 0} per cycle`,
      "policy.metrics.absenceLimit": "Late absence",
      "policy.metrics.absenceLimitValue": `${values?.max ?? 0} inside ${values?.hours ?? 0}h`,
      "policy.metrics.cutoff": "Cutoff and offers",
      "policy.metrics.cutoffValue": `Cancel ${values?.hours ?? 0}h / offer ${values?.minutes ?? 0}m`,
      "priority.title": "Priority signals",
      "priority.description": "Handle these first.",
      "priority.count": `${values?.count ?? 0} urgent signal(s)`,
      "priority.clear": "No urgent signals",
      "priority.empty": "Nothing urgent right now.",
      "priority.open": "Open queue",
      "priority.underfilledList.title": "Coverage gaps to fix first",
      "priority.underfilledList.description": `Showing the next ${values?.count ?? 0} under-filled slot(s).`,
      "priority.underfilledList.openOverrides": "Open manual overrides",
      "priority.underfilledList.openSeats": `${values?.count ?? 0} open`,
      "priority.signals.lateReports.title": "Late absence reports",
      "priority.signals.lateReports.description": `${values?.count ?? 0} late report(s) need review.`,
      "priority.signals.expiringWaitlist.title": "Expiring waitlist offers",
      "priority.signals.expiringWaitlist.description": `${values?.count ?? 0} waitlist offer(s) expire in ${values?.minutes ?? 0} minutes.`,
      "priority.signals.pendingSwaps.title": "Pending peer swaps",
      "priority.signals.pendingSwaps.description": `${values?.count ?? 0} swap proposal(s) need member response.`,
      "priority.signals.underfilled.title": "Under-filled slots",
      "priority.signals.underfilled.description": `${values?.count ?? 0} slot(s) still need coverage.`,
      "reviews.title": "Review breakdown",
      "reviews.description": "See which member requests need decisions.",
      "reviews.count": `${values?.count ?? 0} pending decision(s)`,
      "reviews.clear": "No pending decisions",
      "reviews.items.absences.title": "Absence reports",
      "reviews.items.absences.description": `${values?.count ?? 0} absence report(s) waiting.`,
      "reviews.items.changes.title": "Shift changes",
      "reviews.items.changes.description": `${values?.count ?? 0} shift-change request(s) waiting.`,
      "reviews.items.leave.title": "Special leave",
      "reviews.items.leave.description": `${values?.count ?? 0} time-off request(s) waiting.`,
      "guide.title": "How to run the cycle",
      "guide.description": "Use this rhythm for manual self-service scheduling.",
      "guide.steps.prepare.title": "Prepare",
      "guide.steps.prepare.description": "Set policy and templates.",
      "guide.steps.open.title": "Open",
      "guide.steps.open.description": "Let members pick shifts.",
      "guide.steps.review.title": "Review",
      "guide.steps.review.description": "Handle absences and changes.",
      "guide.steps.improve.title": "Improve",
      "guide.steps.improve.description": "Tune the next cycle.",
      "guide.workflows.member": "Member action",
      "guide.workflows.admin": "Admin follow-up",
      "guide.workflows.result": "Expected result",
      "guide.workflows.picking.title": "Pick shifts and waitlist",
      "guide.workflows.picking.member": "Members pick open shifts.",
      "guide.workflows.picking.admin": "Watch waitlist offers.",
      "guide.workflows.picking.result": "Accepted offers create assignments.",
      "guide.workflows.changes.title": "Changes and absence",
      "guide.workflows.changes.member": "Members request changes or report absence.",
      "guide.workflows.changes.admin": "Review pending queues.",
      "guide.workflows.changes.result": "Changes move assignments.",
      "guide.workflows.leave.title": "Special leave",
      "guide.workflows.leave.member": "Members request time off.",
      "guide.workflows.leave.admin": "Approve or reject requests.",
      "guide.workflows.leave.result": "Approved leave creates presence.",
      "actions.reviews.title": "Review requests",
      "actions.reviews.description": "Approve review requests.",
      "actions.reviews.metric": `${values?.count ?? 0} pending review item(s)`,
      "actions.waitlist.title": "Waitlist queue",
      "actions.waitlist.description": "See active waitlist entries.",
      "actions.waitlist.metric": `${values?.count ?? 0} active waitlist item(s)`,
      "actions.swaps.title": "Peer swaps",
      "actions.swaps.description": "Review pending swaps.",
      "actions.swaps.metric": `${values?.count ?? 0} pending swap proposal(s)`,
      "actions.overrides.title": "Manual overrides",
      "actions.overrides.description": "Fix schedule coverage.",
      "actions.overrides.metric": `${values?.count ?? 0} under-filled slot(s)`,
      "actions.templates.title": "Shift templates",
      "actions.templates.description": "Adjust generated slots.",
      "actions.policy.title": "Policy settings",
      "actions.policy.description": "Tune self-service rules.",
    };
    return translations[key] ?? key;
  },
}));

vi.mock("../../lib/api/selfService", () => ({
  getSelfServiceCycleStatus: (...args: unknown[]) => mockGetSelfServiceCycleStatus(...args),
  getSelfServiceCycleCloseout: (...args: unknown[]) => mockGetSelfServiceCycleCloseout(...args),
  getSelfServiceConfig: (...args: unknown[]) => mockGetSelfServiceConfig(...args),
  getAdminWaitlistEntries: (...args: unknown[]) => mockGetAdminWaitlistEntries(...args),
  downloadSelfServiceCycleCloseoutCsv: (...args: unknown[]) => mockDownloadSelfServiceCycleCloseoutCsv(...args),
  downloadSelfServiceCycleCloseoutPdf: (...args: unknown[]) => mockDownloadSelfServiceCycleCloseoutPdf(...args),
}));

vi.mock("../../components/groups/selfService/CycleControlPanel", () => ({
  default: (props: { onStatusChanged?: () => void }) => {
    mockCycleControlPanel(props);
    return (
      <button type="button" data-testid="cycle-control-panel" onClick={() => props.onStatusChanged?.()}>
        Cycle control
      </button>
    );
  },
}));

describe("SelfServiceOperationsTab", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetSelfServiceCycleStatus.mockResolvedValue({
      cycleId: "cycle-1",
      startsAt: "2026-06-20T00:00:00",
      endsAt: "2026-06-27T00:00:00",
      requestWindowOpensAt: "2026-06-13T00:00:00",
      requestWindowClosesAt: "2026-06-19T00:00:00",
      requestWindowOpen: true,
      isGenerated: true,
      slotCount: 10,
      totalCapacity: 20,
      filledCount: 17,
      approvedCount: 17,
      pendingCount: 2,
      waitlistCount: 4,
      pendingAbsenceReportCount: 2,
      latePendingAbsenceReportCount: 1,
      pendingShiftChangeRequestCount: 3,
      pendingSwapRequestCount: 2,
      pendingSpecialLeaveRequestCount: 1,
      underfilledSlotCount: 3,
      underfilledSlots: [
        {
          shiftSlotId: "slot-1",
          date: "2026-06-21",
          startTime: "08:00",
          endTime: "16:00",
          taskName: "Front desk",
          currentFillCount: 1,
          capacity: 2,
          openSeats: 1,
        },
      ],
    });
    mockGetSelfServiceCycleCloseout.mockResolvedValue({
      cycleId: "cycle-1",
      startsAt: "2026-06-20T00:00:00",
      endsAt: "2026-06-27T00:00:00",
      isClosed: false,
      allowMemberShiftClaims: true,
      allowWaitlist: false,
      allowShiftChangeRequests: false,
      allowAbsenceReports: true,
      allowShiftSwaps: true,
      slotCount: 10,
      totalCapacity: 20,
      filledCount: 17,
      underfilledSlotCount: 3,
      overfilledSlotCount: 0,
      specialDaySlotCount: 2,
      noCoverageSpecialDaySlotCount: 1,
      underfilledSpecialDaySlotCount: 1,
      approvedAssignments: 17,
      cancelledAssignments: 2,
      rejectedRequests: 1,
      pendingRequests: 2,
      adminOverrideAssignments: 1,
      cannotAttendCancellations: 2,
      lateAbsenceReports: 1,
      approvedAbsenceReports: 1,
      rejectedAbsenceReports: 0,
      pendingAbsenceReports: 2,
      presentAttendanceRecords: 12,
      noShowAttendanceRecords: 2,
      excusedAttendanceRecords: 1,
      unconfirmedAttendanceCount: 2,
      approvedChangeRequests: 1,
      rejectedChangeRequests: 0,
      pendingChangeRequests: 3,
      cancelledChangeRequests: 0,
      acceptedSwapRequests: 1,
      declinedSwapRequests: 0,
      pendingSwapRequests: 2,
      cancelledSwapRequests: 0,
      expiredSwapRequests: 0,
      activeWaitlistEntries: 4,
      acceptedWaitlistEntries: 1,
      declinedWaitlistEntries: 0,
      expiredWaitlistEntries: 1,
      removedWaitlistEntries: 0,
      approvedSpecialLeaveRequests: 0,
      rejectedSpecialLeaveRequests: 0,
      pendingSpecialLeaveRequests: 1,
      cancelledSpecialLeaveRequests: 0,
      issueCount: 15,
    });
    mockGetSelfServiceConfig.mockResolvedValue({
      id: "config-1",
      groupId: "group-1",
      minShiftsPerCycle: 1,
      maxShiftsPerCycle: 5,
      requestWindowOpenOffsetHours: 168,
      requestWindowCloseOffsetHours: 24,
      cancellationCutoffHours: 12,
      maxLateCancellationsPerCycle: 2,
      lateCancellationWindowHours: 24,
      waitlistOfferMinutes: 45,
      cycleDurationDays: 7,
      allowMemberShiftClaims: true,
      allowWaitlist: true,
      allowShiftChangeRequests: false,
      allowAbsenceReports: true,
      allowShiftSwaps: true,
    });
    mockGetAdminWaitlistEntries.mockResolvedValue([
      {
        id: "waitlist-1",
        shiftSlotId: "slot-2",
        slotDate: "2026-06-21",
        slotStartTime: "10:00",
        slotEndTime: "12:00",
        taskName: "Front desk",
        position: 1,
        status: "Offered",
        offeredAt: new Date(Date.now() - 5 * 60 * 1000).toISOString(),
        expiresAt: new Date(Date.now() + 10 * 60 * 1000).toISOString(),
        personId: "person-1",
        personName: "Ofek",
      },
    ]);
    mockDownloadSelfServiceCycleCloseoutCsv.mockResolvedValue(undefined);
    mockDownloadSelfServiceCycleCloseoutPdf.mockResolvedValue(undefined);
  });

  it("shows action counts and navigates to the selected queue", async () => {
    const onNavigate = vi.fn();
    render(<SelfServiceOperationsTab spaceId="space-1" groupId="group-1" onNavigate={onNavigate} />);

    expect(await screen.findByText("15 item(s) need attention")).toBeInTheDocument();
    expect(screen.getByText("Cycle closeout")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Export CSV" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Export PDF" })).toBeEnabled();
    expect(screen.getByText("15 unresolved item(s)")).toBeInTheDocument();
    expect(screen.getByText("17/20")).toBeInTheDocument();
    expect(screen.getByText("12 present / 2 no-show / 2 unconfirmed")).toBeInTheDocument();
    expect(screen.getByText("2 slot(s) / 1 no-coverage / 1 under-filled")).toBeInTheDocument();
    expect(screen.getByText("3/5 workflows enabled")).toBeInTheDocument();
    expect(screen.getByText("6 pending review item(s)")).toBeInTheDocument();
    expect(screen.getByText("4 active waitlist item(s)")).toBeInTheDocument();
    expect(screen.getByText("2 pending swap proposal(s)")).toBeInTheDocument();
    expect(screen.getByText("3 under-filled slot(s)")).toBeInTheDocument();
    expect(screen.getByText("4 urgent signal(s)")).toBeInTheDocument();
    expect(screen.getByText("Late absence reports")).toBeInTheDocument();
    expect(screen.getByText("Expiring waitlist offers")).toBeInTheDocument();
    expect(screen.getByText("Pending peer swaps")).toBeInTheDocument();
    expect(screen.getByText("Under-filled slots")).toBeInTheDocument();
    expect(screen.getByText("Coverage gaps to fix first")).toBeInTheDocument();
    expect(screen.getByText("Showing the next 1 under-filled slot(s).")).toBeInTheDocument();
    expect(screen.getByText("Front desk")).toBeInTheDocument();
    expect(screen.getByText("1 open")).toBeInTheDocument();
    expect(screen.getByText("Review breakdown")).toBeInTheDocument();
    expect(screen.getByText("2 absence report(s) waiting.")).toBeInTheDocument();
    expect(screen.getByText("3 shift-change request(s) waiting.")).toBeInTheDocument();
    expect(screen.getByText("1 time-off request(s) waiting.")).toBeInTheDocument();
    expect(screen.getByText("How to run the cycle")).toBeInTheDocument();
    expect(screen.getByText("Active member policy")).toBeInTheDocument();
    expect(screen.getByText("1-5 per cycle")).toBeInTheDocument();
    expect(screen.getByText("2 inside 24h")).toBeInTheDocument();
    expect(screen.getByText("Cancel 12h / offer 45m")).toBeInTheDocument();
    expect(screen.getAllByText("Changes").length).toBeGreaterThan(0);
    expect(screen.getByText("Off")).toBeInTheDocument();
    expect(screen.getByText("Set policy and templates.")).toBeInTheDocument();
    expect(screen.getByText("Pick shifts and waitlist")).toBeInTheDocument();
    expect(screen.getByText("Changes and absence")).toBeInTheDocument();
    expect(screen.getAllByText("Special leave").length).toBeGreaterThan(1);
    expect(screen.getByText("Approved leave creates presence.")).toBeInTheDocument();
    expect(mockCycleControlPanel).toHaveBeenCalledWith(
      expect.objectContaining({ onStatusChanged: expect.any(Function) })
    );

    fireEvent.click(screen.getByRole("button", { name: /Review requests/i }));

    await waitFor(() => {
      expect(onNavigate).toHaveBeenCalledWith("absence-reports");
    });

    fireEvent.click(screen.getByRole("button", { name: /Edit policy/i }));

    await waitFor(() => {
      expect(onNavigate).toHaveBeenCalledWith("self-service-config");
    });

    fireEvent.click(screen.getByRole("button", { name: "Export CSV" }));

    await waitFor(() => {
      expect(mockDownloadSelfServiceCycleCloseoutCsv).toHaveBeenCalledWith("space-1", "group-1", "cycle-1");
    });

    fireEvent.click(screen.getByRole("button", { name: "Export PDF" }));

    await waitFor(() => {
      expect(mockDownloadSelfServiceCycleCloseoutPdf).toHaveBeenCalledWith("space-1", "group-1", "cycle-1");
    });

    fireEvent.click(screen.getByRole("button", { name: /Shift changes/i }));

    await waitFor(() => {
      expect(onNavigate).toHaveBeenCalledWith("absence-reports");
    });

    fireEvent.click(screen.getByRole("button", { name: /Expiring waitlist offers/i }));

    await waitFor(() => {
      expect(onNavigate).toHaveBeenCalledWith("waitlist");
    });

    fireEvent.click(screen.getByRole("button", { name: /2 pending swap proposal\(s\)/i }));

    await waitFor(() => {
      expect(onNavigate).toHaveBeenCalledWith("swaps");
    });

    fireEvent.click(screen.getByRole("button", { name: /Open manual overrides/i }));

    await waitFor(() => {
      expect(onNavigate).toHaveBeenCalledWith("admin-overrides");
    });

    fireEvent.click(screen.getByTestId("cycle-control-panel"));

    await waitFor(() => {
      expect(mockGetSelfServiceCycleStatus).toHaveBeenCalledTimes(2);
      expect(mockGetSelfServiceCycleCloseout).toHaveBeenCalledTimes(2);
      expect(mockGetSelfServiceConfig).toHaveBeenCalledTimes(2);
    });
  });
});
