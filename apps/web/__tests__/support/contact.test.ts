import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import {
  DEFAULT_SUPPORT_EMAIL,
  buildSupportMailtoHref,
  getConfiguredSupportEmail,
} from "../../lib/support/contact";

describe("support contact helpers", () => {
  it("uses a trimmed configured support email", () => {
    expect(getConfiguredSupportEmail(" help@example.com ")).toBe("help@example.com");
  });

  it("falls back to the default support email when not configured", () => {
    expect(getConfiguredSupportEmail()).toBe(DEFAULT_SUPPORT_EMAIL);
    expect(getConfiguredSupportEmail("   ")).toBe(DEFAULT_SUPPORT_EMAIL);
  });

  it("builds mailto links with encoded subjects", () => {
    expect(buildSupportMailtoHref("Bug Report / Feedback", "help@example.com"))
      .toBe("mailto:help@example.com?subject=Bug%20Report%20%2F%20Feedback");
  });

  it("does not hardcode the hosted support mailbox in product support entry points", () => {
    const sourceFiles = [
      "app/LandingPage.tsx",
      "app/page.tsx",
      "app/profile/page.tsx",
      "app/accessibility/page.tsx",
      "components/shell/ShifterAssistant.tsx",
    ];

    for (const sourceFile of sourceFiles) {
      const contents = readFileSync(path.join(process.cwd(), sourceFile), "utf8");
      expect(contents).not.toContain("support@shifter.app");
    }
  });
});
