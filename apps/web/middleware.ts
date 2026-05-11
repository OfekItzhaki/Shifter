import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

/**
 * Middleware: explicitly handle all routes to prevent any plugin from redirecting.
 * 
 * The 307 redirect to /login was being injected by next-intl's plugin.
 * This middleware takes full control and never redirects.
 */
export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Never redirect — just pass through
  const response = NextResponse.next();

  // Set locale cookie if not present (needed for next-intl server components)
  if (!request.cookies.get("locale")) {
    response.cookies.set("locale", "he", { path: "/", maxAge: 31536000 });
  }

  return response;
}

export const config = {
  matcher: [
    // Match all paths except static files
    "/((?!_next/static|_next/image|favicon\\.jpeg|sw\\.js|manifest\\.json|.*\\..*).*)",
  ],
};
