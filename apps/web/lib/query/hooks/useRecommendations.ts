import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getRecommendations,
  getRecommendationsForRun,
  getRecommendationForTask,
  dismissRecommendation,
  acceptRecommendation,
  AcceptRecommendationResult,
} from "@/lib/api/recommendations";
import { queryKeys } from "../keys";

/**
 * Fetches active recommendations for a group.
 * Validates: Requirements 3.1
 */
export function useRecommendations(spaceId: string | null, groupId: string | null) {
  return useQuery({
    queryKey: queryKeys.recommendations(spaceId ?? "", groupId ?? ""),
    queryFn: () => getRecommendations(spaceId!, groupId!),
    enabled: !!spaceId && !!groupId,
  });
}

/**
 * Fetches banner data (recommendations) for a specific solver run.
 * Validates: Requirements 3.2
 */
export function useRecommendationsForRun(spaceId: string | null, runId: string | null) {
  return useQuery({
    queryKey: queryKeys.recommendationsForRun(spaceId ?? "", runId ?? ""),
    queryFn: () => getRecommendationsForRun(spaceId!, runId!),
    enabled: !!spaceId && !!runId,
  });
}

/**
 * Fetches inline suggestion (single recommendation) for a specific task.
 * Validates: Requirements 3.3
 */
export function useRecommendationForTask(spaceId: string | null, taskId: string | null) {
  return useQuery({
    queryKey: queryKeys.recommendationForTask(spaceId ?? "", taskId ?? ""),
    queryFn: () => getRecommendationForTask(spaceId!, taskId!),
    enabled: !!spaceId && !!taskId,
  });
}

/**
 * Mutation hook to dismiss a recommendation.
 * Invalidates all recommendation queries on success.
 * Validates: Requirements 4.3
 */
export function useDismissRecommendation(spaceId: string | null) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (recommendationId: string) =>
      dismissRecommendation(spaceId!, recommendationId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["recommendations", spaceId ?? ""] });
      qc.invalidateQueries({ queryKey: ["recommendations-run", spaceId ?? ""] });
      qc.invalidateQueries({ queryKey: ["recommendation-task", spaceId ?? ""] });
    },
  });
}

interface AcceptRecommendationParams {
  recommendationId: string;
  triggerNewRun: boolean;
}

/**
 * Mutation hook to accept a recommendation (enable double shift on the task).
 * Supports `triggerNewRun` option to optionally enqueue a new solver run.
 * Invalidates all recommendation queries on success.
 * Validates: Requirements 4.1
 */
export function useAcceptRecommendation(spaceId: string | null) {
  const qc = useQueryClient();
  return useMutation<AcceptRecommendationResult, Error, AcceptRecommendationParams>({
    mutationFn: ({ recommendationId, triggerNewRun }) =>
      acceptRecommendation(spaceId!, recommendationId, triggerNewRun),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["recommendations", spaceId ?? ""] });
      qc.invalidateQueries({ queryKey: ["recommendations-run", spaceId ?? ""] });
      qc.invalidateQueries({ queryKey: ["recommendation-task", spaceId ?? ""] });
    },
  });
}
