import type { AvailableSlotDto } from "../api/selfService";

/**
 * Sorts available slots by date ascending, then by start time ascending.
 * Returns a new sorted array (does not mutate the input).
 */
export function sortSlotsByDateTime(
  slots: AvailableSlotDto[]
): AvailableSlotDto[] {
  return [...slots].sort((a, b) => {
    const dateCompare = a.date.localeCompare(b.date);
    if (dateCompare !== 0) return dateCompare;
    return a.startTime.localeCompare(b.startTime);
  });
}
