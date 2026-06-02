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

const isDevelopment = process.env.NODE_ENV === "development";

function createNonce(): string {
  return btoa(crypto.randomUUID());
}

function createContentSecurityPolicy(nonce: string): string {
  return [
    "default-src 'self'",
    [
      "script-src",
      "'self'",
      `'nonce-${nonce}'`,
      "'strict-dynamic'",
      "https://client.crisp.chat",
      ...(isDevelopment ? ["'unsafe-eval'"] : []),
    ].join(" "),
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: blob: http://localhost:5000 https://api.shifter.ofeklabs.com https:",
    "font-src 'self'",
    "connect-src 'self' http://localhost:5000 https://api.shifter.ofeklabs.com https://*.ofeklabs.com ws://localhost:3000 wss: https://*.sentry.io https://*.ingest.sentry.io https://*.posthog.com https://us.i.posthog.com https://*.crisp.chat wss://*.crisp.chat",
    "worker-src 'self'",
    "frame-src https://client.crisp.chat https://*.crisp.chat",
    "frame-ancestors 'none'",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "manifest-src 'self'",
    ...(isDevelopment ? [] : ["upgrade-insecure-requests"]),
  ].join("; ");
}

function nextWithSecurityHeaders(request: NextRequest): NextResponse {
  const nonce = createNonce();
  const csp = createContentSecurityPolicy(nonce);
  const requestHeaders = new Headers(request.headers);
  requestHeaders.set("x-nonce", nonce);
  requestHeaders.set("Content-Security-Policy", csp);

  const response = NextResponse.next({
    request: {
      headers: requestHeaders,
    },
  });
  response.headers.set("Content-Security-Policy", csp);
  return response;
}

/**
 * Next.js 16 proxy file (replaces middleware.ts).
 * Checks for a lightweight auth guard cookie on protected routes.
 * If missing → redirects to /login.
 */
export function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Allow root (landing page)
  if (pathname === "/") return nextWithSecurityHeaders(request);

  // Allow public paths
  if (PUBLIC_PATHS.some((p) => pathname.startsWith(p))) return nextWithSecurityHeaders(request);

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

  return nextWithSecurityHeaders(request);
}

export const config = {
  matcher: [
    {
      source: "/((?!_next/static|_next/image|shifter-favicon.png|sw.js|manifest.json).*)",
      missing: [
        { type: "header", key: "next-router-prefetch" },
        { type: "header", key: "purpose", value: "prefetch" },
      ],
    },
  ],
};
