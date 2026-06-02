import LandingPage from "./LandingPage";

/**
 * Root page:
 * - Authenticated users → client-side redirect to /spaces
 * - Unauthenticated users → show landing/marketing page
 * 
 * The landing page always renders on the server. The client checks for a
 * short-lived access token and redirects if the session is still valid.
 */
export default function RootPage() {
  return <LandingPage />;
}
