import { apiClient } from "./client";

export interface DailyStatPoint {
  date: string;
  count: number;
}

export interface WeeklyStatPoint {
  weekStart: string;
  averageScore: number;
}

export interface HistoricalStats {
  assignmentsPerDay: DailyStatPoint[];
  solverRunsPerDay: DailyStatPoint[];
  burdenScorePerWeek: WeeklyStatPoint[];
  totalAssignments: number;
  totalSolverRuns: number;
  totalVersionsPublished: number;
}

export async function getHistoricalStats(spaceId: string, days = 30): Promise<HistoricalStats> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/stats/historical?days=${days}`);
  return data;
}
