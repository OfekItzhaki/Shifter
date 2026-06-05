import { getRequestConfig } from "next-intl/server";
import { cookies } from "next/headers";
import {
  DEFAULT_LOCALE,
  SUPPORTED_LOCALES,
  getLocaleDirection,
  isRtl,
  isSupportedLocale,
  type Locale,
} from "@/lib/i18n/locales";

export const locales = SUPPORTED_LOCALES;
export type { Locale };
export const defaultLocale = DEFAULT_LOCALE;

export { getLocaleDirection, isRtl, isSupportedLocale };

export default getRequestConfig(async () => {
  // Read locale from cookie set at login, fall back to Hebrew
  let locale: Locale = defaultLocale;
  try {
    const cookieStore = await cookies();
    const cookieLocale = cookieStore.get("locale")?.value;
    locale = isSupportedLocale(cookieLocale) ? cookieLocale : defaultLocale;
  } catch {
    // cookies() may throw during static generation — use default
  }

  return {
    locale,
    messages: (await import(`../messages/${locale}.json`)).default,
  };
});
