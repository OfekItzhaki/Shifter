import { useQuery } from "@tanstack/react-query";
import { getGroupSchedule } from "@/lib/api/groups";
import { queryKeys } from "../keys";

/** Cache duration — schedule data is relatively stable, refresh every 60s */
const SCHEDULE_STALE_MS = 60_000;

/**
 * Fetches the published schedule assignments for a group.
 * Used by the Today, Tomorrow, and group schedule tab pages.
 */
export function useGroupSchedule(spaceId: string | null, groupId: string | null) {
  return useQuery({
    queryKey: queryKeys.groupSchedule(spaceId ?? "", groupId ?? ""),
    queryFn: () => getGroupSchedule(spaceId!, groupId!),
    enabled: !!spaceId && !!groupId,
    staleTime: SCHEDULE_STALE_MS,
  });
}
