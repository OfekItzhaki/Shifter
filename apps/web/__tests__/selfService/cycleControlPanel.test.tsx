import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import CycleControlPanel from "../../components/groups/selfService/CycleControlPanel";

const mockGetSelfServiceCycleStatus = vi.fn();
const mockGenerateNextSelfServiceCycle = vi.fn();
const mockOpenSelfServiceCycleWindow = vi.fn();
const mockCloseSelfServiceCycleWindow = vi.fn();
const mockCheckUnderScheduledMembers = vi.fn();

const translations: Record<string, string> = {
  title: "Cycle controls",
  description: "Run the manual cycle.",
  windowOpen: "Window open",
  windowClosed: "Window closed",
  yes: "Yes",
  no: "No",
  noCycle: "No active cycle",
  openSeatCount: "1 open seat",
  underfilledTitle: "Under-filled slots",
  underfilledCount: "1 under-filled item(s)",
  underScheduledNone: "No under-scheduled members",
  closeChecklist: "Close checklist",
  openReviewQueue: `Open ${""}`,
  "metrics.cycleStarts": "Cycle starts",
  "metrics.windowCloses": "Window closes",
  "metrics.slots": "Slots",
  "metrics.filled": "Filled",
  "metrics.coverage": "Coverage",
  "metrics.openSeats": "Open seats",
  "metrics.approved": "Approved",
  "metrics.pending": "Pending",
  "metrics.waitlist": "Waitlist",
  "metrics.absenceReview": "Absences",
  "metrics.lateReports": "Late reports",
  "metrics.changeReview": "Changes",
  "metrics.leaveReview": "Leave",
  "metrics.generated": "Generated",
  "closeChecklist.title": "Close checklist",
  "closeChecklist.description": "Check before closing.",
  "closeChecklist.warningCount": "3 warning(s)",
  "closeChecklist.ready": "Ready to close",
  "closeChecklist.needsCheck": "Needs under-scheduled check",
  "closeChecklist.open": "Open queue",
  "closeChecklist.items.coverage.label": "Coverage",
  "closeChecklist.items.coverage.warning": "1 slot(s) under-filled",
  "closeChecklist.items.coverage.ok": "Coverage is complete",
  "closeChecklist.items.reviews.label": "Reviews",
  "closeChecklist.items.reviews.warning": "3 pending review(s)",
  "closeChecklist.items.reviews.ok": "No pending reviews",
  "closeChecklist.items.waitlist.label": "Waitlist",
  "closeChecklist.items.waitlist.warning": "2 waitlist item(s)",
  "closeChecklist.items.waitlist.ok": "Waitlist is clear",
  "closeChecklist.items.underScheduled.label": "Under-scheduled members",
  "closeChecklist.items.underScheduled.unknown": "Run the check",
  "closeChecklist.items.underScheduled.warning": "1 member(s) under minimum",
  "closeChecklist.items.underScheduled.ok": "Everyone meets minimum",
  "buttons.open": "Open window",
  "buttons.opening": "Opening...",
  "buttons.close": "Close window",
  "buttons.closing": "Closing...",
  "buttons.checkUnder": "Check minimums",
  "buttons.checking": "Checking...",
  "buttons.generateNext": "Generate next cycle",
  "buttons.generateFirst": "Generate first cycle",
  "buttons.generating": "Generating...",
  "buttons.refresh": "Refresh",
  "buttons.refreshing": "Refreshing...",
};

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string, values?: Record<string, unknown>) => {
    if (key === "openReviewQueue") return `Open ${values?.queue ?? ""}`;
    if (key === "underScheduledCount") return `${values?.count ?? 0} under-scheduled member(s)`;
    return translations[key] ?? key;
  },
}));

vi.mock("../../lib/api/selfService", () => ({
  getSelfServiceCycleStatus: (...args: unknown[]) => mockGetSelfServiceCycleStatus(...args),
  generateNextSelfServiceCycle: (...args: unknown[]) => mockGenerateNextSelfServiceCycle(...args),
  openSelfServiceCycleWindow: (...args: unknown[]) => mockOpenSelfServiceCycleWindow(...args),
  closeSelfServiceCycleWindow: (...args: unknown[]) => mockCloseSelfServiceCycleWindow(...args),
  checkUnderScheduledMembers: (...args: unknown[]) => mockCheckUnderScheduledMembers(...args),
}));

