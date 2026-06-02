import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

const PUBLIC_PATHS = [
  "/login",
  "/register",
  "/forgot-password",
  "/reset-password",
  "/verify-email",
  "/pricing",
  "/billing",
  "/invitations",
  "/group-opt-out",
  "/error",
];

/**
 * Next.js 16 proxy file (replaces middleware.ts).
 * Checks for a lightweight auth guard cookie on protected routes.
 * If missing → redirects to /login.
 */
export function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Allow root (landing page)
  if (pathname === "/") return NextResponse.next();

  // Allow public paths
  if (PUBLIC_PATHS.some((p) => pathname.startsWith(p))) return NextResponse.next();

  // Allow static files
  if (pathname.includes(".")) return NextResponse.next();

  // Keep a temporary access_token fallback so existing sessions survive rollout
  // after the cookie format changes.
  const hasAuthGuard =
    request.cookies.get("auth_guard")?.value === "1" ||
    !!request.cookies.get("access_token")?.value;

  if (!hasAuthGuard) {
    return NextResponse.redirect(new URL("/login", request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|shifter-favicon.png|sw.js|manifest.json).*)"],
};
