/**
 * Property-based tests for formatLocalTime utility.
 *
 * Feature: user-timezone-settings
 * Task: 6.3 Write property tests for formatLocalTime
 *
 * **Validates: Requirements 5.1, 5.3, 5.4**
 */

import { describe, it, expect } from "vitest";
import * as fc from "fast-check";
import {
  formatLocalTime,
  getLocalTimeParts,
  toUtcIsoString,
} from "../lib/utils/formatTime";

/**
 * A curated list of valid IANA timezone identifiers covering diverse UTC offsets,
 * DST rules, and geographic regions. Using a representative subset avoids
 * hitting exotic/deprecated timezone IDs that may not be supported in all
 * Intl implementations.
 */
const VALID_TIMEZONES = [
  "UTC",
  "Asia/Jerusalem",
  "America/New_York",
  "America/Chicago",
  "America/Denver",
  "America/Los_Angeles",
  "America/Anchorage",
  "Pacific/Honolulu",
  "Europe/London",
  "Europe/Paris",
  "Europe/Berlin",
  "Europe/Moscow",
  "Asia/Tokyo",
  "Asia/Shanghai",
  "Asia/Kolkata",
  "Asia/Dubai",
  "Australia/Sydney",
  "Australia/Perth",
  "Pacific/Auckland",
  "America/Sao_Paulo",
  "Africa/Cairo",
  "Africa/Johannesburg",
  "America/Argentina/Buenos_Aires",
  "Asia/Singapore",
  "Asia/Bangkok",
] as const;

/**
 * Arbitrary that generates a valid IANA timezone ID from our curated list.
 */
const timezoneArb = fc.constantFrom(...VALID_TIMEZONES);

/**
 * Arbitrary that generates a random UTC ISO datetime string.
 * Range: 2000-01-01 to 2030-12-31 to cover various DST transitions.
 */
const utcDatetimeArb = fc
  .date({
    min: new Date("2000-01-01T00:00:00Z"),
    max: new Date("2030-12-31T23:59:59Z"),
  })
  .map((d) => d.toISOString());

const PROPERTY_TEST_TIMEOUT_MS = 15_000;

describe("Property 7: DST-Aware Time Display", () => {
  it("formatted output reflects correct local time for any UTC datetime and timezone", () => {
    fc.assert(
      fc.property(utcDatetimeArb, timezoneArb, (utcIso, tz) => {
        // Format using our utility
        const formatted24h = formatLocalTime(utcIso, tz, "24h");

        // The result should never be the error dash for valid inputs
        expect(formatted24h).not.toBe("—");

        // Independently compute expected local time using Intl.DateTimeFormat
        const date = new Date(utcIso);
        const expectedFormatter = new Intl.DateTimeFormat("en-US", {
          timeZone: tz,
          hour: "2-digit",
          minute: "2-digit",
          hour12: false,
        });
        const expectedTime = expectedFormatter.format(date);

        // Both should produce the same result
        expect(formatted24h).toBe(expectedTime);
      }),
      { numRuns: 200 }
    );
  }, PROPERTY_TEST_TIMEOUT_MS);

  it("getLocalTimeParts matches Intl.DateTimeFormat parts for any UTC datetime and timezone", () => {
    fc.assert(
      fc.property(utcDatetimeArb, timezoneArb, (utcIso, tz) => {
        const parts = getLocalTimeParts(utcIso, tz);

        // Should never return null for valid inputs
        expect(parts).not.toBeNull();

        // Independently compute expected parts
        const date = new Date(utcIso);
        const formatter = new Intl.DateTimeFormat("en-US", {
          timeZone: tz,
          hour: "numeric",
          minute: "numeric",
          hour12: false,
        });
        const intlParts = formatter.formatToParts(date);
        const expectedHour = parseInt(
          intlParts.find((p) => p.type === "hour")?.value ?? "0",
          10
        );
        const expectedMinute = parseInt(
          intlParts.find((p) => p.type === "minute")?.value ?? "0",
          10
        );

        expect(parts!.hour).toBe(expectedHour);
        expect(parts!.minute).toBe(expectedMinute);
      }),
      { numRuns: 200 }
    );
  }, PROPERTY_TEST_TIMEOUT_MS);

  it("12h format produces valid AM/PM output for any UTC datetime and timezone", () => {
    fc.assert(
      fc.property(utcDatetimeArb, timezoneArb, (utcIso, tz) => {
        const formatted12h = formatLocalTime(utcIso, tz, "12h");

        // Should never be the error dash for valid inputs
        expect(formatted12h).not.toBe("—");

        // Must contain AM or PM
        expect(formatted12h).toMatch(/AM|PM/);

        // Independently verify using Intl
        const date = new Date(utcIso);
        const expectedFormatter = new Intl.DateTimeFormat("en-US", {
          timeZone: tz,
          hour: "2-digit",
          minute: "2-digit",
          hour12: true,
        });
        const expectedTime = expectedFormatter.format(date);

        expect(formatted12h).toBe(expectedTime);
      }),
      { numRuns: 200 }
    );
  }, PROPERTY_TEST_TIMEOUT_MS);
});

