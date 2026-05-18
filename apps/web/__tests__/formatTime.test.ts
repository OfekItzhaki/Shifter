/**
 * Unit tests for formatLocalTime utility.
 *
 * Feature: user-timezone-settings
 * Task: 6.2 Create formatLocalTime utility
 *
 * **Validates: Requirements 5.1, 5.2, 5.3, 5.4**
 */

import { describe, it, expect } from "vitest";
import {
  formatLocalTime,
  formatLocalDateTime,
  formatLocalDate,
  getLocalTimeParts,
  toUtcIsoString,
} from "../lib/utils/formatTime";

describe("formatLocalTime", () => {
  describe("basic formatting with timezone", () => {
    it("formats UTC time to Asia/Jerusalem (UTC+2 in winter, UTC+3 in summer)", () => {
      // January 15, 2024 at 10:00 UTC → 12:00 in Jerusalem (UTC+2 in winter)
      const result = formatLocalTime("2024-01-15T10:00:00Z", "Asia/Jerusalem", "24h");
      expect(result).toBe("12:00");
    });

    it("formats UTC time to America/New_York (UTC-5 in winter)", () => {
      // January 15, 2024 at 15:30 UTC → 10:30 in New York (UTC-5 in winter)
      const result = formatLocalTime("2024-01-15T15:30:00Z", "America/New_York", "24h");
      expect(result).toBe("10:30");
    });

    it("formats UTC time to Europe/London (UTC+0 in winter)", () => {
      // January 15, 2024 at 14:45 UTC → 14:45 in London (UTC+0 in winter)
      const result = formatLocalTime("2024-01-15T14:45:00Z", "Europe/London", "24h");
      expect(result).toBe("14:45");
    });
  });

  describe("DST handling (Requirement 5.3)", () => {
    it("handles DST transition for America/New_York (summer = UTC-4)", () => {
      // July 15, 2024 at 15:30 UTC → 11:30 in New York (UTC-4 in summer)
      const result = formatLocalTime("2024-07-15T15:30:00Z", "America/New_York", "24h");
      expect(result).toBe("11:30");
    });

    it("handles DST transition for Europe/London (summer = UTC+1)", () => {
      // July 15, 2024 at 14:45 UTC → 15:45 in London (UTC+1 in summer)
      const result = formatLocalTime("2024-07-15T14:45:00Z", "Europe/London", "24h");
      expect(result).toBe("15:45");
    });

    it("handles DST transition for Asia/Jerusalem (summer = UTC+3)", () => {
      // July 15, 2024 at 10:00 UTC → 13:00 in Jerusalem (UTC+3 in summer)
      const result = formatLocalTime("2024-07-15T10:00:00Z", "Asia/Jerusalem", "24h");
      expect(result).toBe("13:00");
    });
  });

  describe("12h format support (Requirement 5.2)", () => {
    it("formats time in 12h format with AM", () => {
      // January 15, 2024 at 08:30 UTC → 10:30 AM in Jerusalem (UTC+2)
      const result = formatLocalTime("2024-01-15T08:30:00Z", "Asia/Jerusalem", "12h");
      expect(result).toMatch(/10:30\s*AM/);
    });

    it("formats time in 12h format with PM", () => {
      // January 15, 2024 at 15:00 UTC → 5:00 PM in Jerusalem (UTC+2)
      const result = formatLocalTime("2024-01-15T15:00:00Z", "Asia/Jerusalem", "12h");
      expect(result).toMatch(/5:00\s*PM/);
    });

    it("formats midnight correctly in 12h format", () => {
      // January 15, 2024 at 22:00 UTC → 12:00 AM (midnight) in Jerusalem (UTC+2)
      const result = formatLocalTime("2024-01-15T22:00:00Z", "Asia/Jerusalem", "12h");
      expect(result).toMatch(/12:00\s*AM/);
    });
  });

  describe("default timezone fallback (Asia/Jerusalem)", () => {
    it("defaults to Asia/Jerusalem when timezoneId is null", () => {
      const withNull = formatLocalTime("2024-01-15T10:00:00Z", null, "24h");
      const withExplicit = formatLocalTime("2024-01-15T10:00:00Z", "Asia/Jerusalem", "24h");
      expect(withNull).toBe(withExplicit);
    });

    it("defaults to Asia/Jerusalem when timezoneId is undefined", () => {
      const withUndefined = formatLocalTime("2024-01-15T10:00:00Z", undefined, "24h");
      const withExplicit = formatLocalTime("2024-01-15T10:00:00Z", "Asia/Jerusalem", "24h");
      expect(withUndefined).toBe(withExplicit);
    });

    it("defaults to Asia/Jerusalem when timezoneId is empty string", () => {
      const withEmpty = formatLocalTime("2024-01-15T10:00:00Z", "", "24h");
      const withExplicit = formatLocalTime("2024-01-15T10:00:00Z", "Asia/Jerusalem", "24h");
      expect(withEmpty).toBe(withExplicit);
    });
  });

  describe("edge cases and error handling", () => {
    it("returns dash for null input", () => {
      expect(formatLocalTime(null, "Asia/Jerusalem", "24h")).toBe("—");
    });

    it("returns dash for undefined input", () => {
      expect(formatLocalTime(undefined, "Asia/Jerusalem", "24h")).toBe("—");
    });

    it("returns dash for empty string input", () => {
      expect(formatLocalTime("", "Asia/Jerusalem", "24h")).toBe("—");
    });

    it("returns dash for invalid date string", () => {
      expect(formatLocalTime("not-a-date", "Asia/Jerusalem", "24h")).toBe("—");
    });

    it("returns dash for invalid timezone", () => {
      expect(formatLocalTime("2024-01-15T10:00:00Z", "Invalid/Timezone", "24h")).toBe("—");
    });

    it("defaults format to 24h when not specified", () => {
      const result = formatLocalTime("2024-01-15T10:00:00Z", "Asia/Jerusalem");
      expect(result).toBe("12:00");
    });
  });
});

