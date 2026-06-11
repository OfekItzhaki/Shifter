import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import WaitlistTab from "../../app/groups/[groupId]/tabs/WaitlistTab";

const mockGetMyWaitlistEntries = vi.fn();
const mockGetAdminWaitlistEntries = vi.fn();
const mockAcceptWaitlistOffer = vi.fn();
const mockLeaveWaitlist = vi.fn();
const mockAdminAssignMember = vi.fn();

const translations: Record<string, string> = {
  title: "Waitlist",
  statusWaiting: "Waiting",
  statusOffered: "Offered",
  statusAccepted: "Accepted",
  statusExpired: "Expired",
  statusDeclined: "Declined",
  statusRemoved: "Removed",
  position: `Position: 1`,
  offerExpires: "Offer expires soon",
  acceptButton: "Accept Offer",
  accepting: "Accepting...",
  declineButton: "Decline",
  declining: "Declining...",
  leaveButton: "Leave",
  leaving: "Leaving...",
  leaveConfirmTitle: "Leave Waitlist",
  leaveConfirmMessage: "Are you sure?",
  leaveConfirmYes: "Yes, leave",
  leaveConfirmNo: "Cancel",
  noEntries: "No waitlist entries",
  adminTitle: "Active waitlist queue",
  adminDescription: "Members currently waiting or holding an offer for this group.",
  adminCount: "1 active",
  adminAssignButton: "Assign now",
  adminAssigning: "Assigning...",
  acceptSuccess: "Waitlist offer accepted. The shift was added to your shifts.",
  declineSuccess: "Offer declined and passed to the next person.",
  leaveSuccess: "You left the waitlist.",
  adminAssignSuccess: "Waitlist member assigned to the shift.",
};
const t = (key: string) => translations[key] ?? key;

vi.mock("next-intl", () => ({
  useLocale: () => "en",
  useTranslations: () => t,
}));

vi.mock("@/lib/api/selfService", () => ({
  getMyWaitlistEntries: (...args: unknown[]) => mockGetMyWaitlistEntries(...args),
  getAdminWaitlistEntries: (...args: unknown[]) => mockGetAdminWaitlistEntries(...args),
  acceptWaitlistOffer: (...args: unknown[]) => mockAcceptWaitlistOffer(...args),
  leaveWaitlist: (...args: unknown[]) => mockLeaveWaitlist(...args),
  adminAssignMember: (...args: unknown[]) => mockAdminAssignMember(...args),
}));

describe("WaitlistTab", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAdminWaitlistEntries.mockResolvedValue([]);
    mockGetMyWaitlistEntries.mockResolvedValue([
      {
        id: "waitlist-1",
        shiftSlotId: "slot-1",
        slotDate: "2026-06-20",
        slotStartTime: "08:00:00",
        slotEndTime: "16:00:00",
        taskName: "Desk",
        position: 1,
        status: "Offered",
        offeredAt: "2026-06-10T08:00:00Z",
        expiresAt: "2026-06-20T08:00:00Z",
      },
    ]);
    mockAcceptWaitlistOffer.mockResolvedValue(undefined);
    mockAdminAssignMember.mockResolvedValue(undefined);
  });

  it("shows confirmation after accepting a waitlist offer", async () => {
    render(<WaitlistTab spaceId="space-1" groupId="group-1" isAdmin={false} />);

    fireEvent.click(await screen.findByRole("button", { name: "Accept Offer" }));

    await waitFor(() => {
      expect(mockAcceptWaitlistOffer).toHaveBeenCalledWith("space-1", "group-1", "slot-1");
    });
    expect(await screen.findByText("Waitlist offer accepted. The shift was added to your shifts.")).toBeInTheDocument();
  });

  it("refreshes entries after a stale waitlist offer fails to accept", async () => {
    mockAcceptWaitlistOffer.mockRejectedValue({
      response: {
        status: 400,
        data: { error: "The shift slot is no longer available." },
      },
    });
    mockGetMyWaitlistEntries
      .mockResolvedValueOnce([
        {
          id: "waitlist-1",
          shiftSlotId: "slot-1",
          slotDate: "2026-06-20",
          slotStartTime: "08:00:00",
          slotEndTime: "16:00:00",
          taskName: "Desk",
          position: 1,
          status: "Offered",
          offeredAt: "2026-06-10T08:00:00Z",
          expiresAt: "2026-06-20T08:00:00Z",
        },
      ])
      .mockResolvedValueOnce([]);

    render(<WaitlistTab spaceId="space-1" groupId="group-1" isAdmin={false} />);

    fireEvent.click(await screen.findByRole("button", { name: "Accept Offer" }));

    expect(await screen.findByText("The shift slot is no longer available.")).toBeInTheDocument();
    await waitFor(() => {
      expect(mockGetMyWaitlistEntries).toHaveBeenCalledTimes(2);
    });
    expect(await screen.findByText("No waitlist entries")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Accept Offer" })).not.toBeInTheDocument();
  });

  it("shows confirmation after an admin assigns a waitlisted member", async () => {
    mockGetMyWaitlistEntries.mockResolvedValue([]);
    mockGetAdminWaitlistEntries.mockResolvedValue([
      {
        id: "admin-waitlist-1",
        shiftSlotId: "slot-2",
        personId: "person-2",
        personName: "Member Two",
        slotDate: "2026-06-21",
        slotStartTime: "10:00:00",
        slotEndTime: "18:00:00",
        taskName: "Gate",
        position: 1,
        status: "Waiting",
        offeredAt: null,
        expiresAt: null,
      },
    ]);

    render(<WaitlistTab spaceId="space-1" groupId="group-1" isAdmin />);

    fireEvent.click(await screen.findByRole("button", { name: "Assign now" }));

    await waitFor(() => {
      expect(mockAdminAssignMember).toHaveBeenCalledWith("space-1", "group-1", "slot-2", "person-2");
    });
    expect(await screen.findByText("Waitlist member assigned to the shift.")).toBeInTheDocument();
  });
});
