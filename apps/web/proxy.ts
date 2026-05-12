import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

/**
 * Next.js 16 proxy file (replaces middleware.ts).
 * 
 * Auth is handled entirely client-side via localStorage tokens + axios interceptor.
 * This proxy passes all requests through without server-side auth checks,
 * since we can't access localStorage from the server.
 */
export function proxy(_request: NextRequest) {
  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.jpeg|sw.js|manifest.json).*)"],
};
