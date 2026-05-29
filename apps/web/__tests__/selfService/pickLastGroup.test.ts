/**
 * Unit tests for the pick last-group memory utility.
 *
 * Feature: shift-picker-lite
 * Task: 1.1 Create last-group memory utility module
 *
 * **Validates: Requirements 3.1, 3.2, 3.4, 3.5**
 */

import { describe, it, expect, beforeEach } from "vitest";
import {
  LAST_GROUP_KEY,
  getLastGroup,
  setLastGroup,
  clearLastGroup,
  resolveLastGroup,
} from "../../lib/utils/pickLastGroup";
import type { GroupWithMemberCountDto } from "../../lib/api/groups";

describe("LAST_GROUP_KEY", () => {
  it("equals 'shifter-pick-last-group'", () => {
    expect(LAST_GROUP_KEY).toBe("shifter-pick-last-group");
  });
});

describe("getLastGroup", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("returns null when nothing is stored", () => {
    expect(getLastGroup()).toBeNull();
  });

  it("returns null when stored value is empty string", () => {
    localStorage.setItem(LAST_GROUP_KEY, "");
    expect(getLastGroup()).toBeNull();
  });

  it("returns null when stored value is whitespace only", () => {
    localStorage.setItem(LAST_GROUP_KEY, "   ");
    expect(getLastGroup()).toBeNull();
  });

  it("returns the stored value when it is non-empty", () => {
    const id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";
    localStorage.setItem(LAST_GROUP_KEY, id);
    expect(getLastGroup()).toBe(id);
  });
});

describe("setLastGroup", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("stores the group ID in localStorage", () => {
    const id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";
    setLastGroup(id);
    expect(localStorage.getItem(LAST_GROUP_KEY)).toBe(id);
  });

  it("overwrites a previously stored value", () => {
    setLastGroup("first-id");
    setLastGroup("second-id");
    expect(localStorage.getItem(LAST_GROUP_KEY)).toBe("second-id");
  });
});

describe("clearLastGroup", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("removes the key from localStorage", () => {
    localStorage.setItem(LAST_GROUP_KEY, "some-value");
    clearLastGroup();
    expect(localStorage.getItem(LAST_GROUP_KEY)).toBeNull();
  });

  it("does not throw when key does not exist", () => {
    expect(() => clearLastGroup()).not.toThrow();
  });
});

describe("resolveLastGroup", () => {
  const groups: GroupWithMemberCountDto[] = [
    {
      id: "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      name: "Alpha",
      memberCount: 5,
      solverHorizonDays: 7,
      ownerPersonId: "p1",
      schedulingMode: "SelfService",
    },
    {
      id: "b2c3d4e5-f6a7-8901-bcde-f12345678901",
      name: "Beta",
      memberCount: 3,
      solverHorizonDays: 14,
      ownerPersonId: "p2",
      schedulingMode: "SelfService",
    },
  ];

  it("returns null when storedGroupId is null", () => {
    expect(resolveLastGroup(null, groups)).toBeNull();
  });

  it("returns null when storedGroupId is empty string", () => {
    expect(resolveLastGroup("", groups)).toBeNull();
  });

  it("returns null when storedGroupId is whitespace only", () => {
    expect(resolveLastGroup("   ", groups)).toBeNull();
  });

  it("returns null when storedGroupId is not a valid UUID", () => {
    expect(resolveLastGroup("not-a-uuid", groups)).toBeNull();
  });

  it("returns null when storedGroupId is a valid UUID but not in the groups list", () => {
    expect(resolveLastGroup("11111111-2222-3333-4444-555555555555", groups)).toBeNull();
  });

  it("returns the group ID when it is a valid UUID and exists in the groups list", () => {
    const id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";
    expect(resolveLastGroup(id, groups)).toBe(id);
  });

  it("handles uppercase UUID correctly", () => {
    const id = "A1B2C3D4-E5F6-7890-ABCD-EF1234567890";
    expect(resolveLastGroup(id, groups)).toBeNull();
    // Note: stored IDs should be lowercase to match, but UUID regex is case-insensitive
    // The match depends on the actual group ID casing in the list
  });

  it("returns null for an empty groups list", () => {
    expect(resolveLastGroup("a1b2c3d4-e5f6-7890-abcd-ef1234567890", [])).toBeNull();
  });

  it("returns null for partial UUID format", () => {
    expect(resolveLastGroup("a1b2c3d4-e5f6-7890-abcd", groups)).toBeNull();
  });

  it("returns null for UUID with extra characters", () => {
    expect(resolveLastGroup("a1b2c3d4-e5f6-7890-abcd-ef1234567890x", groups)).toBeNull();
  });
});
