import createNextIntlPlugin from "next-intl/plugin";
import { withSentryConfig } from "@sentry/nextjs";
import { createRequire } from "module";
import { dirname } from "node:path";
import { fileURLToPath } from "node:url";

const require = createRequire(import.meta.url);
const { version } = require("./package.json");
const appDir = dirname(fileURLToPath(import.meta.url));

const withNextIntl = createNextIntlPlugin("./i18n/request.ts");
const isDevelopment = process.env.NODE_ENV === "development";

const scriptSrc = [
  "'self'",
  // Next.js App Router emits inline bootstrap/flight scripts. Removing this safely
  // requires per-request nonce support in both headers and rendered scripts.
  "'unsafe-inline'",
  "https://client.crisp.chat",
];

if (isDevelopment) {
  scriptSrc.push("'unsafe-eval'");
}

const securityHeaders = [
  { key: "X-Frame-Options", value: "DENY" },
  { key: "X-Content-Type-Options", value: "nosniff" },
  { key: "Referrer-Policy", value: "no-referrer" },
  { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=()" },
  {
    key: "Strict-Transport-Security",
    value: "max-age=63072000; includeSubDomains; preload",
  },
  {
    key: "Content-Security-Policy",
    value: [
      "default-src 'self'",
      `script-src ${scriptSrc.join(" ")}`,
      "style-src 'self' 'unsafe-inline'",
      // Allow images from: self, data URIs, blobs, local API, production API, and any HTTPS source for external profile images
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
    ].join("; "),
  },
];

/** @type {import('next').NextConfig} */
const nextConfig = {
  output: "standalone",
  turbopack: {
    root: appDir,
  },
  env: {
    NEXT_PUBLIC_APP_VERSION: version,
  },
  experimental: {
    viewTransition: true,
  },
  async headers() {
    return [
      {
        source: "/(.*)",
        headers: securityHeaders,
      },
      {
        // Service worker must be served without redirect and with correct scope
        source: "/sw.js",
        headers: [
          { key: "Cache-Control", value: "no-cache, no-store, must-revalidate" },
          { key: "Service-Worker-Allowed", value: "/" },
        ],
      },
      {
        source: "/manifest.json",
        headers: [
          { key: "Content-Type", value: "application/manifest+json" },
        ],
      },
    ];
  },
  async redirects() {
    // Explicitly prevent any redirect from root to login
    // (This is a no-op but ensures no other config adds this redirect)
    return [];
  },
};

export default withSentryConfig(withNextIntl(nextConfig), {
  // Suppress source map upload warnings when no auth token is set
  silent: true,
  // Don't upload source maps (requires Sentry auth token)
  disableServerWebpackPlugin: true,
  disableClientWebpackPlugin: true,
});
