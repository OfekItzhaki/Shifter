import { redirect } from "next/navigation";
import { cookies } from "next/headers";
import LandingPage from "./LandingPage";

/**
 * Root page:
 * - Authenticated users → redirect to /spaces (then to schedule)
 * - Unauthenticated users → show landing/marketing page
 * 
 * We check for the presence of an access_token cookie or rely on
 * client-side redirect. Since tokens are in localStorage (not cookies),
 * we always show the landing page on the server and let the client redirect.
 */
export default function RootPage() {
  return <LandingPage />;
}
