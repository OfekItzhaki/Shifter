import { describe, expect, it } from "vitest";
import { getQueryKeysForUrl } from "@/lib/hooks/useCacheLifecycle";

describe("getQueryKeysForUrl", () => {
  it("maps existing space and group cached endpoints", () => {
    expect(getQueryKeysForUrl("https://app.test/spaces/space-1/groups")).toEqual([
      ["groups", "space-1"],
    ]);

    expect(getQueryKeysForUrl("https://app.test/spaces/space-1/groups/group-1/members")).toEqual([
      ["group-members", "space-1", "group-1"],
    ]);

    expect(getQueryKeysForUrl("https://app.test/spaces/space-1/billing/subscription")).toEqual([
      ["billing", "space-1"],
    ]);
  });

  it("maps self-service member cache updates to broad and specific keys", () => {
    expect(getQueryKeysForUrl("https://app.test/spaces/space-1/groups/group-1/shift-slots/available?cycleId=current")).toEqual([
      ["self-service", "space-1", "group-1"],
      ["self-service-slots", "space-1", "group-1"],
    ]);

    expect(getQueryKeysForUrl("https://app.test/spaces/space-1/groups/group-1/shift-requests/absence-reports/mine?cycleId=current")).toEqual([
      ["self-service", "space-1", "group-1"],
      ["self-service-absence-reports", "space-1", "group-1"],
    ]);

    expect(getQueryKeysForUrl("https://app.test/spaces/space-1/groups/group-1/waitlist/mine")).toEqual([
      ["self-service", "space-1", "group-1"],
      ["self-service-waitlist", "space-1", "group-1"],
    ]);
  });

  it("maps self-service admin cache updates without treating them as unknown", () => {
    expect(getQueryKeysForUrl("https://app.test/spaces/space-1/groups/group-1/shift-change-requests/admin?status=Pending")).toEqual([
      ["self-service", "space-1", "group-1"],
      ["self-service-shift-changes", "space-1", "group-1"],
    ]);

    expect(getQueryKeysForUrl("https://app.test/spaces/space-1/groups/group-1/shift-swaps/admin")).toEqual([
      ["self-service", "space-1", "group-1"],
      ["self-service-swaps", "space-1", "group-1"],
    ]);
  });

  it("returns null for unrelated URLs", () => {
    expect(getQueryKeysForUrl("https://app.test/spaces/space-1/unknown")).toBeNull();
    expect(getQueryKeysForUrl("not-a-url")).toBeNull();
  });
});
