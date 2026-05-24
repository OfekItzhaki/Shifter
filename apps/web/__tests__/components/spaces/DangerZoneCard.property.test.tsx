/**
 * Property-based test for DangerZoneCard transfer target dropdown.
 *
 * Feature: space-management
 * Property 14: Transfer target dropdown excludes current owner
 *
 * For any space with N active members (2-10), the transfer target dropdown
 * SHALL show exactly (N-1) options, excluding the current owner.
 *
 * **Validates: Requirements 9.4**
 */

import { describe, it, expect, vi } from "vitest";
import * as fc from "fast-check";
import { render } from "@testing-library/react";
import React from "react";
import DangerZoneCard from "../../../components/spaces/DangerZoneCard";
import type { SpaceMemberDto } from "@/lib/api/spaces";

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => key,
}));

vi.mock("@/lib/api/spaces", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api/spaces")>();
  return {
    ...actual,
    softDeleteSpace: vi.fn().mockResolvedValue(undefined),
    transferOwnership: vi.fn().mockResolvedValue(undefined),
  };
});

// ── Arbitraries ───────────────────────────────────────────────────────────────

/**
 * Generates a list of N members (2-10) with unique userIds,
 * where one is designated as the current owner.
 * Returns { members, currentOwnerId }.
 */
const membersWithOwnerArb = fc
  .integer({ min: 2, max: 10 })
  .chain((n) =>
    fc.tuple(
      fc.uniqueArray(fc.uuid(), { minLength: n, maxLength: n }),
      fc.array(
        fc.oneof(fc.constant(null), fc.string({ minLength: 1, maxLength: 20 })),
        { minLength: n, maxLength: n }
      ),
      fc.array(
        fc.oneof(fc.constant(null), fc.emailAddress()),
        { minLength: n, maxLength: n }
      ),
      fc.integer({ min: 0, max: n - 1 })
    ).map(([ids, names, emails, ownerIdx]) => {
      const members: SpaceMemberDto[] = ids.map((id, i) => ({
        userId: id,
        displayName: names[i],
        email: emails[i],
        joinedAt: new Date(2025, 0, i + 1).toISOString(),
      }));
      return {
        members,
        currentOwnerId: ids[ownerIdx],
      };
    })
  );

// ── Property Tests ────────────────────────────────────────────────────────────

describe("Property 14: Transfer target dropdown excludes current owner", () => {
  // **Validates: Requirements 9.4**

  it("dropdown shows exactly (N-1) options excluding the current owner", () => {
    fc.assert(
      fc.property(membersWithOwnerArb, fc.uuid(), ({ members, currentOwnerId }, spaceId) => {
        const { container, unmount } = render(
          <DangerZoneCard
            spaceId={spaceId}
            isOwner={true}
            members={members}
            currentOwnerId={currentOwnerId}
          />
        );

        // Find the select element (transfer target dropdown)
        const select = container.querySelector("select");
        expect(select).not.toBeNull();

        // Get all option elements excluding the placeholder (value="")
        const options = Array.from(select!.querySelectorAll("option")).filter(
          (opt) => opt.value !== ""
        );

        // Should have exactly (N-1) options
        const expectedCount = members.length - 1;
        expect(options).toHaveLength(expectedCount);

        // The current owner should NOT appear in the dropdown option values
        const optionValues = options.map((opt) => opt.value);
        expect(optionValues).not.toContain(currentOwnerId);

        // All non-owner members should be present in the dropdown
        const nonOwnerIds = members
          .filter((m) => m.userId !== currentOwnerId)
          .map((m) => m.userId);
        expect(optionValues.sort()).toEqual(nonOwnerIds.sort());

        unmount();
      }),
      { numRuns: 100 }
    );
  });
});
