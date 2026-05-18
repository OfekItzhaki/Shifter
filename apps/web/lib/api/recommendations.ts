import { apiClient } from "./client";

// ── Enums ─────────────────────────────────────────────────────────────────────

export type RecommendationStatus = "Active" | "Dismissed" | "Resolved" | "Cleared";

export type AcceptRecommendationOutcome = "Accepted" | "AlreadyEnabled" | "TaskNotFound";

// ── DTOs ──────────────────────────────────────────────────────────────────────

export interface Recommendation {
  id: string;
  groupTaskId: string;
  taskName: string;
  status: RecommendationStatus;
  additionalSlotsCovered: number;
  affectedDateStart: string;
  affectedDateEnd: string;
  totalUncoveredSlotsInRun: number;
  createdAt: string;
}

export interface RecommendationBanner {
  totalUncoveredSlots: number;
  recommendations: Recommendation[];
  remainingCount: number;
  affectedDateRange: string;
}

export interface AcceptRecommendationResult {
  outcome: AcceptRecommendationOutcome;
  message: string;
  enqueuedRunId: string | null;
}

// ── API Functions ─────────────────────────────────────────────────────────────

export async function getRecommendations(
  spaceId: string,
  groupId: string
): Promise<Recommendation[]> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/recommendations`
  );
  return data;
}

export async function getRecommendationsForRun(
  spaceId: string,
  runId: string
): Promise<RecommendationBanner> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/runs/${runId}/recommendations`
  );
  return data;
}

export async function getRecommendationForTask(
  spaceId: string,
  taskId: string
): Promise<Recommendation | null> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/tasks/${taskId}/recommendation`
  );
  return data;
}

export async function dismissRecommendation(
  spaceId: string,
  recommendationId: string
): Promise<void> {
  await apiClient.post(
    `/spaces/${spaceId}/recommendations/${recommendationId}/dismiss`
  );
}

export async function acceptRecommendation(
  spaceId: string,
  recommendationId: string,
  triggerNewRun: boolean
): Promise<AcceptRecommendationResult> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/recommendations/${recommendationId}/accept`,
    { triggerNewRun }
  );
  return data;
}
