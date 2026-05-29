/**
 * Last-group memory utilities for the Shift Picker (/pick) route.
 *
 * Stores and retrieves the member's last selected self-service group ID
 * in localStorage so returning members skip the group selector.
 */

import type { GroupWithMemberCountDto } from "@/lib/api/groups";

export const LAST_GROUP_KEY = "shifter-pick-last-group";

/**
 * UUID v4 format regex (case-insensitive).
 */
const UUID_REGEX = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Reads the last selected group ID from localStorage.
 * Returns null if the value is missing or empty.
 */
export function getLastGroup(): string | null {
  try {
    const value = localStorage.getItem(LAST_GROUP_KEY);
    return value && value.trim().length > 0 ? value : null;
  } catch {
    return null;
  }
}

/**
 * Stores the given group ID in localStorage.
 */
export function setLastGroup(groupId: string): void {
  try {
    localStorage.setItem(LAST_GROUP_KEY, groupId);
  } catch {
    // Silently ignore storage errors (e.g. quota exceeded, private browsing)
  }
}

/**
 * Removes the last-group key from localStorage.
 */
export function clearLastGroup(): void {
  try {
    localStorage.removeItem(LAST_GROUP_KEY);
  } catch {
    // Silently ignore storage errors
  }
}

/**
 * Validates a stored group ID against the member's current self-service groups.
 *
 * Returns the group ID if:
 * 1. It is not null/empty
 * 2. It matches UUID format
 * 3. It exists in the provided self-service groups list
 *
 * Otherwise returns null (caller should clear localStorage and show group selector).
 */
export function resolveLastGroup(
  storedGroupId: string | null,
  selfServiceGroups: GroupWithMemberCountDto[]
): string | null {
  if (!storedGroupId || storedGroupId.trim().length === 0) return null;
  if (!UUID_REGEX.test(storedGroupId)) return null;
  const match = selfServiceGroups.find((g) => g.id === storedGroupId);
  return match ? storedGroupId : null;
}
