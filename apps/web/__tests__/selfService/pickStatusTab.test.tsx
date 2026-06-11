import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import PickStatusTab from "../../components/pick/PickStatusTab";

const mockGetSelfServiceConfig = vi.fn();
const mockGetMyShiftChangeRequests = vi.fn();
const mockGetMyShiftRequests = vi.fn();
const mockGetMyAbsenceReports = vi.fn();
const mockGetMySwaps = vi.fn();
const mockGetMyWaitlistEntries = vi.fn();
const mockGetMySpecialLeaveRequests = vi.fn();

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string, values?: Record<string, unknown>) => {
    const translations: Record<string, string> = {
      title: "My status",
      shiftProgress: `${values?.current ?? 0}/${values?.max ?? 0} shifts selected. Minimum: ${values?.min ?? 0}.`,
      belowMinimum: "Below minimum",
      onTrack: "On track",
      approved: "Approved",
      openSlots: "Open slots",
      pendingRequests: "Pending requests",
      activeWaitlist: "Waitlist",
      lateAbsences: "Late absences",
      pendingAbsences: "Absence reports",
      pendingLeave: "Time off",
      nextShift: "Next shift",
      noUpcomingShift: "No upcoming approved shift yet.",
      "priority.waitlistOffer.title": "Waitlist offer needs action",
      "priority.waitlistOffer.description": `You have ${values?.count ?? 0} waitlist offer(s) waiting.`,
      "priority.waitlistOffer.button": "Open waitlist",
      "priority.belowMinimum.title": "Pick more shifts",
      "priority.belowMinimum.description": `You have ${values?.current ?? 0}/${values?.min ?? 0} required shifts.`,
      "priority.belowMinimum.button": "Browse shifts",
      "priority.pendingRequests.title": "Requests are waiting",
      "priority.pendingRequests.description": `${values?.count ?? 0} request(s) are waiting.`,
      "priority.pendingRequests.button": "Open my shifts",
      "priority.pendingSwaps.title": "Swap request waiting",
      "priority.pendingSwaps.description": `${values?.count ?? 0} swap request(s) need attention.`,
      "priority.pendingSwaps.button": "Open swaps",
      "priority.allClear.title": "You are on track",
      "priority.allClear.description": "No urgent self-service actions right now.",
      "rules.title": "Rules",
      "rules.shiftRange": "Shift range",
      "rules.shiftRangeValue": `${values?.min ?? 0}-${values?.max ?? 0}`,
      "rules.requestWindow": "Request window",
      "rules.requestWindowValue": `${values?.open ?? 0}/${values?.close ?? 0}`,
      "rules.cancellation": "Cancellation cutoff",
      "rules.cancellationValue": `${values?.hours ?? 0}h`,
      "rules.lateAbsence": "Late absence limit",
      "rules.lateAbsenceValue": `${values?.used ?? 0}/${values?.max ?? 0}/${values?.hours ?? 0}`,
      "rules.waitlist": "Waitlist offers",
      "rules.waitlistValue": `${values?.minutes ?? 0}m`,
      "cards.pick.title": "Pick shifts",
      "cards.pick.description": "Browse open shifts.",
      "cards.pick.underMinimum": "Pick another shift.",
      "cards.pick.button": "Browse shifts",
      "cards.waitlist.title": "Waitlist",
      "cards.waitlist.description": "Track waitlist.",
      "cards.waitlist.offer": "You have a waitlist offer waiting for action.",
      "cards.waitlist.button": "Open waitlist",
      "cards.swaps.title": "Swaps",
      "cards.swaps.description": "Review swaps.",
      "cards.swaps.button": "Open swaps",
      "cards.requests.title": "Requests",
      "cards.requests.description": `${values?.changes ?? 0} changes / ${values?.absences ?? 0} absences`,
      "cards.requests.button": "Open my shifts",
      "cards.leave.title": "Time off",
      "cards.leave.description": "Track time off.",
      "cards.leave.button": "Open my shifts",
    };
    return translations[key] ?? key;
  },
}));

vi.mock("../../lib/api/selfService", () => ({
  getSelfServiceConfig: (...args: unknown[]) => mockGetSelfServiceConfig(...args),
  getMyShiftChangeRequests: (...args: unknown[]) => mockGetMyShiftChangeRequests(...args),
  getMyShiftRequests: (...args: unknown[]) => mockGetMyShiftRequests(...args),
  getMyAbsenceReports: (...args: unknown[]) => mockGetMyAbsenceReports(...args),
  getMySwaps: (...args: unknown[]) => mockGetMySwaps(...args),
  getMyWaitlistEntries: (...args: unknown[]) => mockGetMyWaitlistEntries(...args),
}));

vi.mock("../../lib/api/specialLeave", () => ({
  getMySpecialLeaveRequests: (...args: unknown[]) => mockGetMySpecialLeaveRequests(...args),
}));

describe("PickStatusTab", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetMyShiftRequests.mockResolvedValue({
      requests: [],
      currentShiftCount: 2,
      minShiftsPerCycle: 1,
      maxShiftsPerCycle: 4,
      cancellationCutoffHours: 24,
      maxLateReports: 2,
      lateCancellationWindowHours: 12,
    });
    mockGetMyWaitlistEntries.mockResolvedValue([
      {
        id: "waitlist-1",
        shiftSlotId: "slot-1",
        slotDate: "2026-06-20",
        slotStartTime: "09:00:00",
        slotEndTime: "17:00:00",
        taskName: "Front desk",
        position: 1,
        status: "Offered",
        offeredAt: "2026-06-11T08:00:00",
        expiresAt: "2026-06-11T09:00:00",
      },
    ]);
    mockGetMySwaps.mockResolvedValue([]);
    mockGetMyShiftChangeRequests.mockResolvedValue([]);
    mockGetMyAbsenceReports.mockResolvedValue({
      reports: [],
      lateReportsUsed: 0,
      maxLateReports: 2,
      schedulingCycleId: "cycle-1",
    });
    mockGetSelfServiceConfig.mockResolvedValue({
      id: "config-1",
      groupId: "group-1",
      minShiftsPerCycle: 1,
      maxShiftsPerCycle: 4,
      requestWindowOpenOffsetHours: 72,
      requestWindowCloseOffsetHours: 12,
      cancellationCutoffHours: 24,
      maxLateCancellationsPerCycle: 2,
      lateCancellationWindowHours: 12,
      waitlistOfferMinutes: 60,
      cycleDurationDays: 7,
    });
    mockGetMySpecialLeaveRequests.mockResolvedValue([]);
  });

  it("shows waitlist offers as the first priority and opens the waitlist tab", async () => {
    const onNavigate = vi.fn();

    render(<PickStatusTab spaceId="space-1" groupId="group-1" onNavigate={onNavigate} />);

    expect(await screen.findByText("Waitlist offer needs action")).toBeInTheDocument();
    expect(screen.getByText("You have 1 waitlist offer(s) waiting.")).toBeInTheDocument();

    fireEvent.click(screen.getAllByRole("button", { name: "Open waitlist" })[0]);

    await waitFor(() => {
      expect(onNavigate).toHaveBeenCalledWith("waitlist");
    });
  });
});
