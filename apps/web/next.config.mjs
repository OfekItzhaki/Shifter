import createNextIntlPlugin from "next-intl/plugin";
import { createRequire } from "module";

const require = createRequire(import.meta.url);
const { version } = require("./package.json");

const withNextIntl = createNextIntlPlugin("./i18n/request.ts");

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
      "script-src 'self' 'unsafe-eval' 'unsafe-inline'",
      "style-src 'self' 'unsafe-inline'",
      // Allow images from: self, data URIs, blobs, local API, production API, and any HTTPS source for external profile images
      "img-src 'self' data: blob: http://localhost:5000 https://api.jobuler.app https:",
      "font-src 'self'",
      "connect-src 'self' http://localhost:5000 https://api.jobuler.app ws://localhost:3000 wss:",
      "worker-src 'self'",
      "frame-ancestors 'none'",
    ].join("; "),
  },
];

/** @type {import('next').NextConfig} */
const nextConfig = {
  output: "standalone",
  env: {
    NEXT_PUBLIC_APP_VERSION: version,
  },
  experimental: {
    // optimizeCss causes CSS preload warnings in dev mode — disabled
  },
  turbopack: {
    // Fix CSS MIME type error caused by Windows absolute path in chunk names.
    // Use __dirname equivalent via fileURLToPath for cross-platform compatibility.
    root: new URL(".", import.meta.url).pathname.replace(/^\/([A-Z]:)/, "$1"),
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

export default withNextIntl(nextConfig);
