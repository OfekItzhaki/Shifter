import type { GroupWithMemberCountDto } from "@/lib/api/groups";

/**
 * Filters groups to only those with self-service scheduling mode
 * and sorts them by name ascending using Hebrew locale comparison.
 *
 * @param groups - The full list of groups to filter
 * @returns Only groups with `schedulingMode === "SelfService"`, sorted by name (Hebrew locale)
 */
export function filterSelfServiceGroups(
  groups: GroupWithMemberCountDto[]
): GroupWithMemberCountDto[] {
  return groups
    .filter((g) => g.schedulingMode === "SelfService")
    .sort((a, b) => a.name.localeCompare(b.name, "he"));
}
