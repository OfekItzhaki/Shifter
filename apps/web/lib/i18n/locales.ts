export const SUPPORTED_LOCALES = ["he", "en", "ru"] as const;

export type Locale = (typeof SUPPORTED_LOCALES)[number];
export type LocaleDirection = "ltr" | "rtl";

export const DEFAULT_LOCALE: Locale = "he";
export const PUBLIC_DEFAULT_LOCALE: Locale = "en";

export const LOCALE_META: Record<Locale, { label: string; fullLabel: string; dir: LocaleDirection }> = {
  he: { label: "עב", fullLabel: "עברית", dir: "rtl" },
  en: { label: "EN", fullLabel: "English", dir: "ltr" },
  ru: { label: "RU", fullLabel: "Русский", dir: "ltr" },
};

export function isSupportedLocale(value: string | null | undefined): value is Locale {
  return !!value && (SUPPORTED_LOCALES as readonly string[]).includes(value);
}

export function getLocaleDirection(locale: string | null | undefined): LocaleDirection {
  return isSupportedLocale(locale) ? LOCALE_META[locale].dir : LOCALE_META[DEFAULT_LOCALE].dir;
}

export function isRtl(locale: string | null | undefined): boolean {
  return getLocaleDirection(locale) === "rtl";
}
