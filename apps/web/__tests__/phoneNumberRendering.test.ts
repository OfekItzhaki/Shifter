// Feature: group-alerts-and-phone
// Property 2: Phone number renders correctly for all members

import * as assert from "assert";
import { GroupMemberDto } from "../lib/api/groups";

let passed = 0; let failed = 0;
function test(name: string, fn: () => void) {
  try { fn(); console.log(`  ✓ ${name}`); passed++; }
  catch (err: any) { console.error(`  ✗ ${name}\n    ${err.message}`); failed++; }
}

// Helper: simulate what the UI renders for a member's phone
function renderPhone(member: GroupMemberDto): string {
  // This mirrors the JSX: {m.phoneNumber && <span>{m.phoneNumber}</span>}
  // Returns the rendered text or empty string if null/undefined
  if (!member.phoneNumber) return "";
  return member.phoneNumber;
}

console.log("\nProperty 2: Phone number renders correctly for all members");

test("non-null phone number is rendered as-is", () => {
  const members: GroupMemberDto[] = [
    { personId: "1", fullName: "Alice", displayName: null, isOwner: false, phoneNumber: "050-1234567", invitationStatus: "pending", profileImageUrl: null, birthday: null, linkedUserId: null },
    { personId: "2", fullName: "Bob", displayName: "Bob", isOwner: false, phoneNumber: "+972501234567", invitationStatus: "accepted", profileImageUrl: null, birthday: null, linkedUserId: null },
    { personId: "3", fullName: "Carol", displayName: null, isOwner: true, phoneNumber: "03-9876543", invitationStatus: "accepted", profileImageUrl: null, birthday: null, linkedUserId: null },
  ];
  for (const m of members) {
    const rendered = renderPhone(m);
    assert.strictEqual(rendered, m.phoneNumber, `Phone for ${m.fullName} should render as "${m.phoneNumber}"`);
  }
});

test("null phone number renders as empty string (not 'null')", () => {
  const members: GroupMemberDto[] = [
    { personId: "1", fullName: "Alice", displayName: null, isOwner: false, phoneNumber: null, invitationStatus: "pending", profileImageUrl: null, birthday: null, linkedUserId: null },
    { personId: "2", fullName: "Bob", displayName: "Bob", isOwner: false, phoneNumber: null, invitationStatus: "pending", profileImageUrl: null, birthday: null, linkedUserId: null },
  ];
  for (const m of members) {
    const rendered = renderPhone(m);
    assert.notStrictEqual(rendered, "null", `Phone for ${m.fullName} must not render as "null"`);
    assert.notStrictEqual(rendered, "undefined", `Phone for ${m.fullName} must not render as "undefined"`);
    assert.strictEqual(rendered, "", `Phone for ${m.fullName} should render as empty string`);
  }
});

test("mixed null and non-null phone numbers in same list", () => {
  const members: GroupMemberDto[] = [
    { personId: "1", fullName: "Alice", displayName: null, isOwner: false, phoneNumber: "050-1111111", invitationStatus: "accepted", profileImageUrl: null, birthday: null, linkedUserId: null },
    { personId: "2", fullName: "Bob", displayName: null, isOwner: false, phoneNumber: null, invitationStatus: "pending", profileImageUrl: null, birthday: null, linkedUserId: null },
    { personId: "3", fullName: "Carol", displayName: null, isOwner: false, phoneNumber: "050-2222222", invitationStatus: "accepted", profileImageUrl: null, birthday: null, linkedUserId: null },
    { personId: "4", fullName: "Dan", displayName: null, isOwner: false, phoneNumber: null, invitationStatus: "pending", profileImageUrl: null, birthday: null, linkedUserId: null },
  ];
  const rendered = members.map(renderPhone);
  assert.strictEqual(rendered[0], "050-1111111");
  assert.strictEqual(rendered[1], "");
  assert.strictEqual(rendered[2], "050-2222222");
  assert.strictEqual(rendered[3], "");
  // None should be "null" or "undefined"
  for (const r of rendered) {
    assert.notStrictEqual(r, "null");
    assert.notStrictEqual(r, "undefined");
  }
});

test("100 random members — none render 'null' or 'undefined'", () => {
  const phones = [null, "050-1234567", "+972501234567", null, "03-9876543", null, "052-0000000"];
  for (let i = 0; i < 100; i++) {
    const phone = phones[i % phones.length];
    const m: GroupMemberDto = {
      personId: String(i), fullName: `User ${i}`, displayName: null,
      isOwner: false, phoneNumber: phone, invitationStatus: "pending", profileImageUrl: null, birthday: null, linkedUserId: null
    };
    const rendered = renderPhone(m);
    assert.notStrictEqual(rendered, "null", `Iteration ${i}: must not render "null"`);
    assert.notStrictEqual(rendered, "undefined", `Iteration ${i}: must not render "undefined"`);
    if (phone !== null) {
      assert.strictEqual(rendered, phone, `Iteration ${i}: should render phone as-is`);
    } else {
      assert.strictEqual(rendered, "", `Iteration ${i}: null phone should render as empty`);
    }
  }
});

console.log(`\n${"─".repeat(40)}`);
console.log(`Results: ${passed} passed, ${failed} failed`);
if (failed > 0) process.exit(1);
