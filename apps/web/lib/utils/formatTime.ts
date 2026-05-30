/**
 * Timezone-aware time formatting utility.
 *
 * Uses Intl.DateTimeFormat with the IANA `timeZone` option for correct
 * DST handling. All incoming values are expected as UTC ISO strings.
 * All outgoing API requests continue to send UTC — this utility is
 * strictly for display purposes.
 *
 * Default timezone: "Asia/Jerusalem" (primary user base).
 */

export type TimeFormatOption = "24h" | "12h";

/** Default IANA timezone when none is provided */
const DEFAULT_TIMEZONE = "Asia/Jerusalem";

/**
 * Format a UTC ISO datetime string into a localized time string
 * using the specified IANA timezone and 24h/12h format.
 *
 * @param utcIsoString - A UTC ISO 8601 datetime string (e.g. "2024-06-15T14:30:00Z")
 * @param timezoneId - An IANA timezone identifier (e.g. "America/New_York"). Defaults to "Asia/Jerusalem" if null/undefined.
 * @param format - "24h" or "12h" display format. Defaults to "24h".
 * @returns Formatted local time string (e.g. "17:30" or "5:30 PM"), or "—" for invalid input.
 */
export function formatLocalTime(
  utcIsoString: string | null | undefined,
  timezoneId: string | null | undefined,
  format: TimeFormatOption = "24h"
): string {
  if (!utcIsoString) return "—";

  try {
    const date = new Date(utcIsoString);
    if (isNaN(date.getTime())) return "—";

    const tz = timezoneId || DEFAULT_TIMEZONE;
    const hour12 = format === "12h";

    const formatter = new Intl.DateTimeFormat("en-US", {
      timeZone: tz,
      hour: "2-digit",
      minute: "2-digit",
      hour12,
    });

    return formatter.format(date);
  } catch {
    // Invalid timezone or other Intl error — fall back gracefully
    return "—";
  }
}

/**
 * Format a UTC ISO datetime string into a localized date+time string
 * using the specified IANA timezone.
 *
 * @param utcIsoString - A UTC ISO 8601 datetime string
 * @param timezoneId - An IANA timezone identifier. Defaults to "Asia/Jerusalem" if null/undefined.
 * @param format - "24h" or "12h" display format. Defaults to "24h".
 * @returns Formatted local date+time string, or "—" for invalid input.
 */
export function formatLocalDateTime(
  utcIsoString: string | null | undefined,
  timezoneId: string | null | undefined,
  format: TimeFormatOption = "24h"
): string {
  if (!utcIsoString) return "—";

  try {
    const date = new Date(utcIsoString);
    if (isNaN(date.getTime())) return "—";

    const tz = timezoneId || DEFAULT_TIMEZONE;
    const hour12 = format === "12h";

    const formatter = new Intl.DateTimeFormat("en-US", {
      timeZone: tz,
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      hour12,
    });

    return formatter.format(date);
  } catch {
    return "—";
  }
}

/**
 * Format a UTC ISO datetime string into a localized date-only string
 * using the specified IANA timezone.
 *
 * @param utcIsoString - A UTC ISO 8601 datetime string
 * @param timezoneId - An IANA timezone identifier. Defaults to "Asia/Jerusalem" if null/undefined.
 * @returns Formatted local date string, or "—" for invalid input.
 */
export function formatLocalDate(
  utcIsoString: string | null | undefined,
  timezoneId: string | null | undefined
): string {
  if (!utcIsoString) return "—";

  try {
    const date = new Date(utcIsoString);
    if (isNaN(date.getTime())) return "—";

    const tz = timezoneId || DEFAULT_TIMEZONE;

    const formatter = new Intl.DateTimeFormat("en-US", {
      timeZone: tz,
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
    });

    return formatter.format(date);
  } catch {
    return "—";
  }
}

/**
 * Get the local hour and minute components for a UTC datetime in a given timezone.
 * Useful for programmatic comparisons rather than display.
 *
 * @param utcIsoString - A UTC ISO 8601 datetime string
 * @param timezoneId - An IANA timezone identifier. Defaults to "Asia/Jerusalem" if null/undefined.
 * @returns Object with { hour, minute } in local time, or null for invalid input.
 */
export function getLocalTimeParts(
  utcIsoString: string | null | undefined,
  timezoneId: string | null | undefined
): { hour: number; minute: number } | null {
  if (!utcIsoString) return null;

  try {
    const date = new Date(utcIsoString);
    if (isNaN(date.getTime())) return null;

    const tz = timezoneId || DEFAULT_TIMEZONE;

    const formatter = new Intl.DateTimeFormat("en-US", {
      timeZone: tz,
      hour: "numeric",
      minute: "numeric",
      hour12: false,
    });

    const parts = formatter.formatToParts(date);
    const hour = parseInt(parts.find((p) => p.type === "hour")?.value ?? "0", 10);
    const minute = parseInt(parts.find((p) => p.type === "minute")?.value ?? "0", 10);

    return { hour, minute };
  } catch {
    return null;
  }
}

/**
 * Ensure a Date or ISO string remains in UTC format for API requests.
 * This is a no-op identity function that documents intent: outgoing
 * values are always UTC, never offset by the client.
 *
 * @param dateOrIso - A Date object or ISO string
 * @returns The UTC ISO string representation
 */
export function toUtcIsoString(dateOrIso: Date | string): string {
  if (typeof dateOrIso === "string") {
    // Validate and normalize
    const d = new Date(dateOrIso);
    if (isNaN(d.getTime())) return dateOrIso;
    return d.toISOString();
  }
  return dateOrIso.toISOString();
}

/**
 * Get today's date as a YYYY-MM-DD string in the user's local timezone.
 * This avoids the common bug where `new Date().toISOString().split("T")[0]`
 * returns yesterday's date for users east of UTC after midnight local time.
 *
 * @param timezoneId - An IANA timezone identifier. Defaults to "Asia/Jerusalem" if null/undefined.
 * @returns Today's date string in YYYY-MM-DD format (e.g. "2026-05-31")
 */
export function getLocalToday(timezoneId?: string | null): string {
  const tz = timezoneId || DEFAULT_TIMEZONE;
  const formatter = new Intl.DateTimeFormat("sv-SE", {
    timeZone: tz,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  });
  return formatter.format(new Date());
}
