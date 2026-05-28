/**
 * Self-service scheduling formatting utilities.
 *
 * Provides Hebrew-locale date formatting, 24h time display,
 * countdown timers for waitlist/swap expiry, and capacity
 * visual classification for the slot browser.
 */

/** Hebrew day names indexed by JS getDay() (0=Sunday, 6=Saturday) */
export const HEBREW_DAY_NAMES = [
  "ראשון",
  "שני",
  "שלישי",
  "רביעי",
  "חמישי",
  "שישי",
  "שבת",
] as const;

/**
 * Format a date string as a Hebrew locale date with day name.
 *
 * @param date - A date string in ISO format (e.g. "2024-06-15") or full ISO datetime
 * @returns Hebrew formatted string like "יום ראשון, 15.6.2024" or "—" for invalid input
 */
export function formatSlotDate(date: string): string {
  if (!date) return "—";

  try {
    const d = new Date(date);
    if (isNaN(d.getTime())) return "—";

    const dayName = HEBREW_DAY_NAMES[d.getDay()];
    const formatted = d.toLocaleDateString("he-IL", {
      day: "numeric",
      month: "numeric",
      year: "numeric",
    });

    return `יום ${dayName}, ${formatted}`;
  } catch {
    return "—";
  }
}

/**
 * Format a time string in 24-hour format (HH:mm) with no AM/PM.
 *
 * Accepts time strings like "14:30", "14:30:00", "2:30 PM", or full ISO datetime strings.
 *
 * @param time - A time string (HH:mm, HH:mm:ss) or ISO datetime string
 * @returns Formatted time string like "14:30" or "—" for invalid input
 */
export function formatTime24h(time: string): string {
  if (!time) return "—";

  try {
    // If it's already in HH:mm or HH:mm:ss format, extract hours and minutes
    const timeOnlyMatch = time.match(/^(\d{1,2}):(\d{2})(?::(\d{2}))?$/);
    if (timeOnlyMatch) {
      const hours = parseInt(timeOnlyMatch[1], 10);
      const minutes = parseInt(timeOnlyMatch[2], 10);
      if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59) return "—";
      return `${hours.toString().padStart(2, "0")}:${minutes.toString().padStart(2, "0")}`;
    }

    // Try parsing as a full date/datetime string
    const d = new Date(time);
    if (isNaN(d.getTime())) return "—";

    const hours = d.getHours();
    const minutes = d.getMinutes();
    return `${hours.toString().padStart(2, "0")}:${minutes.toString().padStart(2, "0")}`;
  } catch {
    return "—";
  }
}

/**
 * Calculate remaining time string for countdown display.
 *
 * Returns a Hebrew countdown string:
 * - If more than 24 hours remaining: "X ימים Y שעות"
 * - If 24 hours or less remaining: "X שעות Y דקות"
 * - If expired: "פג תוקף"
 *
 * @param expiresAt - An ISO datetime string representing the expiry time
 * @returns Hebrew countdown string or "—" for invalid input
 */
export function formatCountdown(expiresAt: string): string {
  if (!expiresAt) return "—";

  try {
    const expiry = new Date(expiresAt);
    if (isNaN(expiry.getTime())) return "—";

    const now = new Date();
    const diffMs = expiry.getTime() - now.getTime();

    if (diffMs <= 0) return "פג תוקף";

    const totalMinutes = Math.floor(diffMs / (1000 * 60));
    const totalHours = Math.floor(totalMinutes / 60);

    if (totalHours >= 24) {
      const days = Math.floor(totalHours / 24);
      const remainingHours = totalHours % 24;
      return `${days} ימים ${remainingHours} שעות`;
    }

    const hours = totalHours;
    const minutes = totalMinutes % 60;
    return `${hours} שעות ${minutes} דקות`;
  } catch {
    return "—";
  }
}

/**
 * Determine capacity visual class based on fill ratio.
 *
 * Returns "high-availability" if remaining capacity is more than 50%,
 * otherwise returns "nearly-full".
 *
 * @param currentFill - Current number of filled spots
 * @param capacity - Total capacity of the slot
 * @returns CSS class name: "high-availability" or "nearly-full"
 */
export function getCapacityClass(currentFill: number, capacity: number): string {
  if (capacity <= 0) return "nearly-full";

  const remaining = capacity - currentFill;
  const remainingRatio = remaining / capacity;

  return remainingRatio > 0.5 ? "high-availability" : "nearly-full";
}