function makeStatus(overrides: Record<string, unknown> = {}) {
  return {
    cycleId: "cycle-1",
    startsAt: "2026-06-20T00:00:00Z",
    endsAt: "2026-06-27T00:00:00Z",
    requestWindowOpensAt: "2026-06-13T00:00:00Z",
    requestWindowClosesAt: "2026-06-19T00:00:00Z",
    requestWindowOpen: true,
    isGenerated: true,
    slotCount: 4,
    totalCapacity: 8,
    filledCount: 6,
    approvedCount: 6,
    pendingCount: 1,
    waitlistCount: 2,
    pendingAbsenceReportCount: 1,
    latePendingAbsenceReportCount: 1,
    pendingShiftChangeRequestCount: 1,
    pendingSpecialLeaveRequestCount: 1,
    underfilledSlotCount: 1,
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
    ...overrides,
  };
}

describe("CycleControlPanel", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetSelfServiceCycleStatus.mockResolvedValue(makeStatus());
    mockOpenSelfServiceCycleWindow.mockResolvedValue(makeStatus({ requestWindowOpen: true }));
    mockCloseSelfServiceCycleWindow.mockResolvedValue(makeStatus({ requestWindowOpen: false }));
    mockGenerateNextSelfServiceCycle.mockResolvedValue(makeStatus());
    mockCheckUnderScheduledMembers.mockResolvedValue({
      underScheduledMembers: [
        {
          personId: "person-1",
          personName: "Member One",
          approvedCount: 0,
          minRequired: 1,
        },
      ],
    });
  });

  it("runs cycle window actions and surfaces close-readiness checks", async () => {
    const onNavigate = vi.fn();
    const onStatusChanged = vi.fn();
    render(
      <CycleControlPanel
        spaceId="space-1"
        groupId="group-1"
        onNavigate={onNavigate}
        onStatusChanged={onStatusChanged}
      />
    );

    expect(await screen.findByText("Window open")).toBeInTheDocument();
    expect(screen.getByText("6/8")).toBeInTheDocument();
    expect(screen.getByText("75%")).toBeInTheDocument();
    expect(screen.getByText("Under-filled slots")).toBeInTheDocument();
    expect(screen.getByText("3 warning(s)")).toBeInTheDocument();
    expect(screen.getByText("Run the check")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Open Waitlist" }));
    expect(onNavigate).toHaveBeenCalledWith("waitlist");

    fireEvent.click(screen.getByRole("button", { name: "Close window" }));
    await waitFor(() => {
      expect(mockCloseSelfServiceCycleWindow).toHaveBeenCalledWith("space-1", "group-1", "cycle-1");
    });
    await waitFor(() => {
      expect(onStatusChanged).toHaveBeenCalledTimes(1);
    });
    expect(await screen.findByText("Window closed")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Open window" }));
    await waitFor(() => {
      expect(mockOpenSelfServiceCycleWindow).toHaveBeenCalledWith("space-1", "group-1", "cycle-1", 24);
    });
    await waitFor(() => {
      expect(onStatusChanged).toHaveBeenCalledTimes(2);
    });

    fireEvent.click(screen.getByRole("button", { name: "Check minimums" }));
    await waitFor(() => {
      expect(mockCheckUnderScheduledMembers).toHaveBeenCalledWith("space-1", "group-1", "cycle-1");
    });
    await waitFor(() => {
      expect(onStatusChanged).toHaveBeenCalledTimes(3);
    });
    expect(await screen.findByText("1 under-scheduled member(s)")).toBeInTheDocument();
    expect(screen.getByText("Member One: 0/1")).toBeInTheDocument();
    expect(mockGetSelfServiceCycleStatus).toHaveBeenCalledTimes(2);
  });

  it("generates the first cycle when none exists", async () => {
    mockGetSelfServiceCycleStatus.mockResolvedValueOnce(makeStatus({
      cycleId: null,
      startsAt: null,
      endsAt: null,
      requestWindowOpensAt: null,
      requestWindowClosesAt: null,
      requestWindowOpen: false,
      isGenerated: false,
      slotCount: 0,
      totalCapacity: 0,
      filledCount: 0,
      approvedCount: 0,
      pendingCount: 0,
      waitlistCount: 0,
      pendingAbsenceReportCount: 0,
      latePendingAbsenceReportCount: 0,
      pendingShiftChangeRequestCount: 0,
      pendingSpecialLeaveRequestCount: 0,
      underfilledSlotCount: 0,
      underfilledSlots: [],
    }));

    const onStatusChanged = vi.fn();
    render(<CycleControlPanel spaceId="space-1" groupId="group-1" onStatusChanged={onStatusChanged} />);

    expect(await screen.findByText("No active cycle")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Generate first cycle" }));

    await waitFor(() => {
      expect(mockGenerateNextSelfServiceCycle).toHaveBeenCalledWith("space-1", "group-1");
    });
    await waitFor(() => {
      expect(onStatusChanged).toHaveBeenCalledTimes(1);
    });
    expect(await screen.findByText("Window open")).toBeInTheDocument();
  });
});
