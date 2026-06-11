import { describe, expect, it } from "vitest";
import type { AdminWaitlistEntryDto, WaitlistEntryDto } from "../../lib/api/selfService";
import { classifyWaitlistEntries, summarizeWaitlist } from "../../lib/utils/selfServiceWaitlist";

describe("self-service waitlist utilities", () => {
  it("keeps offered entries separate from the rest of a member's waitlist", () => {
    const entries = [
      makeWaitlistEntry({ id: "waiting", status: "Waiting" }),
      makeWaitlistEntry({ id: "offered", status: "Offered" }),
      makeWaitlistEntry({ id: "accepted", status: "Accepted" }),
      makeWaitlistEntry({ id: "expired", status: "Expired" }),
    ];

    const result = classifyWaitlistEntries(entries);

    expect(result.offeredEntries.map((entry) => entry.id)).toEqual(["offered"]);
    expect(result.otherEntries.map((entry) => entry.id)).toEqual([
      "waiting",
      "accepted",
      "expired",
    ]);
  });

  it("summarizes member and admin active waitlist pressure", () => {
    const memberEntries = [
      makeWaitlistEntry({ id: "offered-1", status: "Offered" }),
      makeWaitlistEntry({ id: "waiting-1", status: "Waiting" }),
      makeWaitlistEntry({ id: "removed-1", status: "Removed" }),
    ];
    const adminEntries = [
      makeAdminWaitlistEntry({ id: "admin-offered", status: "Offered" }),
      makeAdminWaitlistEntry({ id: "admin-waiting", status: "Waiting" }),
      makeAdminWaitlistEntry({ id: "admin-expired", status: "Expired" }),
      makeAdminWaitlistEntry({ id: "admin-accepted", status: "Accepted" }),
    ];

    expect(summarizeWaitlist(memberEntries, adminEntries)).toEqual({
      offeredCount: 1,
      waitingCount: 1,
      activeAdminCount: 2,
    });
  });
});

function makeWaitlistEntry(overrides: Partial<WaitlistEntryDto>): WaitlistEntryDto {
  return {
    id: "entry",
    shiftSlotId: "slot-1",
    slotDate: "2026-06-20",
    slotStartTime: "08:00:00",
    slotEndTime: "16:00:00",
    taskName: "Front desk",
    position: 1,
    status: "Waiting",
    offeredAt: null,
    expiresAt: null,
    ...overrides,
  };
}

function makeAdminWaitlistEntry(
  overrides: Partial<AdminWaitlistEntryDto>
): AdminWaitlistEntryDto {
  return {
    ...makeWaitlistEntry(overrides),
    personId: "person-1",
    personName: "Member One",
    ...overrides,
  };
}