describe("Property 8: Outgoing Requests Preserve UTC", () => {
  it("toUtcIsoString always returns a valid UTC ISO string ending with Z for any Date", () => {
    fc.assert(
      fc.property(
        fc.date({
          min: new Date("2000-01-01T00:00:00Z"),
          max: new Date("2030-12-31T23:59:59Z"),
        }),
        (date) => {
          const result = toUtcIsoString(date);

          // Must end with Z (UTC indicator)
          expect(result).toMatch(/Z$/);

          // Must be a valid ISO string that parses back to the same timestamp
          const parsed = new Date(result);
          expect(parsed.getTime()).toBe(date.getTime());
        }
      ),
      { numRuns: 200 }
    );
  }, PROPERTY_TEST_TIMEOUT_MS);

  it("toUtcIsoString preserves the original UTC value for any valid ISO string input", () => {
    fc.assert(
      fc.property(utcDatetimeArb, (utcIso) => {
        const result = toUtcIsoString(utcIso);

        // Must end with Z
        expect(result).toMatch(/Z$/);

        // The timestamp must be preserved exactly
        const originalMs = new Date(utcIso).getTime();
        const resultMs = new Date(result).getTime();
        expect(resultMs).toBe(originalMs);
      }),
      { numRuns: 200 }
    );
  }, PROPERTY_TEST_TIMEOUT_MS);

  it("toUtcIsoString never applies a timezone offset — round-trip is identity on UTC values", () => {
    fc.assert(
      fc.property(utcDatetimeArb, timezoneArb, (utcIso, _tz) => {
        // Even though we have a timezone in scope, toUtcIsoString should
        // never be influenced by any timezone — it always returns UTC
        const result = toUtcIsoString(utcIso);

        // Parse both and compare milliseconds
        const originalDate = new Date(utcIso);
        const resultDate = new Date(result);

        expect(resultDate.getTime()).toBe(originalDate.getTime());

        // The ISO string should always be in UTC format (ends with Z, no offset)
        expect(result).not.toMatch(/[+-]\d{2}:\d{2}$/);
        expect(result).toMatch(/Z$/);
      }),
      { numRuns: 200 }
    );
  }, PROPERTY_TEST_TIMEOUT_MS);

  it("displayed local time does not corrupt the original UTC value when sent back", () => {
    fc.assert(
      fc.property(utcDatetimeArb, timezoneArb, (utcIso, tz) => {
        // Simulate the full flow:
        // 1. Display time in local timezone (what the user sees)
        const _displayedTime = formatLocalTime(utcIso, tz, "24h");

        // 2. When sending back to API, we use the original UTC value
        const sentToApi = toUtcIsoString(utcIso);

        // 3. The sent value must be the same UTC instant as the original
        const originalMs = new Date(utcIso).getTime();
        const sentMs = new Date(sentToApi).getTime();
        expect(sentMs).toBe(originalMs);

        // 4. The sent value must be in UTC format
        expect(sentToApi).toMatch(/Z$/);
      }),
      { numRuns: 200 }
    );
  }, PROPERTY_TEST_TIMEOUT_MS);
});
