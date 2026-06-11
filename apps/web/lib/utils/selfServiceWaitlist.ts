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

export function classifyWaitlistEntries(entries: WaitlistEntryDto[]): ClassifiedWaitlistEntries {
  return {
    offeredEntries: entries.filter((entry) => entry.status === "Offered"),
    otherEntries: entries.filter((entry) => entry.status !== "Offered"),
  };
}

export function summarizeWaitlist(
  entries: WaitlistEntryDto[],
  adminEntries: AdminWaitlistEntryDto[]
): WaitlistSummary {
  return {
    offeredCount: entries.filter((entry) => entry.status === "Offered").length,
    waitingCount: entries.filter((entry) => entry.status === "Waiting").length,
    activeAdminCount: adminEntries.filter(
      (entry) => entry.status === "Offered" || entry.status === "Waiting"
    ).length,
  };
}
