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

// ── Cumulative Tracking Endpoints ─────────────────────────────────────────────

export interface HistoricalSnapshotDto {
  id: string;
  personId: string;
  groupId: string;
  snapshotDate: string;
  taskTypeId: string | null;
  slotId: string | null;
  shiftStart: string | null;
  shiftEnd: string | null;
  burdenLevel: string | null;
  versionId: string;
  periodId: string;
}

export interface HistoricalScheduleResponse {
  assignments: HistoricalSnapshotDto[];
  retentionExceeded: boolean;
}

export interface CumulativePersonStats {
  personId: string;
  displayName: string;
  profileImageUrl: string | null;
  totalAssignments: number;
  hardTasks: number;
  kitchenCount: number;
  nightMissions: number;
  dislikedHatedScore: number;
  totalHoursAssigned: number;
  consecutiveHoursAtBase: number;
  lastHomeLeaveEnd: string | null;
}

export interface CumulativeStatsResponse {
  people: CumulativePersonStats[];
  periodId: string | null;
  periodStartsAt: string | null;
  periodEndsAt: string | null;
  periodStatus: string | null;
  timeRange: string;
}

export interface TimeseriesDataPoint {
  date: string;
  assignmentsCount: number;
  hardCount: number;
  normalCount: number;
  easyCount: number;
}

export interface TimeseriesResponse {
  dataPoints: TimeseriesDataPoint[];
  periodId: string | null;
  periodStartsAt: string | null;
  periodEndsAt: string | null;
}

/**
 * Fetch historical schedule assignments from daily_snapshots.
 */
export async function getHistoricalSchedule(
  spaceId: string,
  groupId: string,
  startDate: string,
  endDate: string
): Promise<HistoricalScheduleResponse> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/schedule/history`, {
    params: { group_id: groupId, start_date: startDate, end_date: endDate },
  });
  return data;
}

/**
 * Fetch per-person cumulative statistics for a time range.
 */
export async function getCumulativeStats(
  spaceId: string,
  groupId: string,
  timeRange: string,
  periodId?: string
): Promise<CumulativeStatsResponse> {
  const params: Record<string, string> = {
    group_id: groupId,
    time_range: timeRange,
  };
  if (periodId) params.period_id = periodId;
  const { data } = await apiClient.get(`/spaces/${spaceId}/stats/cumulative`, { params });
  return data;
}

/**
 * Fetch daily time-series data for charting.
 */
export async function getStatsTimeseries(
  spaceId: string,
  groupId: string,
  startDate: string,
  endDate: string,
  periodId?: string
): Promise<TimeseriesResponse> {
  const params: Record<string, string> = {
    group_id: groupId,
    start_date: startDate,
    end_date: endDate,
  };
  if (periodId) params.period_id = periodId;
  const { data } = await apiClient.get(`/spaces/${spaceId}/stats/timeseries`, { params });
  return data;
}
