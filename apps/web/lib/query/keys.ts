export const queryKeys = {
  notifications: (spaceId: string) => ["notifications", spaceId] as const,
  groups: (spaceId: string) => ["groups", spaceId] as const,
  groupMembers: (spaceId: string, groupId: string) => ["group-members", spaceId, groupId] as const,
  groupSchedule: (spaceId: string, groupId: string) => ["group-schedule", spaceId, groupId] as const,
  draftVersions: (spaceId: string) => ["draft-versions", spaceId] as const,
  myAssignments: (spaceId: string, range: string) => ["my-assignments", spaceId, range] as const,
  spaces: () => ["spaces"] as const,
};
