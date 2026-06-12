import { describe, expect, it } from "vitest";
import { isSentryEnabled } from "@/lib/monitoring/sentryConfig";

describe("Sentry config", () => {
  it("stays disabled in production when no DSN is configured", () => {
    expect(isSentryEnabled(undefined, "production")).toBe(false);
    expect(isSentryEnabled("", "production")).toBe(false);
    expect(isSentryEnabled("   ", "production")).toBe(false);
  });

  it("enables only when a DSN is configured in production", () => {
    expect(isSentryEnabled("https://public@sentry.example/1", "production")).toBe(true);
    expect(isSentryEnabled("https://public@sentry.example/1", "development")).toBe(false);
    expect(isSentryEnabled("https://public@sentry.example/1", "test")).toBe(false);
  });
});
