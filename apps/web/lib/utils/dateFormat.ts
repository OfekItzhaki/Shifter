/**
 * Central date formatting utility — timezone-aware.
 *
 * Uses the user's preferredLocale (from authStore) to determine the
 * correct date format for their country:
 *   "he"    → Israel    → dd/mm/yyyy  (he-IL)
 *   "en"    → US        → mm/dd/yyyy  (en-US)
 *   "en-gb" → UK        → dd/mm/yyyy  (en-GB)
 *   "ar"    → Arabic    → dd/mm/yyyy  (ar)
 *   etc.
 *
 * All functions accept a locale string (pass useAuthStore().preferredLocale),
 * an optional hour12 flag (false = 24h, true = 12h AM/PM), and an optional
 * IANA timezoneId for correct DST-aware display.
 * Falls back to "he-IL" if locale is unrecognised.
 * Falls back to "Asia/Jerusalem" if timezoneId is null/undefined.
 */

export type TimeFormatOption = "24h" | "12h";

/** Default IANA timezone when none is provided */
const DEFAULT_TIMEZONE = "Asia/Jerusalem";

/** Map our app locale codes to BCP-47 locale tags */
function toBcp47(locale: string): string {
  // If already a full BCP-47 tag (e.g. "he-IL", "en-US"), use directly
  if (locale.includes("-")) return locale;

  const map: Record<string, string> = {
    he: "he-IL",
    en: "en-US",
    "en-us": "en-US",
    "en-gb": "en-GB",
    ar: "ar-SA",
    ru: "ru-RU",
    fr: "fr-FR",
    de: "de-DE",
    es: "es-ES",
  };
  return map[locale?.toLowerCase()] ?? "he-IL";
}

/** Convert TimeFormatOption to the hour12 boolean for Intl */
function getHour12(timeFormat?: TimeFormatOption): boolean {
  return timeFormat === "12h";
}

/** Resolve the effective timezone — never returns null/undefined */
function resolveTimezone(timezoneId?: string | null): string {
  return timezoneId || DEFAULT_TIMEZONE;
}

/** Format a date as a short date string: dd/mm/yyyy or mm/dd/yyyy depending on locale */
export function formatDate(date: string | Date | null | undefined, locale: string, _timeFormat?: TimeFormatOption, timezoneId?: string | null): string {
  if (!date) return "—";
  try {
    const d = typeof date === "string" ? new Date(date) : date;
    if (isNaN(d.getTime())) return "—";
    return d.toLocaleDateString(toBcp47(locale), {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
      timeZone: resolveTimezone(timezoneId),
    });
  } catch {
    return String(date);
  }
}

/** Format a date with a long month name: e.g. "27 באפריל 2026" or "April 27, 2026" */
export function formatDateLong(date: string | Date | null | undefined, locale: string, _timeFormat?: TimeFormatOption, timezoneId?: string | null): string {
  if (!date) return "—";
  try {
    const d = typeof date === "string" ? new Date(date) : date;
    if (isNaN(d.getTime())) return "—";
    return d.toLocaleDateString(toBcp47(locale), {
      day: "numeric",
      month: "long",
      year: "numeric",
      timeZone: resolveTimezone(timezoneId),
    });
  } catch {
    return String(date);
  }
}

/** Format a datetime: date + time, e.g. "27/04/2026, 07:15" or "27/04/2026, 7:15 AM" */
export function formatDateTime(date: string | Date | null | undefined, locale: string, timeFormat?: TimeFormatOption, timezoneId?: string | null): string {
  if (!date) return "—";
  try {
    const d = typeof date === "string" ? new Date(date) : date;
    if (isNaN(d.getTime())) return "—";
    return d.toLocaleString(toBcp47(locale), {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
      hour12: getHour12(timeFormat),
      timeZone: resolveTimezone(timezoneId),
    });
  } catch {
    return String(date);
  }
}

/** Format just the time: "07:15" or "7:15 AM" */
export function formatTime(date: string | Date | null | undefined, locale: string, timeFormat?: TimeFormatOption, timezoneId?: string | null): string {
  if (!date) return "—";
  try {
    const d = typeof date === "string" ? new Date(date) : date;
    if (isNaN(d.getTime())) return "—";
    return d.toLocaleTimeString(toBcp47(locale), {
      hour: "2-digit",
      minute: "2-digit",
      hour12: getHour12(timeFormat),
      timeZone: resolveTimezone(timezoneId),
    });
  } catch {
    return String(date);
  }
}

/** Format a short date+time without year: "27 Apr, 07:15" or "27 Apr, 7:15 AM" */
export function formatDateTimeShort(date: string | Date | null | undefined, locale: string, timeFormat?: TimeFormatOption, timezoneId?: string | null): string {
  if (!date) return "—";
  try {
    const d = typeof date === "string" ? new Date(date) : date;
    if (isNaN(d.getTime())) return "—";
    return d.toLocaleString(toBcp47(locale), {
      day: "numeric",
      month: "short",
      hour: "2-digit",
      minute: "2-digit",
      hour12: getHour12(timeFormat),
      timeZone: resolveTimezone(timezoneId),
    });
  } catch {
    return String(date);
  }
}

/** Format a date range: "27/04/2026 – 03/05/2026" */
export function formatDateRange(
  from: string | Date | null | undefined,
  to: string | Date | null | undefined,
  locale: string,
  _timeFormat?: TimeFormatOption,
  timezoneId?: string | null
): string {
  return `${formatDate(from, locale, _timeFormat, timezoneId)} – ${formatDate(to, locale, _timeFormat, timezoneId)}`;
}
