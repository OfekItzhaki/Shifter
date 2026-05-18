/**
 * Bug Condition Exploration Property Test
 *
 * Feature: redirect-and-member-email-fix
 * Task: 1 - Write bug condition exploration test
 *
 * **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
 *
 * CRITICAL: These tests are EXPECTED TO FAIL on unfixed code.
 * Failure confirms the bugs exist. DO NOT fix the code or tests when they fail.
 *
 * Bug Conditions:
 * - Pricing page back link navigates to `/login` instead of using browser history
 * - Space selection redirects to `/schedule/today` instead of `/groups`
 * - Member modal does not render email in info view
 * - Member edit form does not contain email input field
 */

import { describe, it, expect } from "vitest";
import * as fc from "fast-check";
import * as fs from "fs";
import * as path from "path";

// ── Source file paths ─────────────────────────────────────────────────────────

const PRICING_PAGE_PATH = path.resolve(
  __dirname,
  "../../app/pricing/page.tsx"
);
const SPACES_PAGE_PATH = path.resolve(
  __dirname,
  "../../app/spaces/page.tsx"
);
const MEMBERS_TAB_PATH = path.resolve(
  __dirname,
  "../../app/groups/[groupId]/tabs/MembersTab.tsx"
);
const GROUPS_API_PATH = path.resolve(
  __dirname,
  "../../lib/api/groups.ts"
);

// ── Read source files ─────────────────────────────────────────────────────────

const pricingPageSource = fs.readFileSync(PRICING_PAGE_PATH, "utf-8");
const spacesPageSource = fs.readFileSync(SPACES_PAGE_PATH, "utf-8");
const membersTabSource = fs.readFileSync(MEMBERS_TAB_PATH, "utf-8");
const groupsApiSource = fs.readFileSync(GROUPS_API_PATH, "utf-8");

// ── Bug Condition Types ───────────────────────────────────────────────────────

type BugConditionInput =
  | { type: "pricing_back_click"; hasHistory: boolean }
  | { type: "space_selection"; hasNoRedirectParam: boolean; spaceCount: number }
  | { type: "member_modal_open"; email: string | null }
  | { type: "member_edit_start"; email: string | null };

// ── Generators ────────────────────────────────────────────────────────────────

const pricingBackClickArb: fc.Arbitrary<BugConditionInput> = fc.record({
  type: fc.constant("pricing_back_click" as const),
  hasHistory: fc.boolean(),
});

const spaceSelectionArb: fc.Arbitrary<BugConditionInput> = fc.record({
  type: fc.constant("space_selection" as const),
  hasNoRedirectParam: fc.constant(true), // Bug only manifests without redirect param
  spaceCount: fc.integer({ min: 1, max: 10 }),
});

const memberModalOpenArb: fc.Arbitrary<BugConditionInput> = fc.record({
  type: fc.constant("member_modal_open" as const),
  email: fc.oneof(
    fc.emailAddress(),
    fc.constant(null)
  ),
});

const memberEditStartArb: fc.Arbitrary<BugConditionInput> = fc.record({
  type: fc.constant("member_edit_start" as const),
  email: fc.oneof(
    fc.emailAddress(),
    fc.constant(null)
  ),
});

const bugConditionArb: fc.Arbitrary<BugConditionInput> = fc.oneof(
  pricingBackClickArb,
  spaceSelectionArb,
  memberModalOpenArb,
  memberEditStartArb
);

// ── Property Tests ────────────────────────────────────────────────────────────

