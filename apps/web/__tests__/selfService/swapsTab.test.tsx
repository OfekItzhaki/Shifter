import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import SwapsTab from "../../app/groups/[groupId]/tabs/SwapsTab";
import type { SwapRequestDto } from "../../lib/api/selfService";
import type { GroupMemberDto } from "../../lib/api/groups";

const mockGetMySwaps = vi.fn();
const mockAcceptSwap = vi.fn();
const mockDeclineSwap = vi.fn();
const mockCancelSwap = vi.fn();
const mockProposeSwap = vi.fn();
const mockGetMyShiftRequests = vi.fn();
const mockGetMemberApprovedShifts = vi.fn();

const translations: Record<string, string> = {
  title: "Swaps",
  proposeButton: "Propose swap",
  incoming: "Incoming",
  outgoing: "Outgoing",
  history: "History",
  noSwaps: "No swaps",
  statusPending: "Pending",
  statusAccepted: "Accepted",
  statusDeclined: "Declined",
  statusCancelled: "Cancelled",
  statusExpired: "Expired",
  countdown: "Expires soon",
  offeredShift: "Offered shift",
  requestedShift: "Requested shift",
  acceptButton: "Accept",
  accepting: "Accepting...",
  declineButton: "Decline",
  declining: "Declining...",
  cancelButton: "Cancel",
  cancelling: "Cancelling...",
  selectYourShift: "Select your shift",
  selectTargetShift: "Select target shift",
  selectTargetMember: "Select target member",
  close: "Close",
  noApprovedShifts: "No approved shifts",
  noTargetMembers: "No target members",
  noTargetShifts: "No target shifts",
  proposing: "Proposing...",
  loading: "Loading",
  error: "Something went wrong",
};
const t = (key: string) => translations[key] ?? key;

vi.mock("next-intl", () => ({
  useTranslations: () => t,
}));

vi.mock("@/lib/store/authStore", () => ({
  useAuthStore: () => ({ userId: "user-current" }),
}));

vi.mock("@/lib/store/spaceStore", () => ({
  useSpaceStore: (selector?: (state: { currentSpaceId: string }) => unknown) => {
    const state = { currentSpaceId: "space-1" };
    return selector ? selector(state) : state;
  },
}));

vi.mock("@/lib/api/selfService", () => ({
  getMySwaps: (...args: unknown[]) => mockGetMySwaps(...args),
  acceptSwap: (...args: unknown[]) => mockAcceptSwap(...args),
  declineSwap: (...args: unknown[]) => mockDeclineSwap(...args),
  cancelSwap: (...args: unknown[]) => mockCancelSwap(...args),
  proposeSwap: (...args: unknown[]) => mockProposeSwap(...args),
  getMyShiftRequests: (...args: unknown[]) => mockGetMyShiftRequests(...args),
  getMemberApprovedShifts: (...args: unknown[]) => mockGetMemberApprovedShifts(...args),
}));

describe("SwapsTab", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetMySwaps.mockResolvedValue([
      makeSwap({
        id: "incoming-swap",
        initiatorPersonId: "person-other",
        targetPersonId: "person-current",
        initiatorPersonName: "Other Member",
        targetPersonName: "Current Member",
      }),
      makeSwap({
        id: "outgoing-swap",
        initiatorPersonId: "person-current",
        targetPersonId: "person-other",
        initiatorPersonName: "Current Member",
        targetPersonName: "Other Member",
      }),
    ]);
    mockAcceptSwap.mockResolvedValue({ swapRequestId: "incoming-swap" });
    mockDeclineSwap.mockResolvedValue(undefined);
    mockCancelSwap.mockResolvedValue(undefined);
    mockProposeSwap.mockResolvedValue({ swapRequestId: "new-swap" });
    mockGetMyShiftRequests.mockResolvedValue({ requests: [] });
    mockGetMemberApprovedShifts.mockResolvedValue([]);
  });

  it("lets the target member accept and decline incoming swaps", async () => {
    render(<SwapsTab spaceId="space-1" groupId="group-1" members={members} />);

    fireEvent.click(await screen.findByRole("button", { name: "Accept" }));

    await waitFor(() => {
      expect(mockAcceptSwap).toHaveBeenCalledWith("space-1", "group-1", "incoming-swap");
    });

    fireEvent.click(await screen.findByRole("button", { name: "Decline" }));

    await waitFor(() => {
      expect(mockDeclineSwap).toHaveBeenCalledWith("space-1", "group-1", "incoming-swap");
    });
    expect(mockGetMySwaps).toHaveBeenCalledTimes(3);
  });

  it("lets the initiator cancel outgoing swaps", async () => {
    render(<SwapsTab spaceId="space-1" groupId="group-1" members={members} />);

    fireEvent.click(await screen.findByRole("button", { name: "Cancel" }));

    await waitFor(() => {
      expect(mockCancelSwap).toHaveBeenCalledWith("space-1", "group-1", "outgoing-swap");
    });
    expect(mockGetMySwaps).toHaveBeenCalledTimes(2);
  });
});

const members: GroupMemberDto[] = [
  {
    personId: "person-current",
    fullName: "Current Member",
    displayName: "Current Member",
    isOwner: false,
    phoneNumber: null,
    email: null,
    invitationStatus: "accepted",
    profileImageUrl: null,
    birthday: null,
    linkedUserId: "user-current",
    roleId: null,
    roleName: null,
  },
  {
    personId: "person-other",
    fullName: "Other Member",
    displayName: "Other Member",
    isOwner: false,
    phoneNumber: null,
    email: null,
    invitationStatus: "accepted",
    profileImageUrl: null,
    birthday: null,
    linkedUserId: "user-other",
    roleId: null,
    roleName: null,
  },
];

function makeSwap(overrides: Partial<SwapRequestDto>): SwapRequestDto {
  return {
    id: "swap",
    initiatorPersonId: "initiator",
    targetPersonId: "target",
    initiatorPersonName: "Initiator",
    targetPersonName: "Target",
    initiatorShiftRequestId: "initiator-shift",
    targetShiftRequestId: "target-shift",
    initiatorSlotDate: "2026-06-20",
    initiatorSlotTime: "08:00:00",
    initiatorTaskName: "Front desk",
    targetSlotDate: "2026-06-21",
    targetSlotTime: "10:00:00",
    targetTaskName: "Kitchen",
    status: "Pending",
    expiresAt: "2026-06-12T12:00:00",
    createdAt: "2026-06-11T09:00:00",
    ...overrides,
  };
}
