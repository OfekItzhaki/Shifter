import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/lib/api/client";
import { queryKeys } from "../keys";

export interface MyAssignmentDto {
  id: string;
  groupId: string;
  groupName: string;
  taskTypeName: string;
  slotStartsAt: string;
  slotEndsAt: string;
  source: string;
}

export type AssignmentRange = "today" | "week" | "month" | "year";

export function useMyAssignments(spaceId: string | null, range: AssignmentRange) {
  return useQuery({
    queryKey: queryKeys.myAssignments(spaceId ?? "", range),
    queryFn: async () => {
      const { data } = await apiClient.get<MyAssignmentDto[]>(
        `/spaces/${spaceId}/my-assignments?range=${range}`
      );
      return data;
    },
    enabled: !!spaceId,
    staleTime: 60_000,
  });
}
