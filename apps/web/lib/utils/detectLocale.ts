import { DEFAULT_LOCALE, isSupportedLocale } from "@/lib/i18n/locales";

/**
 * Detects the user's preferred locale based on their timezone (location-based).
 * Priority: timezone → browser language → fallback "he"
 *
 * Mapping:
 * - Israel timezones → Hebrew
 * - Russia/Ukraine/Belarus/Kazakhstan timezones → Russian
 * - Everything else → English
 */
export function detectBrowserLocale(): string {
  if (typeof window === "undefined") return DEFAULT_LOCALE;

  // First try timezone-based detection (most accurate for location)
  const tzLocale = detectLocaleFromTimezone();
  if (tzLocale) return tzLocale;

  // Fallback to browser language
  const lang = navigator.language || navigator.languages?.[0] || DEFAULT_LOCALE;
  const primary = lang.split("-")[0].toLowerCase();

  return isSupportedLocale(primary) ? primary : "en";
}

/**
 * Detects locale from the system timezone.
 * This is location-based — gives the language of the country the user is in.
 */
function detectLocaleFromTimezone(): string | null {
  try {
    const tz = Intl.DateTimeFormat().resolvedOptions().timeZone;
    if (!tz) return null;

    // Israel → Hebrew
    if (tz.startsWith("Asia/Jerusalem") || tz.startsWith("Asia/Tel_Aviv")) {
      return "he";
    }

    // Russian-speaking countries → Russian
    const russianTimezones = [
      "Europe/Moscow", "Europe/Kaliningrad", "Europe/Samara", "Europe/Volgograd",
      "Asia/Yekaterinburg", "Asia/Omsk", "Asia/Novosibirsk", "Asia/Krasnoyarsk",
      "Asia/Irkutsk", "Asia/Yakutsk", "Asia/Vladivostok", "Asia/Kamchatka",
      // Ukraine
      "Europe/Kiev", "Europe/Kyiv", "Europe/Zaporozhye", "Europe/Uzhgorod",
      // Belarus
      "Europe/Minsk",
      // Kazakhstan
      "Asia/Almaty", "Asia/Aqtau", "Asia/Aqtobe", "Asia/Atyrau", "Asia/Oral", "Asia/Qyzylorda",
    ];
    if (russianTimezones.some(rtz => tz === rtz || tz.startsWith(rtz))) {
      return "ru";
    }

    // Everything else → English
    return "en";
  } catch {
    return null;
  }
}

/**
 * Returns the country code from the browser timezone.
 * e.g. "Asia/Jerusalem" → "IL", "America/New_York" → "US"
 * Used as a hint for date format — not authoritative.
 */
export function detectCountryFromTimezone(): string | null {
  try {
    const tz = Intl.DateTimeFormat().resolvedOptions().timeZone;
    const tzToCountry: Record<string, string> = {
      "Asia/Jerusalem": "IL",
      "Asia/Tel_Aviv": "IL",
      "America/New_York": "US",
      "America/Chicago": "US",
      "America/Denver": "US",
      "America/Los_Angeles": "US",
      "Europe/London": "GB",
      "Europe/Paris": "FR",
      "Europe/Berlin": "DE",
      "Europe/Moscow": "RU",
      "Europe/Kiev": "UA",
      "Europe/Kyiv": "UA",
      "Europe/Minsk": "BY",
      "Asia/Almaty": "KZ",
      "Asia/Dubai": "AE",
      "Asia/Riyadh": "SA",
      "Asia/Amman": "JO",
      "Asia/Beirut": "LB",
      "Africa/Cairo": "EG",
    };
    return tzToCountry[tz] ?? null;
  } catch {
    return null;
  }
}
