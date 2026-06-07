import LandingPage from "./LandingPage";
import { getLocale } from "next-intl/server";
import { PUBLIC_DEFAULT_LOCALE, isSupportedLocale } from "@/lib/i18n/locales";

/**
 * Root page:
 * - Authenticated users → client-side redirect to /spaces
 * - Unauthenticated users → show landing/marketing page
 * 
 * The landing page always renders on the server. The client checks for a
 * short-lived access token and redirects if the session is still valid.
 */
export default async function RootPage() {
  const locale = await getLocale();
  const initialLocale = isSupportedLocale(locale) ? locale : PUBLIC_DEFAULT_LOCALE;

  return <LandingPage initialLocale={initialLocale} />;
}