describe("formatLocalDateTime", () => {
  it("formats date and time with timezone", () => {
    const result = formatLocalDateTime("2024-01-15T10:00:00Z", "Asia/Jerusalem", "24h");
    // Should contain date and time parts
    expect(result).toContain("01");
    expect(result).toContain("15");
    expect(result).toContain("2024");
    expect(result).toContain("12:00");
  });

  it("returns dash for null input", () => {
    expect(formatLocalDateTime(null, "Asia/Jerusalem", "24h")).toBe("—");
  });

  it("returns dash for invalid date", () => {
    expect(formatLocalDateTime("invalid", "Asia/Jerusalem", "24h")).toBe("—");
  });
});

describe("formatLocalDate", () => {
  it("formats date only with timezone", () => {
    const result = formatLocalDate("2024-01-15T10:00:00Z", "Asia/Jerusalem");
    expect(result).toContain("01");
    expect(result).toContain("15");
    expect(result).toContain("2024");
  });

  it("handles date boundary crossing (UTC midnight → next day in positive offset)", () => {
    // January 14, 2024 at 23:00 UTC → January 15 in Jerusalem (UTC+2)
    const result = formatLocalDate("2024-01-14T23:00:00Z", "Asia/Jerusalem");
    expect(result).toContain("01/15/2024");
  });

  it("returns dash for null input", () => {
    expect(formatLocalDate(null, "Asia/Jerusalem")).toBe("—");
  });
});

describe("getLocalTimeParts", () => {
  it("returns correct hour and minute for timezone", () => {
    // January 15, 2024 at 10:30 UTC → 12:30 in Jerusalem (UTC+2)
    const parts = getLocalTimeParts("2024-01-15T10:30:00Z", "Asia/Jerusalem");
    expect(parts).toEqual({ hour: 12, minute: 30 });
  });

  it("returns null for null input", () => {
    expect(getLocalTimeParts(null, "Asia/Jerusalem")).toBeNull();
  });

  it("returns null for invalid date", () => {
    expect(getLocalTimeParts("invalid", "Asia/Jerusalem")).toBeNull();
  });

  it("handles midnight correctly (hour 0 or 24)", () => {
    // January 15, 2024 at 22:00 UTC → 00:00 (midnight) in Jerusalem (UTC+2)
    const parts = getLocalTimeParts("2024-01-15T22:00:00Z", "Asia/Jerusalem");
    // Intl may return 24 or 0 for midnight depending on implementation
    expect(parts?.minute).toBe(0);
    expect(parts?.hour === 0 || parts?.hour === 24).toBe(true);
  });
});

describe("toUtcIsoString (Requirement 5.4 — outgoing requests preserve UTC)", () => {
  it("converts Date to UTC ISO string", () => {
    const date = new Date("2024-01-15T10:30:00Z");
    expect(toUtcIsoString(date)).toBe("2024-01-15T10:30:00.000Z");
  });

  it("normalizes ISO string to UTC", () => {
    expect(toUtcIsoString("2024-01-15T10:30:00Z")).toBe("2024-01-15T10:30:00.000Z");
  });

  it("returns original string for invalid date", () => {
    expect(toUtcIsoString("not-a-date")).toBe("not-a-date");
  });

  it("preserves UTC value without applying any offset", () => {
    const utcInput = "2024-07-15T14:30:00.000Z";
    const result = toUtcIsoString(utcInput);
    expect(result).toBe(utcInput);
  });
});
