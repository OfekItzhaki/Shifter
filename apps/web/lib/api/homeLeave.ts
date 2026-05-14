import { apiClient } from "./client";

// ── Home-Leave Configuration ─────────────────────────────────────────────────

export interface HomeLeaveConfigDto {
  groupId: string;
  minRestHours: number;
  eligibilityThresholdHours: number;
  leaveCapacity: number;
  leaveDurationHours: number;
  balanceValue: number;
}

export interface UpdateHomeLeaveConfigPayload {
  minRestHours: number;
  eligibilityThresholdHours: number;
  leaveCapacity: number;
  leaveDurationHours: number;
  balanceValue?: number;
}

export async function getHomeLeaveConfig(
  spaceId: string,
  groupId: string
): Promise<HomeLeaveConfigDto> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/home-leave-config`
  );
  return data;
}

export async function updateHomeLeaveConfig(
  spaceId: string,
  groupId: string,
  payload: UpdateHomeLeaveConfigPayload
): Promise<HomeLeaveConfigDto> {
  const { data } = await apiClient.put(
    `/spaces/${spaceId}/groups/${groupId}/home-leave-config`,
    payload
  );
  return data;
}

// ── Home-Leave Preview ───────────────────────────────────────────────────────

export interface CoverageGapDto {
  startsAt: string;
  endsAt: string;
  availableCount: number;
}

export interface HomeLeavePreviewResponse {
  status: "optimal" | "feasible" | "no_solution";
  peopleHomeCount: number;
  peopleAtBaseCount: number;
  totalHomeLeaveSlots: number;
  coverageGaps: CoverageGapDto[];
  fairnessSpread: number;
  solverTimeMs: number;
}

export async function getHomeLeavePreview(
  spaceId: string,
  groupId: string,
  balanceValue: number,
  signal?: AbortSignal
): Promise<HomeLeavePreviewResponse> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/home-leave-preview`,
    { balanceValue },
    { signal }
  );
  return data;
}
