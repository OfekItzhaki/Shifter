/**
 * Central date formatting utility.
 *
 * Uses the user's preferredLocale (from authStore) to determine the
 * correct date format for their country:
 *   "he"    → Israel    → dd/mm/yyyy  (he-IL)
 *   "en"    → US        → mm/dd/yyyy  (en-US)
 *   "en-gb" → UK        → dd/mm/yyyy  (en-GB)
 *   "ar"    → Arabic    → dd/mm/yyyy  (ar)
 *   etc.
 *
 * All functions accept a locale string (pass useAuthStore().preferredLocale).
 * Falls back to "he-IL" if locale is unrecognised.
 */

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

/** Format a date as a short date string: dd/mm/yyyy or mm/dd/yyyy depending on locale */
export function formatDate(date: string | Date | null | undefined, locale: string): string {
  if (!date) return "—";
  try {
    const d = typeof date === "string" ? new Date(date) : date;
    if (isNaN(d.getTime())) return "—";
    return d.toLocaleDateString(toBcp47(locale), {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
    });
  } catch {
    return String(date);
  }
}

/** Format a date with a long month name: e.g. "27 באפריל 2026" or "April 27, 2026" */
export function formatDateLong(date: string | Date | null | undefined, locale: string): string {
  if (!date) return "—";
  try {
    const d = typeof date === "string" ? new Date(date) : date;
    if (isNaN(d.getTime())) return "—";
    return d.toLocaleDateString(toBcp47(locale), {
      day: "numeric",
      month: "long",
      year: "numeric",
    });
  } catch {
    return String(date);
  }
}

/** Format a datetime: date + time, e.g. "27/04/2026, 07:15" */
export function formatDateTime(date: string | Date | null | undefined, locale: string): string {
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
    });
  } catch {
    return String(date);
  }
}

/** Format just the time: "07:15" */
export function formatTime(date: string | Date | null | undefined, locale: string): string {
  if (!date) return "—";
  try {
    const d = typeof date === "string" ? new Date(date) : date;
    if (isNaN(d.getTime())) return "—";
    return d.toLocaleTimeString(toBcp47(locale), {
      hour: "2-digit",
      minute: "2-digit",
    });
  } catch {
    return String(date);
  }
}

/** Format a short date+time without year: "27 Apr, 07:15" */
export function formatDateTimeShort(date: string | Date | null | undefined, locale: string): string {
  if (!date) return "—";
  try {
    const d = typeof date === "string" ? new Date(date) : date;
    if (isNaN(d.getTime())) return "—";
    return d.toLocaleString(toBcp47(locale), {
      day: "numeric",
      month: "short",
      hour: "2-digit",
      minute: "2-digit",
    });
  } catch {
    return String(date);
  }
}

/** Format a date range: "27/04/2026 – 03/05/2026" */
export function formatDateRange(
  from: string | Date | null | undefined,
  to: string | Date | null | undefined,
  locale: string
): string {
  return `${formatDate(from, locale)} – ${formatDate(to, locale)}`;
}
