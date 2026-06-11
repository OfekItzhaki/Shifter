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
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAvailableSlots.mockResolvedValue({
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
          currentFillCount: 0,
        },
      ],
    });
    mockSubmitShiftRequest.mockResolvedValue({ shiftRequestId: "request-1" });
    mockJoinWaitlist.mockResolvedValue({ position: 1 });
  });

  it("confirms an available shift immediately after the member picks it", async () => {
    render(<SlotBrowserTab spaceId="space-1" groupId="group-1" isAdmin={false} />);

    fireEvent.click(await screen.findByRole("button", { name: "Pick Shift" }));

    await waitFor(() => {
      expect(mockSubmitShiftRequest).toHaveBeenCalledWith("space-1", "group-1", "slot-1");
    });
    expect(await screen.findByText("Shift confirmed. It was added to your shifts.")).toBeInTheDocument();
    expect(screen.getByText("1/2")).toBeInTheDocument();
  });
});
