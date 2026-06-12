import * as Sentry from "@sentry/nextjs";
import { isSentryEnabled } from "@/lib/monitoring/sentryConfig";

const sentryDsn = process.env.NEXT_PUBLIC_SENTRY_DSN;

Sentry.init({
  dsn: sentryDsn,

  // Only enable when explicitly configured in production.
  enabled: isSentryEnabled(sentryDsn, process.env.NODE_ENV),

  // Performance monitoring.
  tracesSampleRate: 0.1,

  // Do not send PII.
  sendDefaultPii: false,

  // Environment tag.
  environment: process.env.NODE_ENV,
  release: process.env.NEXT_PUBLIC_APP_VERSION,
});
