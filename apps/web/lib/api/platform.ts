import { apiClient } from "./client";

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
