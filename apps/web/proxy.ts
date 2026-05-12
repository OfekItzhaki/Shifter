import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

const PUBLIC_PATHS = [
  "/login",
  "/register",
  "/forgot-password",
  "/reset-password",
  "/verify-email",
  "/pricing",
  "/invitations",
  "/group-opt-out",
  "/error",
];

/**
 * Next.js 16 proxy file (replaces middleware.ts).
 * Checks for access_token cookie on protected routes.
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

  // Check for access_token cookie
  const token = request.cookies.get("access_token")?.value;
  if (!token) {
    return NextResponse.redirect(new URL("/login", request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.jpeg|sw.js|manifest.json).*)"],
};
