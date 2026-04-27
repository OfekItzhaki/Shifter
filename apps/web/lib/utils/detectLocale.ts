/**
 * Detects the user's preferred locale from the browser.
 * Uses Intl.DateTimeFormat to get the system timezone/locale — no external API needed.
 * Falls back to "he" (Hebrew/Israel) as the app default.
 */
export function detectBrowserLocale(): string {
  if (typeof window === "undefined") return "he";

  // navigator.language gives the full BCP-47 tag e.g. "he-IL", "en-US"
  const lang = navigator.language || navigator.languages?.[0] || "he";

  // Map to our app's supported locale codes
  const primary = lang.split("-")[0].toLowerCase();
  const supported = ["he", "en", "ar", "ru", "fr", "de", "es"];

  return supported.includes(primary) ? primary : "he";
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
