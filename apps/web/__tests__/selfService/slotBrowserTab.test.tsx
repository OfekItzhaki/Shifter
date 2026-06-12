import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import SlotBrowserTab from "../../app/groups/[groupId]/tabs/SlotBrowserTab";

const mockGetAvailableSlots = vi.fn();
const mockSubmitShiftRequest = vi.fn();
const mockJoinWaitlist = vi.fn();

const translations: Record<string, string> = {
  "selfService.error": "Error loading data",
  "selfService.slotBrowser.dateFilter": "Date",
  "selfService.slotBrowser.allDates": "All dates",
  "selfService.slotBrowser.noSlots": "No available slots",
  "selfService.slotBrowser.highAvailability": "Open",
  "selfService.slotBrowser.nearlyFull": "Nearly full",
  "selfService.slotBrowser.full": "Full",
  "selfService.slotBrowser.requestButton": "Pick Shift",
  "selfService.slotBrowser.joinWaitlistButton": "Join Waitlist",
  "selfService.slotBrowser.specialDayNoCoverage": "No coverage required",
  "selfService.slotBrowser.specialDayUnavailable": "Not open for picking",
  "selfService.slotBrowser.claimDisabled": "Picking disabled",
  "selfService.slotBrowser.waitlistDisabled": "Waitlist disabled",
  "selfService.slotBrowser.requestSuccess": "Shift confirmed. It was added to your shifts.",
  "selfService.slotBrowser.windowClosed": "Request window closed",
};
const translationFns = new Map<string, (key: string, values?: Record<string, unknown>) => string>();

vi.mock("next-intl", () => ({
  useTranslations: (namespace: string) => {
    if (!translationFns.has(namespace)) {
      translationFns.set(namespace, (key: string, values?: Record<string, unknown>) => {
        if (`${namespace}.${key}` === "selfService.slotBrowser.waitlistSuccess") {
          return `Joined waitlist at position ${values?.position ?? 0}`;
        }
        if (`${namespace}.${key}` === "selfService.slotBrowser.windowOpensAt") {
          return `Opens ${values?.date ?? ""}`;
        }
        if (`${namespace}.${key}` === "selfService.slotBrowser.specialDayNamed") {
          return `Special day: ${values?.name ?? ""}`;
        }
        return translations[`${namespace}.${key}`] ?? key;
      });
    }

    return translationFns.get(namespace)!;
  },
}));

vi.mock("@/lib/api/selfService", () => ({
  getAvailableSlots: (...args: unknown[]) => mockGetAvailableSlots(...args),
  submitShiftRequest: (...args: unknown[]) => mockSubmitShiftRequest(...args),
  joinWaitlist: (...args: unknown[]) => mockJoinWaitlist(...args),
}));

