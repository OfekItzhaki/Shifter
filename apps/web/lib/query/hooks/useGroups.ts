import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/lib/api/client";
import { getDeletedGroups, restoreGroup, DeletedGroupDto } from "@/lib/api/groups";
import { queryKeys } from "../keys";

export interface GroupDto {
  id: string;
  name: string;
  memberCount: number;
  solverHorizonDays: number;
  ownerPersonId: string | null;
}

export function useGroups(spaceId: string | null) {
  return useQuery({
    queryKey: queryKeys.groups(spaceId ?? ""),
    queryFn: async () => {
      const { data } = await apiClient.get<GroupDto[]>(`/spaces/${spaceId}/groups`);
      return data;
    },
    enabled: !!spaceId,
    staleTime: 30_000,
  });
}

export function useDeletedGroups(spaceId: string | null) {
  return useQuery({
    queryKey: ["deleted-groups", spaceId ?? ""],
    queryFn: () => getDeletedGroups(spaceId!),
    enabled: !!spaceId,
    staleTime: 60_000,
  });
}

export function useCreateGroup(spaceId: string | null) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (name: string) =>
      apiClient.post(`/spaces/${spaceId}/groups`, { name, description: null }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.groups(spaceId ?? "") });
    },
  });
}

export function useRestoreGroup(spaceId: string | null) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (groupId: string) => restoreGroup(spaceId!, groupId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.groups(spaceId ?? "") });
      qc.invalidateQueries({ queryKey: ["deleted-groups", spaceId ?? ""] });
    },
  });
}
