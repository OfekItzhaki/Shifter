/**
 * Capacity indicator formatting utility for the shift picker.
 *
 * Formats the current fill count and total capacity into a display string
 * used in the slot browser's capacity indicator.
 */

/**
 * Format a capacity indicator string showing current fill vs total capacity.
 *
 * @param currentFill - The current number of filled spots (non-negative integer)
 * @param capacity - The total capacity of the slot (positive integer)
 * @returns Formatted string like "3/5"
 */
export function formatCapacity(currentFill: number, capacity: number): string {
  return `${currentFill}/${capacity}`;
}
