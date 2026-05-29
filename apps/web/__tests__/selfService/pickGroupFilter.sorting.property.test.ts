/**
 * Property-based tests for group sorting in the pick group filter utility.
 *
 * Feature: shift-picker-lite
 * Task: 2.3 Write property test for group sorting
 *
 * **Validates: Requirements 2.1**
 */

import { describe, it, expect } from "vitest";
import * as fc from "fast-check";
import { filterSelfServiceGroups } from "../../lib/utils/pickGroupFilter";
import type { GroupWithMemberCountDto } from "../../lib/api/groups";

/**
 * Arbitrary that generates a random group name using Hebrew and English characters.
 * This covers the realistic input space for the Hebrew locale sorting.
 */
const hebrewChars = "אבגדהוזחטיכלמנסעפצקרשת";
const englishChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
const allChars = hebrewChars + englishChars + " 0123456789";

const groupNameArb = fc
  .stringOf(fc.constantFrom(...allChars.split("")), { minLength: 1, maxLength: 30 })
  .filter((s) => s.trim().length > 0);

/**
 * Arbitrary that generates a GroupWithMemberCountDto with schedulingMode "SelfService"
 * and a random Hebrew/English name.
 */
const selfServiceGroupArb = groupNameArb.map(
  (name): GroupWithMemberCountDto => ({
    id: fc.sample(fc.uuid(), 1)[0],
    name,
    memberCount: 1,
    solverHorizonDays: 7,
    ownerPersonId: null,
    schedulingMode: "SelfService",
  })
);

/**
 * Arbitrary that generates an array of self-service groups with unique IDs.
 */
const selfServiceGroupsArb = fc
  .array(groupNameArb, { minLength: 0, maxLength: 20 })
  .map((names) =>
    names.map(
      (name, i): GroupWithMemberCountDto => ({
        id: `group-${i}-${Math.random().toString(36).slice(2, 10)}`,
        name,
        memberCount: i + 1,
        solverHorizonDays: 7,
        ownerPersonId: null,
        schedulingMode: "SelfService",
      })
    )
  );

describe("Property 4: Group list sorting is stable and locale-aware", () => {
  it("sorted output is in ascending order by name using Hebrew locale comparison", () => {
    fc.assert(
      fc.property(selfServiceGroupsArb, (groups) => {
        const result = filterSelfServiceGroups(groups);

        // For every consecutive pair (a, b), a.name.localeCompare(b.name, "he") <= 0
        for (let i = 0; i < result.length - 1; i++) {
          const comparison = result[i].name.localeCompare(
            result[i + 1].name,
            "he"
          );
          expect(comparison).toBeLessThanOrEqual(0);
        }
      }),
      { numRuns: 100 }
    );
  });
});
