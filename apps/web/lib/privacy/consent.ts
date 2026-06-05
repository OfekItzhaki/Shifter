"use client";

export const ANALYTICS_CONSENT_KEY = "shifter_analytics_consent";

export type AnalyticsConsent = "accepted" | "declined";

export function getAnalyticsConsent(): AnalyticsConsent | null {
  if (typeof window === "undefined") return null;
  const value = window.localStorage.getItem(ANALYTICS_CONSENT_KEY);
  return value === "accepted" || value === "declined" ? value : null;
}

export function setAnalyticsConsent(value: AnalyticsConsent): void {
  if (typeof window === "undefined") return;
  window.localStorage.setItem(ANALYTICS_CONSENT_KEY, value);
  window.dispatchEvent(new CustomEvent("analytics-consent-changed", { detail: value }));
}
