import { apiClient } from "./client";
import type {
  SpaceSelfServiceDefaultsDto,
  UpdateSpaceSelfServiceDefaultsPayload,
} from "./spaces";

export interface SolverStats {
  totalRunsLast24h: number;
  completedLast24h: number;
  failedLast24h: number;
  avgDurationMs: number;
  queueDepth: number;
}

export interface StorageStats {
  totalAssignments: number;
  totalConstraints: number;
  totalTasks: number;
}

export interface PlatformStats {
  totalUsers: number;
  activeUsersLast7d: number;
  totalSpaces: number;
  totalGroups: number;
  totalPeople: number;
  solverStats: SolverStats;
  storageStats: StorageStats;
}

export async function getPlatformStats(): Promise<PlatformStats> {
  const { data } = await apiClient.get<PlatformStats>("/platform/stats");
  return data;
}

export interface OrganizationCandidateDto {
  id: string;
  displayName: string;
  normalizedName: string;
  primaryOwnerUserId: string;
  primaryOwnerEmail: string | null;
  primaryOwnerDisplayName: string | null;
  countryCode: string | null;
  setupTemplate: string | null;
  defaultLocale: string | null;
  status: string;
  disabledAt: string | null;
  purgeEligibleAt: string | null;
  dedicatedDeploymentKey: string | null;
  spaceCount: number;
  groupCount: number;
  memberCount: number;
  createdAt: string;
}

export async function searchPlatformOrganizations(search?: string): Promise<OrganizationCandidateDto[]> {
  const { data } = await apiClient.get<OrganizationCandidateDto[]>("/platform/organizations", {
    params: {
      search: search?.trim() || undefined,
      limit: 25,
    },
  });
  return data;
}

export async function getOrganizationSelfServiceDefaults(
  organizationId: string
): Promise<SpaceSelfServiceDefaultsDto> {
  const { data } = await apiClient.get<SpaceSelfServiceDefaultsDto>(
    `/platform/organizations/${organizationId}/self-service-defaults`
  );
  return data;
}

export async function updateOrganizationSelfServiceDefaults(
  organizationId: string,
  payload: UpdateSpaceSelfServiceDefaultsPayload
): Promise<SpaceSelfServiceDefaultsDto> {
  const { data } = await apiClient.put<SpaceSelfServiceDefaultsDto>(
    `/platform/organizations/${organizationId}/self-service-defaults`,
    payload
  );
  return data;
}

export type ProviderHealthStatus = "healthy" | "unhealthy" | "skipped" | string;

export interface ProviderHealthCheck {
  serviceName: string;
  status: ProviderHealthStatus;
  errorMessage: string | null;
  responseTime: string | null;
}

export interface ProviderHealthReport {
  overallStatus: "healthy" | "degraded" | string;
  version: string;
  timestamp: string;
  checks: ProviderHealthCheck[];
}

export async function getProviderHealthReport(): Promise<ProviderHealthReport> {
  const { data } = await apiClient.get<ProviderHealthReport>("/health/detailed", {
    validateStatus: (status) => status === 200 || status === 503,
  });
  return data;
}
