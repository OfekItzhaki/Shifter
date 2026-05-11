import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

/**
 * Next.js middleware that checks for the presence of an access_token cookie.
 * If the cookie is missing on a protected route, redirects to /login.
 *
 * This prevents the browser from showing a raw 401 error page when
 * Next.js RSC fetches hit the server without valid auth.
 *
 * Note: This does NOT validate the token (no JWT verification on the edge).
 * It only checks presence. If the token is expired, the client-side axios
 * interceptor handles refresh. If refresh fails, it redirects to /error/unauthorized.
 */

const PUBLIC_PATHS = [
  "/login",
  "/register",
  "/forgot-password",
  "/reset-password",
  "/pricing",
  "/invitations",
  "/group-opt-out",
  "/error",
  "/_next",
  "/api",
  "/favicon",
  "/manifest.json",
];

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Allow the root/landing page
  if (pathname === "/") {
    return NextResponse.next();
  }

  // Allow public paths
  if (PUBLIC_PATHS.some((p) => pathname.startsWith(p))) {
    return NextResponse.next();
  }

  // Allow static files
  if (pathname.includes(".")) {
    return NextResponse.next();
  }

  // Check for access_token cookie
  const token = request.cookies.get("access_token")?.value;

  if (!token) {
    // No token — redirect to login
    const loginUrl = new URL("/login", request.url);
    return NextResponse.redirect(loginUrl);
  }

  // Basic JWT expiry check (decode payload without verification)
  try {
    const parts = token.split(".");
    if (parts.length === 3) {
      const payload = JSON.parse(atob(parts[1].replace(/-/g, "+").replace(/_/g, "/")));
      if (payload.exp && payload.exp * 1000 < Date.now()) {
        // Token expired — clear cookie and redirect to login
        const response = NextResponse.redirect(new URL("/login", request.url));
        response.cookies.delete("access_token");
        return response;
      }
    }
  } catch {
    // If we can't decode the token, let it through — the API will reject it
    // and the axios interceptor will handle the refresh
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    /*
     * Match all request paths except:
     * - _next/static (static files)
     * - _next/image (image optimization)
     * - favicon.ico
     */
    "/((?!_next/static|_next/image|favicon.ico).*)",
  ],
};
