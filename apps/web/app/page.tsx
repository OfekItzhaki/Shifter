import LandingPage from "./LandingPage";

/**
 * Root page:
 * - Authenticated users → client-side redirect to /spaces
 * - Unauthenticated users → show landing/marketing page
 * 
 * Since tokens are in localStorage (not cookies), the landing page
 * always renders on the server. The client checks for a token and redirects.
 */
export default function RootPage() {
  return <LandingPage />;
}
