import { describe, expect, it } from "vitest";
import {
  getPhonePlaceholder,
  normalizePhoneForLooseComparison,
  normalizePhoneNumberForCountry,
} from "@/lib/utils/phoneNumbers";

describe("phone number normalization", () => {
  it.each([
    ["0541234567", "+972541234567"],
    ["054-123-4567", "+972541234567"],
    ["972541234567", "+972541234567"],
    ["00972541234567", "+972541234567"],
    ["+972 54 123 4567", "+972541234567"],
    ["541234567", "+972541234567"],
    ["03-1234567", "+97231234567"],
  ])("normalizes Israeli number %s", (input, expected) => {
    expect(normalizePhoneNumberForCountry(input, "IL")).toEqual({
      value: expected,
      isValid: true,
    });
  });

  it("rejects invalid Israeli phone numbers", () => {
    expect(normalizePhoneNumberForCountry("054", "IL")).toEqual({
      value: "+97254",
      isValid: false,
    });
    expect(normalizePhoneNumberForCountry("not-a-phone", "IL").isValid).toBe(false);
  });

  it("uses common country dialing codes for non-Israeli countries", () => {
    expect(normalizePhoneNumberForCountry("5550000000", "US")).toEqual({
      value: "+15550000000",
      isValid: true,
    });
  });

  it("compares local and international Israeli numbers as the same value", () => {
    expect(normalizePhoneForLooseComparison("050-123-4567"))
      .toBe(normalizePhoneForLooseComparison("+972 50 123 4567"));
  });

  it("returns a country-specific placeholder when known", () => {
    expect(getPhonePlaceholder("IL")).toBe("+972 50 000 0000");
    expect(getPhonePlaceholder("US")).toBe("+1 555 000 0000");
  });
});