describe("Property 1: Bug Condition - Navigation and Email Display Defects", () => {
  /**
   * Bug 1.1: Pricing page back link should use router.back() or navigate to "/"
   * instead of hardcoding href="/login"
   *
   * Expected behavior: The pricing page back element triggers router.back()
   * or navigates to "/" as fallback.
   *
   * Current bug: The pricing page uses <Link href="/login"> for the back link.
   */
  it("pricing page back element triggers router.back() or navigates to / as fallback (NOT /login)", () => {
    fc.assert(
      fc.property(pricingBackClickArb, (input) => {
        // The pricing page source should NOT contain a Link to /login for the back button
        // Instead it should use router.back() or window.history.back()
        const hasHardcodedLoginLink = pricingPageSource.includes('href="/login"');
        const usesRouterBack = pricingPageSource.includes("router.back()") ||
          pricingPageSource.includes("history.back()") ||
          pricingPageSource.includes("window.history.back()");

        // Expected behavior: no hardcoded /login link, uses history navigation
        expect(hasHardcodedLoginLink).toBe(false);
        expect(usesRouterBack).toBe(true);
      }),
      { numRuns: 10 }
    );
  });

  /**
   * Bug 1.2: Space selection should redirect to /groups instead of /schedule/today
   *
   * Expected behavior: handleSelect on spaces page calls router.push("/groups")
   *
   * Current bug: handleSelect calls router.push("/schedule/today")
   */
  it("handleSelect on spaces page calls router.push('/groups') not '/schedule/today'", () => {
    fc.assert(
      fc.property(spaceSelectionArb, (input) => {
        // The spaces page should push to "/groups" not "/schedule/today"
        const pushesToScheduleToday = spacesPageSource.includes('router.push("/schedule/today")');
        const pushesToGroups = spacesPageSource.includes('router.push("/groups")');

        // Expected behavior: pushes to /groups, not /schedule/today
        expect(pushesToScheduleToday).toBe(false);
        expect(pushesToGroups).toBe(true);
      }),
      { numRuns: 10 }
    );
  });

  /**
   * Bug 1.3: Member modal info view should render email when present in DTO
   *
   * Expected behavior: The member modal info view displays member.email
   *
   * Current bug: No email rendering exists in the member modal info view
   */
  it("member modal info view renders email when present in DTO", () => {
    fc.assert(
      fc.property(memberModalOpenArb, (input) => {
        // The MembersTab source should render the email in the info view
        // Look for email display in the info section (non-edit view)
        const infoViewSection = membersTabSource.split("editForm ?")[1]?.split(": (")[0] ?? "";
        const fullInfoView = membersTabSource;

        // Check that the info view renders member.email
        const rendersEmail = fullInfoView.includes("member.email") ||
          fullInfoView.includes("{member.email}");

        // Expected behavior: email is rendered in the info view
        expect(rendersEmail).toBe(true);
      }),
      { numRuns: 10 }
    );
  });

  /**
   * Bug 1.4: Member edit form should include an email input field
   *
   * Expected behavior: The edit form contains an email input field
   *
   * Current bug: The editForm type and UI do not include email
   */
  it("member edit form includes an email input field", () => {
    fc.assert(
      fc.property(memberEditStartArb, (input) => {
        // The editForm type in MemberProfileModalProps should include email
        // Look for the editForm type definition
        const editFormTypeMatch = membersTabSource.match(
          /editForm:\s*\{[^}]+\}/
        );
        const editFormType = editFormTypeMatch?.[0] ?? "";

        // Check that the edit form type includes email
        const editFormHasEmail = editFormType.includes("email");

        // Also check that there's an email input in the edit form UI
        const hasEmailInput = membersTabSource.includes('type="email"') ||
          (membersTabSource.includes("email") &&
            membersTabSource.includes("onChangeForm") &&
            membersTabSource.includes("editForm.email"));

        // Expected behavior: edit form type includes email and UI has email input
        expect(editFormHasEmail).toBe(true);
        expect(hasEmailInput).toBe(true);
      }),
      { numRuns: 10 }
    );
  });

  /**
   * Additional check: GroupMemberDto should include email field
   */
  it("GroupMemberDto interface includes email field", () => {
    fc.assert(
      fc.property(memberModalOpenArb, (input) => {
        // Extract the GroupMemberDto interface from the groups API source
        const dtoMatch = groupsApiSource.match(
          /export interface GroupMemberDto \{[\s\S]*?\}/
        );
        const dtoSource = dtoMatch?.[0] ?? "";

        // Check that the DTO includes an email field
        const hasEmailField = dtoSource.includes("email");

        // Expected behavior: GroupMemberDto has an email field
        expect(hasEmailField).toBe(true);
      }),
      { numRuns: 10 }
    );
  });
});
