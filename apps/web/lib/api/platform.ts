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
