import type { GroupMemberDto, GroupRoleDto } from "@/lib/api/groups";

/**
 * Builds a lookup map from personId → role color.
 * Returns null for persons without a role or whose role has no color.
 */
export function buildRoleColorMap(
  members: GroupMemberDto[],
  roles: GroupRoleDto[]
): Map<string, string | null> {
  const roleMap = new Map<string, string | null>();
  for (const role of roles) {
    roleMap.set(role.id, role.color ?? null);
  }

  const result = new Map<string, string | null>();
  for (const member of members) {
    if (member.roleId) {
      result.set(member.personId, roleMap.get(member.roleId) ?? null);
    }
  }
  return result;
}
