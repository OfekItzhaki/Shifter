import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import SelfServiceOperationsTab from "../../components/groups/selfService/SelfServiceOperationsTab";

const mockGetSelfServiceCycleStatus = vi.fn();
const mockGetAdminWaitlistEntries = vi.fn();
const mockCycleControlPanel = vi.fn();

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string, values?: Record<string, unknown>) => {
    const translations: Record<string, string> = {
      title: "Self-service operations",
      description: "Run the current cycle and jump into queues.",
      statusLoading: "Checking queues...",
      activeSignals: `${values?.count ?? 0} item(s) need attention`,
      allClear: "No urgent items",
      "priority.title": "Priority signals",
      "priority.description": "Handle these first.",
      "priority.count": `${values?.count ?? 0} urgent signal(s)`,
      "priority.clear": "No urgent signals",
      "priority.empty": "Nothing urgent right now.",
      "priority.open": "Open queue",
      "priority.signals.lateReports.title": "Late absence reports",
      "priority.signals.lateReports.description": `${values?.count ?? 0} late report(s) need review.`,
      "priority.signals.expiringWaitlist.title": "Expiring waitlist offers",
      "priority.signals.expiringWaitlist.description": `${values?.count ?? 0} waitlist offer(s) expire in ${values?.minutes ?? 0} minutes.`,
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
  getAdminWaitlistEntries: (...args: unknown[]) => mockGetAdminWaitlistEntries(...args),
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
  });

  it("shows action counts and navigates to the selected queue", async () => {
    const onNavigate = vi.fn();
    render(<SelfServiceOperationsTab spaceId="space-1" groupId="group-1" onNavigate={onNavigate} />);

    expect(await screen.findByText("13 item(s) need attention")).toBeInTheDocument();
    expect(screen.getByText("6 pending review item(s)")).toBeInTheDocument();
    expect(screen.getByText("4 active waitlist item(s)")).toBeInTheDocument();
    expect(screen.getByText("3 under-filled slot(s)")).toBeInTheDocument();
    expect(screen.getByText("3 urgent signal(s)")).toBeInTheDocument();
    expect(screen.getByText("Late absence reports")).toBeInTheDocument();
    expect(screen.getByText("Expiring waitlist offers")).toBeInTheDocument();
    expect(screen.getByText("Under-filled slots")).toBeInTheDocument();
    expect(screen.getByText("Review breakdown")).toBeInTheDocument();
    expect(screen.getByText("2 absence report(s) waiting.")).toBeInTheDocument();
    expect(screen.getByText("3 shift-change request(s) waiting.")).toBeInTheDocument();
    expect(screen.getByText("1 time-off request(s) waiting.")).toBeInTheDocument();
    expect(screen.getByText("How to run the cycle")).toBeInTheDocument();
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

    fireEvent.click(screen.getByRole("button", { name: /Shift changes/i }));

    await waitFor(() => {
      expect(onNavigate).toHaveBeenCalledWith("absence-reports");
    });

    fireEvent.click(screen.getByRole("button", { name: /Expiring waitlist offers/i }));

    await waitFor(() => {
      expect(onNavigate).toHaveBeenCalledWith("waitlist");
    });

    fireEvent.click(screen.getByTestId("cycle-control-panel"));

    await waitFor(() => {
      expect(mockGetSelfServiceCycleStatus).toHaveBeenCalledTimes(2);
    });
  });
});