describe("SlotBrowserTab", () => {
  function makeAvailableResponse(currentFillCount = 0) {
    return {
      requestWindowOpen: true,
      requestWindowOpensAt: null,
      requestWindowClosesAt: "2026-06-19T00:00:00Z",
      slots: [
        {
          id: "slot-1",
          date: "2026-06-20",
          startTime: "09:00:00",
          endTime: "17:00:00",
          taskName: "Desk",
          capacity: 2,
          currentFillCount,
        },
      ],
    };
  }

  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAvailableSlots.mockResolvedValue(makeAvailableResponse());
    mockSubmitShiftRequest.mockResolvedValue({ shiftRequestId: "request-1" });
    mockJoinWaitlist.mockResolvedValue({ position: 1 });
  });

  it("confirms an available shift immediately after the member picks it", async () => {
    mockGetAvailableSlots
      .mockResolvedValueOnce(makeAvailableResponse(0))
      .mockResolvedValueOnce(makeAvailableResponse(1));

    render(<SlotBrowserTab spaceId="space-1" groupId="group-1" isAdmin={false} />);

    fireEvent.click(await screen.findByRole("button", { name: "Pick Shift" }));

    await waitFor(() => {
      expect(mockSubmitShiftRequest).toHaveBeenCalledWith("space-1", "group-1", "slot-1");
    });
    await waitFor(() => {
      expect(mockGetAvailableSlots).toHaveBeenCalledTimes(2);
    });
    expect(await screen.findByText("Shift confirmed. It was added to your shifts.")).toBeInTheDocument();
    expect(screen.getByText("1/2")).toBeInTheDocument();
  });

  it("refreshes capacity after a stale pick rejection", async () => {
    mockGetAvailableSlots
      .mockResolvedValueOnce(makeAvailableResponse(0))
      .mockResolvedValueOnce(makeAvailableResponse(2));
    mockSubmitShiftRequest.mockRejectedValue({
      response: {
        status: 422,
        data: {
          status: 422,
          type: "https://api.shifter.co.il/errors/shift-request-rejected",
          detail: "The slot is at full capacity.",
        },
      },
    });

    render(<SlotBrowserTab spaceId="space-1" groupId="group-1" isAdmin={false} />);

    fireEvent.click(await screen.findByRole("button", { name: "Pick Shift" }));

    expect(await screen.findByText("The slot is at full capacity.")).toBeInTheDocument();
    await waitFor(() => {
      expect(mockGetAvailableSlots).toHaveBeenCalledTimes(2);
    });
    expect(screen.getByText("2/2")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Join Waitlist" })).toBeInTheDocument();
  });

  it("joins the waitlist when a shift is already full", async () => {
    mockGetAvailableSlots.mockResolvedValue({
      requestWindowOpen: true,
      requestWindowOpensAt: null,
      requestWindowClosesAt: "2026-06-19T00:00:00Z",
      slots: [
        {
          id: "slot-full",
          date: "2026-06-20",
          startTime: "09:00:00",
          endTime: "17:00:00",
          taskName: "Desk",
          capacity: 1,
          currentFillCount: 1,
        },
      ],
    });
    mockJoinWaitlist.mockResolvedValue({ position: 3 });

    render(<SlotBrowserTab spaceId="space-1" groupId="group-1" isAdmin={false} />);

    fireEvent.click(await screen.findByRole("button", { name: "Join Waitlist" }));

    await waitFor(() => {
      expect(mockJoinWaitlist).toHaveBeenCalledWith("space-1", "group-1", "slot-full");
    });
    expect(await screen.findByText("Joined waitlist at position 3")).toBeInTheDocument();
    expect(mockSubmitShiftRequest).not.toHaveBeenCalled();
  });

  it("shows special-day labels on matching slots", async () => {
    mockGetAvailableSlots.mockResolvedValue({
      ...makeAvailableResponse(0),
      slots: [
        {
          id: "slot-holiday",
          date: "2026-06-20",
          startTime: "09:00:00",
          endTime: "17:00:00",
          taskName: "Desk",
          capacity: 2,
          currentFillCount: 0,
          isSpecialDay: true,
          specialDayName: "Festival",
          specialDayKind: "Holiday",
        },
      ],
    });

    render(<SlotBrowserTab spaceId="space-1" groupId="group-1" isAdmin={false} />);

    expect(await screen.findByText("Special day: Festival")).toBeInTheDocument();
  });

  it("disables member actions when a special day does not require coverage", async () => {
    mockGetAvailableSlots.mockResolvedValue({
      ...makeAvailableResponse(0),
      slots: [
        {
          id: "slot-closed",
          date: "2026-06-20",
          startTime: "09:00:00",
          endTime: "17:00:00",
          taskName: "Desk",
          capacity: 2,
          currentFillCount: 0,
          isSpecialDay: true,
          specialDayName: "Closure",
          specialDayKind: "Custom",
          specialDayRequiresCoverage: false,
        },
      ],
    });

    render(<SlotBrowserTab spaceId="space-1" groupId="group-1" isAdmin={false} />);

    expect(await screen.findByText("Special day: Closure")).toBeInTheDocument();
    expect(screen.getByText("No coverage required")).toBeInTheDocument();
    expect(screen.getByText("Not open for picking")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Pick Shift" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Join Waitlist" })).not.toBeInTheDocument();
  });

  it("hides pick and waitlist actions when member workflows are disabled", async () => {
    mockGetAvailableSlots.mockResolvedValue({
      requestWindowOpen: true,
      requestWindowOpensAt: null,
      requestWindowClosesAt: "2026-06-19T00:00:00Z",
      allowMemberShiftClaims: false,
      allowWaitlist: false,
      slots: [
        {
          id: "slot-open",
          date: "2026-06-20",
          startTime: "09:00:00",
          endTime: "17:00:00",
          taskName: "Desk",
          capacity: 2,
          currentFillCount: 0,
        },
        {
          id: "slot-full",
          date: "2026-06-20",
          startTime: "18:00:00",
          endTime: "22:00:00",
          taskName: "Desk",
          capacity: 1,
          currentFillCount: 1,
        },
      ],
    });

    render(<SlotBrowserTab spaceId="space-1" groupId="group-1" isAdmin={false} />);

    expect(await screen.findByText("Picking disabled")).toBeInTheDocument();
    expect(screen.getByText("Waitlist disabled")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Pick Shift" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Join Waitlist" })).not.toBeInTheDocument();
    expect(mockSubmitShiftRequest).not.toHaveBeenCalled();
    expect(mockJoinWaitlist).not.toHaveBeenCalled();
  });
});
