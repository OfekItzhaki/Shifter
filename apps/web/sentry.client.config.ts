import * as Sentry from "@sentry/nextjs";
import { ANALYTICS_CONSENT_KEY } from "@/lib/privacy/consent";

function hasAnalyticsConsent(): boolean {
  if (typeof window === "undefined") return false;
  return window.localStorage.getItem(ANALYTICS_CONSENT_KEY) === "accepted";
}

const analyticsConsent = hasAnalyticsConsent();

Sentry.init({
  dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
  
  // Only enable in production
  enabled: process.env.NODE_ENV === "production",

  // Performance monitoring — enabled only after analytics consent
  tracesSampleRate: analyticsConsent ? 0.1 : 0,

  // Session replay — enabled only after analytics consent
  replaysSessionSampleRate: analyticsConsent ? 0.01 : 0,
  replaysOnErrorSampleRate: analyticsConsent ? 1.0 : 0,

  // Don't send PII
  sendDefaultPii: false,

  // Environment tag
  environment: process.env.NODE_ENV,
  release: process.env.NEXT_PUBLIC_APP_VERSION,
});
