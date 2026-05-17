import { apiClient } from "./client";
import type { SolverInputDto, SolverOutputDto } from "@/lib/store/sandboxStore";

/**
 * Calls the simulation endpoint to run the solver with the given payload.
 * This is a synchronous solver call (no job queue) — admin-only, low concurrency.
 */
export async function runSimulation(
  spaceId: string,
  groupId: string,
  payload: SolverInputDto
): Promise<SolverOutputDto> {
  const { data } = await apiClient.post<SolverOutputDto>(
    `/spaces/${spaceId}/groups/${groupId}/simulate`,
    { payload }
  );
  return data;
}

// ── Publish Sandbox Types ─────────────────────────────────────────────────────

export interface TaskOverrideDto {
  action: string;
  existingTaskId?: string | null;
  name?: string | null;
  startsAt?: string | null;
  endsAt?: string | null;
  shiftDurationMinutes?: number | null;
  requiredHeadcount?: number | null;
  burdenLevel?: string | null;
  requiredQualificationNames?: string[] | null;
}

export interface ConstraintOverrideDto {
  action: string;
  existingConstraintId?: string | null;
  ruleType?: string | null;
  severity?: string | null;
  scopeType?: string | null;
  scopeId?: string | null;
  payload?: Record<string, unknown> | null;
}

export interface SettingsOverrideDto {
  minRestBetweenShiftsHours?: number | null;
  eligibilityThresholdHours?: number | null;
  leaveDurationHours?: number | null;
  leaveCapacity?: number | null;
  balanceValue?: number | null;
  minPeopleAtBase?: number | null;
}

export interface PublishSandboxRequest {
  versionId: string;
  taskOverrides: TaskOverrideDto[];
  constraintOverrides: ConstraintOverrideDto[];
  memberExclusions: string[];
  settingsOverrides: SettingsOverrideDto | null;
}

/**
 * Publishes sandbox overrides alongside the draft version.
 * Persists all overrides in a single transaction.
 * Returns 204 on success, 409 on conflict (version already published/discarded).
 */
export async function publishSandbox(
  spaceId: string,
  groupId: string,
  request: PublishSandboxRequest
): Promise<void> {
  await apiClient.post(
    `/spaces/${spaceId}/groups/${groupId}/publish-sandbox`,
    request
  );
}
