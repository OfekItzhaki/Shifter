import { apiClient } from "./client";

// ── Types ────────────────────────────────────────────────────────────────────

export type HomeLeaveMode = "automatic" | "manual";

export interface HomeLeaveConfigDto {
  id: string;
  groupId: string;
  spaceId: string;
  mode: HomeLeaveMode;
  baseDays: number;
  homeDays: number;
  leaveDurationHours: number;
  leaveCapacity: number;
  minPeopleAtBase: number;
  restHoursAfterReturn: number;
  balanceValue: number;
  emergencyFreezeActive: boolean;
  emergencyUseForScheduling: boolean;
  freezeStartedAt: string | null;
  // Computed fields from API
  optimalBaseDays: number;
  optimalHomeDays: number;
  optimalIsReduced: boolean;
}

export interface UpdateHomeLeaveConfigPayload {
  mode: HomeLeaveMode;
  baseDays?: number | null;
  homeDays?: number | null;
  sliderValue?: number | null;
  leaveDurationHours: number;
  minPeopleAtBase: number;
  restHoursAfterReturn?: number | null;
  emergencyFreezeActive?: boolean | null;
  emergencyUseForScheduling?: boolean | null;
}

export interface OptimalRatioResponse {
  baseDays: number;
  homeDays: number;
  isReduced: boolean;
  memberCount: number;
  coverageRequirement: number;
}

export interface FeasibilityResultDto {
  isFeasible: boolean;
  maxFeasibleHomeDays?: number | null;
  reason?: string | null;
}

// ── Home-Leave Configuration ─────────────────────────────────────────────────

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

// ── Optimal Ratio ────────────────────────────────────────────────────────────

export async function getOptimalRatio(
  spaceId: string,
  groupId: string
): Promise<OptimalRatioResponse> {
  const { data } = await apiClient.get(
    `/spaces/${spaceId}/groups/${groupId}/home-leave-config/optimal-ratio`
  );
  return data;
}

// ── Emergency Freeze ─────────────────────────────────────────────────────────

export async function toggleEmergencyFreeze(
  spaceId: string,
  groupId: string,
  active: boolean,
  useForScheduling: boolean
): Promise<HomeLeaveConfigDto> {
  const { data } = await apiClient.put(
    `/spaces/${spaceId}/groups/${groupId}/home-leave-config`,
    {
      mode: "automatic", // mode is preserved server-side via pre-freeze mode
      emergencyFreezeActive: active,
      emergencyUseForScheduling: useForScheduling,
      leaveDurationHours: 48, // placeholder — server uses existing value
      minPeopleAtBase: 1, // placeholder — server uses existing value
    }
  );
  return data;
}

// ── Home-Leave Preview ───────────────────────────────────────────────────────

export interface HomeLeavePreviewRequest {
  mode: HomeLeaveMode;
  baseDays?: number | null;
  homeDays?: number | null;
  sliderValue?: number | null;
  leaveDurationHours?: number | null;
}

export interface CoverageGapDto {
  startsAt: string;
  endsAt: string;
  availableCount: number;
}

export interface HomeLeavePreviewResponse {
  preview: {
    status: "optimal" | "feasible" | "no_solution";
    peopleHomeCount: number;
    peopleAtBaseCount: number;
    totalHomeLeaveSlots: number;
    coverageGaps: CoverageGapDto[];
    fairnessSpread: number;
    solverTimeMs: number;
  };
  feasibility: FeasibilityResultDto | null;
}

export async function getHomeLeavePreview(
  spaceId: string,
  groupId: string,
  request: HomeLeavePreviewRequest,
  signal?: AbortSignal
): Promise<HomeLeavePreviewResponse> {
  const { data } = await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/home-leave-preview`,
    request,
    { signal }
  );
  return data;
}
