import { apiClient } from "./client";

export interface AssignmentDto {
  id: string;
  taskSlotId: string;
  personId: string;
  personName: string;
  taskTypeName: string;
  slotStartsAt: string;
  slotEndsAt: string;
  source: string;
}

export interface DiffSummaryDto {
  addedCount: number;
  removedCount: number;
  changedCount: number;
  stabilityScore: number | null;
  diffJson: string | null;
}

export interface ScheduleVersionDto {
  id: string;
  versionNumber: number;
  status: string;
  createdAt: string;
  publishedAt: string | null;
  summaryJson: string | null;
}

export interface ScheduleVersionDetailDto {
  version: ScheduleVersionDto;
  diff: DiffSummaryDto | null;
  assignments: AssignmentDto[];
}

export async function getCurrentSchedule(spaceId: string): Promise<ScheduleVersionDetailDto | null> {
  try {
    const { data } = await apiClient.get(`/spaces/${spaceId}/schedule-versions/current`);
    return data;
  } catch (e: any) {
    if (e.response?.status === 404) return null;
    throw e;
  }
}

export async function getScheduleVersions(spaceId: string): Promise<ScheduleVersionDto[]> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/schedule-versions`);
  return data;
}

export async function getVersionDetail(spaceId: string, versionId: string): Promise<ScheduleVersionDetailDto> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/schedule-versions/${versionId}`);
  return data;
}

export async function triggerSolve(spaceId: string, triggerMode = "standard"): Promise<{ runId: string }> {
  const { data } = await apiClient.post(`/spaces/${spaceId}/schedule-runs/trigger`, { triggerMode });
  return data;
}

export async function getRunStatus(spaceId: string, runId: string) {
  const { data } = await apiClient.get(`/spaces/${spaceId}/schedule-runs/${runId}`);
  return data;
}

export async function publishVersion(spaceId: string, versionId: string): Promise<void> {
  await apiClient.post(`/spaces/${spaceId}/schedule-versions/${versionId}/publish`);
}

export async function rollbackVersion(spaceId: string, versionId: string): Promise<{ newVersionId: string }> {
  const { data } = await apiClient.post(`/spaces/${spaceId}/schedule-versions/${versionId}/rollback`);
  return data;
}

export function exportCsvUrl(spaceId: string, versionId: string): string {
  return `/api/proxy/spaces/${spaceId}/exports/${versionId}/csv`;
}

export function exportPdfUrl(spaceId: string, versionId: string): string {
  return `/api/proxy/spaces/${spaceId}/exports/${versionId}/pdf`;
}

export async function downloadExport(
  spaceId: string, versionId: string, format: "csv" | "pdf"
): Promise<void> {
  const { data, headers } = await apiClient.get(
    `/spaces/${spaceId}/exports/${versionId}/${format}`,
    { responseType: "blob" }
  );
  const mime = format === "pdf" ? "application/pdf" : "text/csv";
  const ext  = format === "pdf" ? "pdf" : "csv";
  const cd   = headers["content-disposition"] ?? "";
  const name = cd.match(/filename="?([^"]+)"?/)?.[1] ?? `schedule.${ext}`;
  const url  = URL.createObjectURL(new Blob([data], { type: mime }));
  const a    = document.createElement("a");
  a.href = url; a.download = name; a.click();
  URL.revokeObjectURL(url);
}

// ── Statistics / Burden ───────────────────────────────────────────────────────

export interface PersonBurdenStats {
  personId: string;
  displayName: string;
  profileImageUrl: string | null;
  // Rolling counters
  totalAssignments7d: number;
  totalAssignments14d: number;
  totalAssignments30d: number;
  hatedTasks7d: number;
  hatedTasks14d: number;
  dislikedHatedScore7d: number;
  kitchenCount7d: number;
  nightMissions7d: number;
  consecutiveBurdenCount: number;
  // All-time
  totalAssignmentsAllTime: number;
  hatedTasksAllTime: number;
  dislikedTasksAllTime: number;
  favorableTasksAllTime: number;
  burdenScoreAllTime: number;
  // Extended
  groupsCount: number;
  lastAssignmentDate: string | null;
  averageAssignmentsPerWeek: number;
  burdenBalance: number;
}

export interface LeaderboardEntry {
  personId: string;
  displayName: string;
  profileImageUrl: string | null;
  value: number;
  label: string;
}

export interface BurdenStats {
  people: PersonBurdenStats[];
  mostAssignments: LeaderboardEntry[];
  mostHatedTasks: LeaderboardEntry[];
  highestBurdenScore: LeaderboardEntry[];
  mostKitchenDuty: LeaderboardEntry[];
  mostNightMissions: LeaderboardEntry[];
  mostFavorableTasks: LeaderboardEntry[];
  bestBurdenBalance: LeaderboardEntry[];
  worstBurdenBalance: LeaderboardEntry[];
  mostConsecutiveBurden: LeaderboardEntry[];
  totalPublishedAssignments: number;
  totalPeople: number;
  totalGroups: number;
  totalPublishedVersions: number;
  averageAssignmentsPerPerson: number;
  mostBurdenedPersonId: string | null;
  leastBurdenedPersonId: string | null;
  lastUpdated: string | null;
}

export async function getBurdenStats(spaceId: string): Promise<BurdenStats> {
  const { data } = await apiClient.get(`/spaces/${spaceId}/stats/burden`);
  return data;
}
