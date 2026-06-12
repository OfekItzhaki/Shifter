import { describe, expect, it } from "vitest";
import { buildRefreshUrls } from "@/lib/cache/backgroundRefresh";

describe("buildRefreshUrls", () => {
  it("includes space-level cached endpoints", () => {
    const urls = buildRefreshUrls("space-1");

    expect(urls).toContain("/spaces/space-1/groups");
    expect(urls).toContain("/spaces/space-1/schedule-versions");
    expect(urls).toContain("/spaces/space-1/billing/subscription");
  });

  it("includes member-safe self-service endpoints for unique groups", () => {
    const urls = buildRefreshUrls("space-1", ["group-1", "group-1", "group 2"]);

    expect(urls).toContain("/spaces/space-1/groups/group-1/self-service-cycles/status");
    expect(urls).toContain("/spaces/space-1/groups/group-1/shift-slots/available?cycleId=current");
    expect(urls).toContain("/spaces/space-1/groups/group-1/shift-requests/mine?schedulingCycleId=current");
    expect(urls).toContain("/spaces/space-1/groups/group-1/shift-requests/absence-reports/mine?cycleId=current");
    expect(urls).toContain("/spaces/space-1/groups/group-1/shift-change-requests/mine");
    expect(urls).toContain("/spaces/space-1/groups/group-1/waitlist/mine");
    expect(urls).toContain("/spaces/space-1/groups/group-1/shift-swaps/my");
    expect(urls).toContain("/spaces/space-1/groups/group%202/self-service-config");

    expect(urls.filter((url) => url.includes("/groups/group-1/self-service-config"))).toHaveLength(1);
    expect(urls.some((url) => url.includes("/admin"))).toBe(false);
    expect(urls.some((url) => url.includes("/closeout"))).toBe(false);
  });
});
