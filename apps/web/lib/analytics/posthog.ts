"use client";

import posthog from "posthog-js";
import { isPostHogEnabled } from "@/lib/analytics/posthogConfig";
import { getAnalyticsConsent } from "@/lib/privacy/consent";

const POSTHOG_KEY = process.env.NEXT_PUBLIC_POSTHOG_KEY;
const POSTHOG_HOST = process.env.NEXT_PUBLIC_POSTHOG_HOST || "https://us.i.posthog.com";

/**
 * Initialize PostHog analytics.
 * Only runs in production and only if the key is configured.
 * Call this once from the app's root provider.
 */
export function initPostHog() {
  if (typeof window === "undefined") return;
  const key = POSTHOG_KEY?.trim();
  if (!key || !isPostHogEnabled(key, process.env.NODE_ENV)) return;
  if (getAnalyticsConsent() !== "accepted") return;

  posthog.init(key, {
    api_host: POSTHOG_HOST,
    capture_pageview: true,
    capture_pageleave: true,
    autocapture: true,
    persistence: "localStorage",
    // Respect user privacy
    disable_session_recording: false,
    mask_all_text: false,
    mask_all_element_attributes: false,
  });
}

/**
 * Identify a user after login.
 */
export function identifyUser(userId: string, properties?: Record<string, unknown>) {
  if (!isPostHogEnabled(POSTHOG_KEY, process.env.NODE_ENV)) return;
  if (getAnalyticsConsent() !== "accepted") return;
  posthog.identify(userId, properties);
}

/**
 * Track a custom event.
 */
export function trackEvent(event: string, properties?: Record<string, unknown>) {
  if (!isPostHogEnabled(POSTHOG_KEY, process.env.NODE_ENV)) return;
  if (getAnalyticsConsent() !== "accepted") return;
  posthog.capture(event, properties);
}

/**
 * Reset identity on logout.
 */
export function resetAnalytics() {
  if (!isPostHogEnabled(POSTHOG_KEY, process.env.NODE_ENV)) return;
  posthog.reset();
}
