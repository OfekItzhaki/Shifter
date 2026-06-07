"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { initPostHog } from "@/lib/analytics/posthog";
import { isRtl as isRtlLocale, PUBLIC_DEFAULT_LOCALE, isSupportedLocale, type Locale } from "@/lib/i18n/locales";
import {
  getAnalyticsConsent,
  setAnalyticsConsent,
  type AnalyticsConsent,
} from "@/lib/privacy/consent";
import enMessages from "@/messages/en.json";
import heMessages from "@/messages/he.json";
import ruMessages from "@/messages/ru.json";

const PRIVACY_MESSAGES = {
  en: enMessages.privacyConsent,
  he: heMessages.privacyConsent,
  ru: ruMessages.privacyConsent,
} satisfies Record<Locale, typeof enMessages.privacyConsent>;

function readLocale(): Locale {
  if (typeof document === "undefined") return PUBLIC_DEFAULT_LOCALE;
  const cookieLocale = document.cookie.match(/(?:^|;\s*)locale=([^;]+)/)?.[1];
  const decodedLocale = cookieLocale ? decodeURIComponent(cookieLocale) : null;
  return isSupportedLocale(decodedLocale) ? decodedLocale : PUBLIC_DEFAULT_LOCALE;
}

const primaryButtonStyle = {
  background: "#0ea5e9",
  color: "white",
  border: "none",
  borderRadius: "6px",
  padding: "6px 16px",
  fontSize: "0.8125rem",
  fontWeight: 600,
  cursor: "pointer",
  whiteSpace: "nowrap",
  flexShrink: 0,
} as const;

export default function CookieConsentBanner() {
  const [consent, setConsent] = useState<AnalyticsConsent | null | "loading">("loading");
  const [showOptions, setShowOptions] = useState(false);
  const [locale, setLocale] = useState<Locale>(PUBLIC_DEFAULT_LOCALE);

  useEffect(() => {
    setConsent(getAnalyticsConsent());
    setLocale(readLocale());
  }, []);

  useEffect(() => {
    function handleLocaleChanged(event: Event) {
      const detail = (event as CustomEvent<string>).detail;
      setLocale(isSupportedLocale(detail) ? detail : readLocale());
    }

    window.addEventListener("shifter-locale-changed", handleLocaleChanged);
    return () => window.removeEventListener("shifter-locale-changed", handleLocaleChanged);
  }, []);

  function choose(value: AnalyticsConsent) {
    setAnalyticsConsent(value);
    setConsent(value);
    if (value === "accepted") {
      initPostHog();
    }
  }

  if (consent !== null) return null;

  const isRtl = isRtlLocale(locale);
  const t = PRIVACY_MESSAGES[locale];

  return (
    <aside
      dir={isRtl ? "rtl" : "ltr"}
      aria-label={t.title}
      style={{
        position: "fixed",
        bottom: 0,
        left: 0,
        right: 0,
        zIndex: 9999,
        background: "#1e293b",
        borderTop: "1px solid #334155",
        padding: "12px 20px",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        gap: "12px",
        flexWrap: "wrap",
      }}
    >
      <p style={{ color: "#cbd5e1", fontSize: "0.8125rem", margin: 0, lineHeight: 1.5 }}>
        <strong style={{ color: "#fff" }}>{t.title}: </strong>
        {showOptions ? t.body : t.summary}{" "}
        <Link href="/privacy" style={{ color: "#7dd3fc", textDecoration: "underline" }}>
          {t.privacyLink}
        </Link>
      </p>
      {showOptions ? (
        <button type="button" onClick={() => choose("accepted")} style={primaryButtonStyle}>
          {t.accept}
        </button>
      ) : null}
      <button type="button" onClick={() => choose("declined")} style={primaryButtonStyle}>
        {showOptions ? t.decline : t.understood}
      </button>
      {!showOptions ? (
        <button
          type="button"
          onClick={() => setShowOptions(true)}
          aria-expanded={showOptions}
          style={primaryButtonStyle}
        >
          {t.options}
        </button>
      ) : null}
    </aside>
  );
}
