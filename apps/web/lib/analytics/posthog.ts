"use client";

import posthog from "posthog-js";

const POSTHOG_KEY = process.env.NEXT_PUBLIC_POSTHOG_KEY;
const POSTHOG_HOST = process.env.NEXT_PUBLIC_POSTHOG_HOST || "https://us.i.posthog.com";

/**
 * Initialize PostHog analytics.
 * Only runs in production and only if the key is configured.
 * Call this once from the app's root provider.
 */
export function initPostHog() {
  if (typeof window === "undefined") return;
  if (!POSTHOG_KEY) return;
  if (process.env.NODE_ENV !== "production") return;

  posthog.init(POSTHOG_KEY, {
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
  if (!POSTHOG_KEY) return;
  posthog.identify(userId, properties);
}

/**
 * Track a custom event.
 */
export function trackEvent(event: string, properties?: Record<string, unknown>) {
  if (!POSTHOG_KEY) return;
  posthog.capture(event, properties);
}

/**
 * Reset identity on logout.
 */
export function resetAnalytics() {
  if (!POSTHOG_KEY) return;
  posthog.reset();
}
