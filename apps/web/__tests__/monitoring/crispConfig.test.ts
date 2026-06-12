import { describe, expect, it } from "vitest";
import { getConfiguredCrispWebsiteId } from "@/lib/support/crispConfig";

describe("Crisp config", () => {
  it("returns null when no website ID is configured", () => {
    expect(getConfiguredCrispWebsiteId(undefined)).toBeNull();
    expect(getConfiguredCrispWebsiteId("")).toBeNull();
    expect(getConfiguredCrispWebsiteId("   ")).toBeNull();
  });

  it("returns a trimmed website ID when configured", () => {
    expect(getConfiguredCrispWebsiteId(" crisp-site-id ")).toBe("crisp-site-id");
  });
});
