import { describe, expect, it } from "vitest";
import {
  formatIsoDateForDateInput,
  getDateInputPattern,
  parseLocalizedDateInput,
} from "@/lib/utils/localizedDateInput";

describe("localized date input", () => {
  it("uses day-first dates for Israel", () => {
    expect(getDateInputPattern("IL", null, "he").placeholder).toBe("dd/mm/yyyy");
    expect(parseLocalizedDateInput("13/06/2026", "IL", null, "he")).toEqual({
      isoDate: "2026-06-13",
      isValid: true,
    });
  });

  it("uses month-first dates for the United States", () => {
    expect(getDateInputPattern("US", "CA", "en").placeholder).toBe("mm/dd/yyyy");
    expect(parseLocalizedDateInput("06/13/2026", "US", "CA", "en")).toEqual({
      isoDate: "2026-06-13",
      isValid: true,
    });
  });

  it("uses year-first dates where that is the local convention", () => {
    expect(getDateInputPattern("JP", null, "en").placeholder).toBe("yyyy-mm-dd");
    expect(parseLocalizedDateInput("2026-06-13", "JP", null, "en")).toEqual({
      isoDate: "2026-06-13",
      isValid: true,
    });
  });

  it("rejects impossible dates", () => {
    expect(parseLocalizedDateInput("31/02/2026", "IL", null, "he").isValid).toBe(false);
    expect(parseLocalizedDateInput("13/31/2026", "US", null, "en").isValid).toBe(false);
  });

  it("formats ISO dates back into the selected local input format", () => {
    expect(formatIsoDateForDateInput("2026-06-13", "IL", null, "he")).toBe("13/06/2026");
    expect(formatIsoDateForDateInput("2026-06-13", "US", "NY", "en")).toBe("06/13/2026");
  });
});
