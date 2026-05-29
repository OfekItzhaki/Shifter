/**
 * Cancellation eligibility utility for the shift picker.
 *
 * Determines whether a shift can be cancelled based on the time remaining
 * until the shift starts and the configured cancellation cutoff hours.
 */

/**
 * Returns true if the shift is eligible for cancellation.
 *
 * A shift can be cancelled if and only if:
 *   shiftStartTime - currentTime > cutoffHours * 3600000
 *
 * @param shiftStartTime - The start time of the shift (Date or ISO string)
 * @param currentTime - The current time to compare against
 * @param cutoffHours - The cancellation cutoff in hours
 * @returns true if the shift can still be cancelled
 */
export function isCancellable(
  shiftStartTime: Date | string,
  currentTime: Date,
  cutoffHours: number
): boolean {
  const start =
    typeof shiftStartTime === "string"
      ? new Date(shiftStartTime)
      : shiftStartTime;

  if (isNaN(start.getTime())) return false;

  const cutoffMs = cutoffHours * 3600000;
  return start.getTime() - currentTime.getTime() > cutoffMs;
}
