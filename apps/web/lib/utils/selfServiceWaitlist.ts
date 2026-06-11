import type { AdminWaitlistEntryDto, WaitlistEntryDto } from "@/lib/api/selfService";

export interface ClassifiedWaitlistEntries {
  offeredEntries: WaitlistEntryDto[];
  otherEntries: WaitlistEntryDto[];
}

export interface WaitlistSummary {
  offeredCount: number;
  waitingCount: number;
  activeAdminCount: number;
}

export function isActiveWaitlistStatus(status: WaitlistEntryDto["status"]): boolean {
  return status === "Offered" || status === "Waiting";
}

export function classifyWaitlistEntries(entries: WaitlistEntryDto[]): ClassifiedWaitlistEntries {
  return {
    offeredEntries: sortWaitlistEntriesForReview(entries.filter((entry) => entry.status === "Offered")),
    otherEntries: entries.filter((entry) => entry.status !== "Offered"),
  };
}

export function sortWaitlistEntriesForReview<T extends WaitlistEntryDto>(entries: T[]): T[] {
  return [...entries].sort((a, b) => {
    const aRank = getWaitlistReviewRank(a);
    const bRank = getWaitlistReviewRank(b);

    if (aRank !== bRank) return aRank - bRank;

    if (a.status === "Offered" && b.status === "Offered") {
      return getExpiryTime(a) - getExpiryTime(b);
    }

    if (a.status === "Waiting" && b.status === "Waiting") {
      return a.position - b.position;
    }

    return `${a.slotDate}T${a.slotStartTime}`.localeCompare(`${b.slotDate}T${b.slotStartTime}`);
  });
}

export function summarizeWaitlist(
  entries: WaitlistEntryDto[],
  adminEntries: AdminWaitlistEntryDto[]
): WaitlistSummary {
  return {
    offeredCount: entries.filter((entry) => entry.status === "Offered").length,
    waitingCount: entries.filter((entry) => entry.status === "Waiting").length,
    activeAdminCount: adminEntries.filter((entry) => isActiveWaitlistStatus(entry.status)).length,
  };
}

function getWaitlistReviewRank(entry: WaitlistEntryDto): number {
  if (entry.status === "Offered") return 0;
  if (entry.status === "Waiting") return 1;
  return 2;
}

function getExpiryTime(entry: WaitlistEntryDto): number {
  if (!entry.expiresAt) return Number.MAX_SAFE_INTEGER;

  const time = new Date(entry.expiresAt).getTime();
  return Number.isFinite(time) ? time : Number.MAX_SAFE_INTEGER;
}
