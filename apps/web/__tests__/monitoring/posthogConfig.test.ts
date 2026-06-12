import { describe, expect, it } from "vitest";
import { isPostHogEnabled } from "@/lib/analytics/posthogConfig";

describe("PostHog config", () => {
  it("stays disabled in production when no key is configured", () => {
    expect(isPostHogEnabled(undefined, "production")).toBe(false);
    expect(isPostHogEnabled("", "production")).toBe(false);
    expect(isPostHogEnabled("   ", "production")).toBe(false);
  });

  it("enables only when a key is configured in production", () => {
    expect(isPostHogEnabled("phc_test", "production")).toBe(true);
    expect(isPostHogEnabled("phc_test", "development")).toBe(false);
    expect(isPostHogEnabled("phc_test", "test")).toBe(false);
  });
});
