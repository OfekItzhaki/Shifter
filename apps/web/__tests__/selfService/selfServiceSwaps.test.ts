import { describe, expect, it } from "vitest";
import type { SwapRequestDto } from "../../lib/api/selfService";
import { classifySwapsForPerson } from "../../lib/utils/selfServiceSwaps";

describe("classifySwapsForPerson", () => {
  it("separates pending incoming, pending outgoing, and completed swaps", () => {
    const swaps = [
      makeSwap({
        id: "incoming",
        initiatorPersonId: "person-other",
        targetPersonId: "person-current",
        status: "Pending",
      }),
      makeSwap({
        id: "outgoing",
        initiatorPersonId: "person-current",
        targetPersonId: "person-other",
        status: "Pending",
      }),
      makeSwap({
        id: "accepted",
        initiatorPersonId: "person-current",
        targetPersonId: "person-other",
        status: "Accepted",
      }),
      makeSwap({
        id: "declined-for-other-member",
        initiatorPersonId: "person-a",
        targetPersonId: "person-b",
        status: "Declined",
      }),
    ];

    const result = classifySwapsForPerson(swaps, "person-current");

    expect(result.incomingSwaps.map((swap) => swap.id)).toEqual(["incoming"]);
    expect(result.outgoingSwaps.map((swap) => swap.id)).toEqual(["outgoing"]);
    expect(result.completedSwaps.map((swap) => swap.id)).toEqual([
      "accepted",
      "declined-for-other-member",
    ]);
  });

  it("does not show pending swaps as actionable when no current person is linked", () => {
    const result = classifySwapsForPerson([
      makeSwap({ id: "incoming", targetPersonId: "person-current", status: "Pending" }),
      makeSwap({ id: "outgoing", initiatorPersonId: "person-current", status: "Pending" }),
      makeSwap({ id: "expired", targetPersonId: "person-current", status: "Expired" }),
    ], null);

    expect(result.incomingSwaps).toEqual([]);
    expect(result.outgoingSwaps).toEqual([]);
    expect(result.completedSwaps.map((swap) => swap.id)).toEqual(["expired"]);
  });
});

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
